using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EVOLVE_VAPT.Scanning.Normalization
{
    public sealed class ZapResultNormalizer :
        IScanResultNormalizer
    {
        public string ScannerTypeCode =>
            "ZAP";


        public IReadOnlyList<NormalizedFinding>
            Normalize(string rawOutput)
        {
            if (string.IsNullOrWhiteSpace(
                rawOutput))
            {
                throw new InvalidOperationException(
                    "ZAP report is empty.");
            }


            JsonDocument document;


            try
            {
                document =
                    JsonDocument.Parse(
                        rawOutput);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    "ZAP report contains invalid JSON.",
                    ex);
            }


            using (document)
            {
                var root =
                    document.RootElement;


                if (root.ValueKind !=
                    JsonValueKind.Object)
                {
                    throw new InvalidOperationException(
                        "ZAP report root must be a JSON object.");
                }


                var programName =
                    GetOptionalString(
                        root,
                        "@programName");


                if (!string.Equals(
                        programName,
                        "ZAP",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The JSON report was not produced by ZAP.");
                }


                if (!root.TryGetProperty(
                        "site",
                        out var sitesElement) ||
                    sitesElement.ValueKind !=
                        JsonValueKind.Array)
                {
                    throw new InvalidOperationException(
                        "ZAP report does not contain a valid site collection.");
                }


                var findings =
                    new List<NormalizedFinding>();


                var usedIdentifiers =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);


                foreach (var site in
                    sitesElement.EnumerateArray())
                {
                    if (site.ValueKind !=
                        JsonValueKind.Object)
                    {
                        continue;
                    }


                    var siteName =
                        GetOptionalString(
                            site,
                            "@name");


                    if (string.IsNullOrWhiteSpace(
                        siteName))
                    {
                        throw new InvalidOperationException(
                            "ZAP report contains a site without a name.");
                    }


                    if (!site.TryGetProperty(
                            "alerts",
                            out var alertsElement))
                    {
                        continue;
                    }


                    if (alertsElement.ValueKind !=
                        JsonValueKind.Array)
                    {
                        throw new InvalidOperationException(
                            $"ZAP site '{siteName}' contains an invalid alerts collection.");
                    }


                    var alertIndex =
                        0;


                    foreach (var alert in
                        alertsElement.EnumerateArray())
                    {
                        alertIndex++;


                        if (alert.ValueKind !=
                            JsonValueKind.Object)
                        {
                            throw new InvalidOperationException(
                                $"ZAP alert {alertIndex} for site '{siteName}' is invalid.");
                        }


                        var alertRef =
                            GetOptionalString(
                                alert,
                                "alertRef")
                            ?? GetOptionalString(
                                alert,
                                "pluginid");


                        if (string.IsNullOrWhiteSpace(
                            alertRef))
                        {
                            throw new InvalidOperationException(
                                $"ZAP alert {alertIndex} does not contain an alert reference or plugin ID.");
                        }


                        var title =
                            GetOptionalString(
                                alert,
                                "name")
                            ?? GetOptionalString(
                                alert,
                                "alert");


                        if (string.IsNullOrWhiteSpace(
                            title))
                        {
                            throw new InvalidOperationException(
                                $"ZAP alert '{alertRef}' does not contain a name.");
                        }


                        var severity =
                            MapSeverity(
                                GetRequiredValueAsString(
                                    alert,
                                    "riskcode",
                                    alertRef));


                        var targetValue =
                            GetFirstInstanceUri(
                                alert)
                            ?? siteName;


                        var sourceIdentifier =
                        BuildSourceIdentifier(
                            alertRef,
                            siteName);


                        if (!usedIdentifiers.Add(
                            sourceIdentifier))
                        {
                            sourceIdentifier =
                                $"{sourceIdentifier}-{alertIndex:D3}";

                            usedIdentifiers.Add(
                                sourceIdentifier);
                        }


                        var description =
                            NormalizeHtmlText(
                                GetOptionalString(
                                    alert,
                                    "desc"));


                        if (string.IsNullOrWhiteSpace(
                            description))
                        {
                            description =
                                $"OWASP ZAP reported '{title}' on the target.";
                        }


                        var recommendation =
                            NormalizeHtmlText(
                                GetOptionalString(
                                    alert,
                                    "solution"));


                        if (string.IsNullOrWhiteSpace(
                            recommendation))
                        {
                            recommendation =
                                "Review the ZAP alert, validate the condition, and apply the appropriate remediation.";
                        }


                        var reference =
                            NormalizeHtmlText(
                                GetOptionalString(
                                    alert,
                                    "reference"));


                        if (string.IsNullOrWhiteSpace(
                            reference))
                        {
                            reference =
                                $"ZAP:{alertRef}";
                        }


                        findings.Add(
                            new NormalizedFinding
                            {
                                SourceFindingIdentifier =
                                    sourceIdentifier,

                                Title =
                                    title.Trim(),

                                SeverityCode =
                                    severity,

                                TargetValue =
                                    targetValue,

                                Description =
                                    description,

                                Evidence =
                                    BuildEvidence(
                                        siteName,
                                        alertRef,
                                        alert),

                                Recommendation =
                                    recommendation,

                                Reference =
                                    reference
                            });
                    }
                }


                return findings;
            }
        }


        private static string MapSeverity(
            string riskCode)
        {
            return riskCode.Trim() switch
            {
                "0" => "INFO",
                "1" => "LOW",
                "2" => "MEDIUM",
                "3" => "HIGH",

                _ => throw new InvalidOperationException(
                    $"ZAP returned unsupported risk code '{riskCode}'.")
            };
        }


        private static string? GetFirstInstanceUri(
            JsonElement alert)
        {
            if (!alert.TryGetProperty(
                    "instances",
                    out var instances) ||
                instances.ValueKind !=
                    JsonValueKind.Array)
            {
                return null;
            }


            foreach (var instance in
                instances.EnumerateArray())
            {
                if (instance.ValueKind !=
                    JsonValueKind.Object)
                {
                    continue;
                }


                var uri =
                    GetOptionalString(
                        instance,
                        "uri");


                if (!string.IsNullOrWhiteSpace(
                    uri))
                {
                    return uri.Trim();
                }
            }


            return null;
        }


        private static string BuildEvidence(
            string siteName,
            string alertRef,
            JsonElement alert)
        {
            var builder =
                new StringBuilder();


            AppendEvidence(
                builder,
                "ZAP Alert",
                alertRef);


            AppendEvidence(
                builder,
                "Plugin ID",
                GetOptionalString(
                    alert,
                    "pluginid"));


            AppendEvidence(
                builder,
                "Site",
                siteName);


            AppendEvidence(
                builder,
                "Risk",
                GetOptionalString(
                    alert,
                    "riskdesc"));


            AppendEvidence(
                builder,
                "Confidence",
                GetOptionalString(
                    alert,
                    "confidence"));


            AppendEvidence(
                builder,
                "Count",
                GetOptionalString(
                    alert,
                    "count"));


            AppendEvidence(
                builder,
                "CWE",
                GetOptionalString(
                    alert,
                    "cweid"));


            AppendEvidence(
                builder,
                "WASC",
                GetOptionalString(
                    alert,
                    "wascid"));


            /*
             * Preserve affected locations without copying
             * arbitrary response/header evidence into the
             * normalized Finding. Raw ZAP JSON remains stored
             * separately and may contain sensitive values.
             */
            if (alert.TryGetProperty(
                    "instances",
                    out var instances) &&
                instances.ValueKind ==
                    JsonValueKind.Array)
            {
                var instanceNumber =
                    0;


                foreach (var instance in
                    instances.EnumerateArray())
                {
                    if (instance.ValueKind !=
                        JsonValueKind.Object)
                    {
                        continue;
                    }


                    instanceNumber++;


                    /*
                     * Keep normalized evidence bounded.
                     * Full instance details remain available
                     * in the raw scanner output.
                     */
                    if (instanceNumber > 20)
                    {
                        builder.AppendLine(
                            "Additional instances omitted from normalized evidence.");

                        break;
                    }


                    var uri =
                        GetOptionalString(
                            instance,
                            "uri");


                    var method =
                        GetOptionalString(
                            instance,
                            "method");


                    var parameter =
                        GetOptionalString(
                            instance,
                            "param");


                    builder.Append(
                        $"Instance {instanceNumber}: ");


                    if (!string.IsNullOrWhiteSpace(
                        method))
                    {
                        builder.Append(
                            $"{method} ");
                    }


                    builder.Append(
                        uri ?? siteName);


                    if (!string.IsNullOrWhiteSpace(
                        parameter))
                    {
                        builder.Append(
                            $" | Parameter: {parameter}");
                    }


                    builder.AppendLine();
                }
            }


            return builder
                .ToString()
                .Trim();
        }


        private static void AppendEvidence(
            StringBuilder builder,
            string label,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return;
            }


            builder.Append(label);
            builder.Append(": ");
            builder.AppendLine(
                value.Trim());
        }


        private static string BuildSourceIdentifier(
     string alertRef,
     string site)
        {
            var normalizedReference =
                SanitizeIdentifierPart(
                    alertRef);


            var normalizedSite =
                site
                    .Trim()
                    .TrimEnd('/')
                    .ToLowerInvariant();


            var hashInput =
                $"{normalizedSite}|{alertRef}";


            var hashBytes =
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        hashInput));


            var shortHash =
                Convert.ToHexString(
                    hashBytes.AsSpan(
                        0,
                        6));


            return
                $"ZAP-{normalizedReference}-{shortHash}";
        }


        private static string SanitizeIdentifierPart(
            string value)
        {
            var builder =
                new StringBuilder();


            foreach (var character in
                value)
            {
                if (char.IsLetterOrDigit(
                        character) ||
                    character == '-' ||
                    character == '_')
                {
                    builder.Append(
                        character);
                }
                else
                {
                    builder.Append('-');
                }
            }


            var result =
                builder
                    .ToString()
                    .Trim('-');


            return string.IsNullOrWhiteSpace(
                result)
                ? "ALERT"
                : result;
        }


        private static string? GetOptionalString(
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
                    property.GetString(),

                JsonValueKind.Number =>
                    property.GetRawText(),

                JsonValueKind.True =>
                    "true",

                JsonValueKind.False =>
                    "false",

                _ =>
                    null
            };
        }


        private static string GetRequiredValueAsString(
            JsonElement element,
            string propertyName,
            string alertReference)
        {
            var value =
                GetOptionalString(
                    element,
                    propertyName);


            if (string.IsNullOrWhiteSpace(
                value))
            {
                throw new InvalidOperationException(
                    $"ZAP alert '{alertReference}' does not contain '{propertyName}'.");
            }


            return value;
        }


        private static string? NormalizeHtmlText(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return null;
            }


            /*
             * ZAP's traditional JSON report stores fields such
             * as desc, solution and reference as HTML fragments.
             */
            var withLineBreaks =
                Regex.Replace(
                    value,
                    @"</?(?:p|br|li|ul|ol)[^>]*>",
                    "\n",
                    RegexOptions.IgnoreCase);


            var withoutTags =
                Regex.Replace(
                    withLineBreaks,
                    "<[^>]+>",
                    " ");


            var decoded =
                WebUtility.HtmlDecode(
                    withoutTags);


            var normalizedLines =
                decoded
                    .Split(
                        '\n',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Select(x =>
                        Regex.Replace(
                            x,
                            @"\s+",
                            " "))
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(
                            x));


            var result =
                string.Join(
                    Environment.NewLine,
                    normalizedLines);


            return string.IsNullOrWhiteSpace(
                result)
                ? null
                : result;
        }
    }
}