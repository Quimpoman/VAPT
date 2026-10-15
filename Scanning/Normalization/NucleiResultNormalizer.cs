using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EVOLVE_VAPT.Scanning.Normalization
{
    public sealed class NucleiResultNormalizer :
        IScanResultNormalizer
    {
        private static readonly HashSet<string>
            SupportedSeverityCodes =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    "INFO",
                    "LOW",
                    "MEDIUM",
                    "HIGH",
                    "CRITICAL"
                };


        public string ScannerTypeCode =>
            "NUCLEI";


        public IReadOnlyList<NormalizedFinding> Normalize(string rawOutput)
        {
            /*
             * An empty Nuclei stdout is valid.
             *
             * Unlike Nmap XML, Nuclei produces no JSONL
             * records when no templates match.
             */
            if (string.IsNullOrWhiteSpace(
                rawOutput))
            {
                return
                    Array.Empty<NormalizedFinding>();
            }


            var findings =
                new List<NormalizedFinding>();


            /*
             * Used only to guarantee identifiers remain unique
             * inside this individual scan result.
             *
             * We do not silently discard duplicate source records.
             */
            var sourceIdentifiers =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);


            using var reader =
                new StringReader(
                    rawOutput);


            string? line;

            var lineNumber =
                0;


            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;


                if (string.IsNullOrWhiteSpace(
                    line))
                {
                    continue;
                }


                try
                {
                    using var document =
                        JsonDocument.Parse(
                            line);


                    var root =
                        document.RootElement;


                    if (root.ValueKind !=
                        JsonValueKind.Object)
                    {
                        throw new InvalidOperationException(
                            "The JSONL record is not a JSON object.");
                    }


                    /*
                     * Nuclei normally outputs positive matches.
                     * If matcher-status is explicitly false,
                     * do not convert it into a Finding.
                     */
                    var matcherStatus =
                        GetBoolean(
                            root,
                            "matcher-status");


                    if (matcherStatus == false)
                    {
                        continue;
                    }


                    var templateId =
                        GetRequiredString(
                            root,
                            "template-id");


                    if (!TryGetObject(
                        root,
                        "info",
                        out var info))
                    {
                        throw new InvalidOperationException(
                            $"Nuclei template '{templateId}' is missing its info object.");
                    }


                    var templateName =
                        GetRequiredString(
                            info,
                            "name");


                    var severity =
                        GetRequiredString(
                                info,
                                "severity")
                            .ToUpperInvariant();


                    /*
                     * Fail closed instead of inventing or
                     * silently downgrading an unknown severity.
                     */
                    if (!SupportedSeverityCodes.Contains(
                        severity))
                    {
                        throw new InvalidOperationException(
                            $"Nuclei template '{templateId}' returned unsupported severity '{severity}'.");
                    }


                    var matcherName =
                        GetString(
                            root,
                            "matcher-name");


                    /*
                     * matched-at is the most specific location
                     * describing where Nuclei identified the issue.
                     */
                    var targetValue =
                        GetString(
                            root,
                            "matched-at")
                        ??
                        GetString(
                            root,
                            "url")
                        ??
                        GetString(
                            root,
                            "host");


                    if (string.IsNullOrWhiteSpace(
                        targetValue))
                    {
                        throw new InvalidOperationException(
                            $"Nuclei template '{templateId}' did not provide a usable target.");
                    }


                    var sourceFindingIdentifier =
                        BuildSourceFindingIdentifier(
                            templateId,
                            matcherName,
                            targetValue);


                    /*
                     * Preserve every source record.
                     *
                     * If Nuclei legitimately returns the same
                     * template/matcher/location more than once,
                     * append the JSONL line number instead of
                     * silently discarding the record.
                     */
                    if (!sourceIdentifiers.Add(
                        sourceFindingIdentifier))
                    {
                        sourceFindingIdentifier =
                            $"{sourceFindingIdentifier}-{lineNumber:D4}";


                        sourceIdentifiers.Add(
                            sourceFindingIdentifier);
                    }


                    var description =
                        GetString(
                            info,
                            "description")
                        ??
                        $"Nuclei matched template '{templateId}' against the target.";


                    var recommendation =
                        GetString(
                            info,
                            "remediation")
                        ??
                        "Review the Nuclei match, validate whether the condition applies to the target, and remediate the underlying configuration or vulnerability according to the affected technology's security guidance.";


                    var reference =
                        GetString(
                            root,
                            "template-url")
                        ??
                        GetFirstStringValue(
                            info,
                            "reference")
                        ??
                        $"NUCLEI:{templateId}";


                    findings.Add(
                        new NormalizedFinding
                        {
                            SourceFindingIdentifier =
                                sourceFindingIdentifier,

                            Title =
                                BuildTitle(
                                    templateName,
                                    matcherName),

                            SeverityCode =
                                severity,

                            TargetValue =
                                targetValue,

                            Description =
                                description,

                            Evidence =
                                BuildEvidence(
                                    root,
                                    info,
                                    templateId,
                                    matcherName,
                                    targetValue),

                            Recommendation =
                                recommendation,

                            Reference =
                                reference
                        });
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException(
                        $"Nuclei JSONL line {lineNumber} contains invalid JSON.",
                        ex);
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException(
                        $"Nuclei JSONL line {lineNumber} is invalid: {ex.Message}",
                        ex);
                }
            }


            return findings;
        }


        private static string BuildTitle(
            string templateName,
            string? matcherName)
        {
            if (string.IsNullOrWhiteSpace(
                matcherName))
            {
                return templateName;
            }


            /*
             * Preserve Nuclei's matcher identifier so multiple
             * results from one template remain distinguishable.
             */
            return
                $"{templateName} ({matcherName})";
        }


        private static string
            BuildSourceFindingIdentifier(
                string templateId,
                string? matcherName,
                string targetValue)
        {
            var matcherIdentifier =
                string.IsNullOrWhiteSpace(
                    matcherName)
                    ? "MATCH"
                    : matcherName;


            /*
             * Include the affected location in a short deterministic
             * hash. A single Nuclei template may match several
             * endpoints using the same matcher.
             */
            var identityValue =
                $"{templateId}|{matcherIdentifier}|{targetValue}";


            var hash =
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        identityValue));


            var shortHash =
                Convert.ToHexString(
                    hash.AsSpan(
                        0,
                        6));


            return
                $"NUCLEI-{SanitizeIdentifierPart(templateId, 60)}-" +
                $"{SanitizeIdentifierPart(matcherIdentifier, 50)}-" +
                shortHash;
        }


        private static string
            SanitizeIdentifierPart(
                string value,
                int maxLength)
        {
            var builder =
                new StringBuilder();


            foreach (var character in
                value.Trim())
            {
                if (char.IsLetterOrDigit(
                        character) ||
                    character == '-' ||
                    character == '_' ||
                    character == '.')
                {
                    builder.Append(
                        character);
                }
                else
                {
                    builder.Append(
                        '_');
                }


                if (builder.Length >=
                    maxLength)
                {
                    break;
                }
            }


            var result =
                builder
                    .ToString()
                    .Trim('_');


            return string.IsNullOrWhiteSpace(
                result)
                    ? "VALUE"
                    : result;
        }


        private static string BuildEvidence(
            JsonElement root,
            JsonElement info,
            string templateId,
            string? matcherName,
            string targetValue)
        {
            var lines =
                new List<string>
                {
                    $"Template ID: {templateId}",
                    $"Target: {targetValue}"
                };


            AddIfPresent(
                lines,
                "Matcher",
                matcherName);


            AddIfPresent(
                lines,
                "Type",
                GetString(
                    root,
                    "type"));


            AddIfPresent(
                lines,
                "Host",
                GetString(
                    root,
                    "host"));


            AddIfPresent(
                lines,
                "IP Address",
                GetString(
                    root,
                    "ip"));


            AddIfPresent(
                lines,
                "Port",
                GetScalarString(
                    root,
                    "port"));


            AddIfPresent(
                lines,
                "Scheme",
                GetString(
                    root,
                    "scheme"));


            AddIfPresent(
                lines,
                "Path",
                GetString(
                    root,
                    "path"));


            AddIfPresent(
                lines,
                "Matched At",
                GetString(
                    root,
                    "matched-at"));


            AddIfPresent(
                lines,
                "Timestamp",
                GetString(
                    root,
                    "timestamp"));


            var tags =
                GetStringValues(
                    info,
                    "tags");


            if (tags.Count > 0)
            {
                lines.Add(
                    $"Tags: {string.Join(", ", tags)}");
            }


            if (TryGetObject(
                info,
                "classification",
                out var classification))
            {
                var cves =
                    GetStringValues(
                        classification,
                        "cve-id");


                if (cves.Count > 0)
                {
                    lines.Add(
                        $"CVE: {string.Join(", ", cves)}");
                }


                var cwes =
                    GetStringValues(
                        classification,
                        "cwe-id");


                if (cwes.Count > 0)
                {
                    lines.Add(
                        $"CWE: {string.Join(", ", cwes)}");
                }
            }


            var matcherStatus =
                GetBoolean(
                    root,
                    "matcher-status");


            if (matcherStatus.HasValue)
            {
                lines.Add(
                    $"Matcher Status: {matcherStatus.Value}");
            }


            /*
             * Intentionally do not copy Nuclei's curl-command
             * into normalized Evidence.
             *
             * Future authenticated scans could include sensitive
             * request headers or tokens there. The untouched raw
             * scanner output remains available under its own
             * protected evidence controls.
             */
            return string.Join(
                Environment.NewLine,
                lines);
        }


        private static void AddIfPresent(
            ICollection<string> lines,
            string label,
            string? value)
        {
            if (!string.IsNullOrWhiteSpace(
                value))
            {
                lines.Add(
                    $"{label}: {value}");
            }
        }


        private static string
            GetRequiredString(
                JsonElement element,
                string propertyName)
        {
            var value =
                GetString(
                    element,
                    propertyName);


            if (string.IsNullOrWhiteSpace(
                value))
            {
                throw new InvalidOperationException(
                    $"Required property '{propertyName}' is missing or empty.");
            }


            return value;
        }


        private static string?
            GetString(
                JsonElement element,
                string propertyName)
        {
            if (!element.TryGetProperty(
                    propertyName,
                    out var property) ||
                property.ValueKind !=
                    JsonValueKind.String)
            {
                return null;
            }


            var value =
                property
                    .GetString()
                    ?.Trim();


            return string.IsNullOrWhiteSpace(
                value)
                    ? null
                    : value;
        }


        private static string?
            GetScalarString(
                JsonElement element,
                string propertyName)
        {
            if (!element.TryGetProperty(
                propertyName,
                out var property))
            {
                return null;
            }


            return property.ValueKind switch
            {
                JsonValueKind.String =>
                    property.GetString()?.Trim(),

                JsonValueKind.Number =>
                    property.GetRawText(),

                _ =>
                    null
            };
        }


        private static bool?
            GetBoolean(
                JsonElement element,
                string propertyName)
        {
            if (!element.TryGetProperty(
                    propertyName,
                    out var property))
            {
                return null;
            }


            return property.ValueKind switch
            {
                JsonValueKind.True =>
                    true,

                JsonValueKind.False =>
                    false,

                _ =>
                    null
            };
        }


        private static bool TryGetObject(
            JsonElement element,
            string propertyName,
            out JsonElement value)
        {
            if (element.TryGetProperty(
                    propertyName,
                    out value) &&
                value.ValueKind ==
                    JsonValueKind.Object)
            {
                return true;
            }


            value =
                default;


            return false;
        }


        private static IReadOnlyList<string>
            GetStringValues(
                JsonElement element,
                string propertyName)
        {
            if (!element.TryGetProperty(
                    propertyName,
                    out var property))
            {
                return Array.Empty<string>();
            }


            if (property.ValueKind ==
                JsonValueKind.String)
            {
                var value =
                    property
                        .GetString()
                        ?.Trim();


                return string.IsNullOrWhiteSpace(
                    value)
                        ? Array.Empty<string>()
                        : new[] { value };
            }


            if (property.ValueKind !=
                JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }


            var values =
                property
                    .EnumerateArray()

                    .Where(x =>
                        x.ValueKind ==
                        JsonValueKind.String)

                    .Select(x =>
                        x.GetString()
                            ?.Trim())

                    .Where(x =>
                        !string.IsNullOrWhiteSpace(
                            x))

                    .Select(x =>
                        x!)

                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)

                    .ToList();


            return values;
        }


        private static string?
            GetFirstStringValue(
                JsonElement element,
                string propertyName)
        {
            return GetStringValues(
                    element,
                    propertyName)
                .FirstOrDefault();
        }
    }
}