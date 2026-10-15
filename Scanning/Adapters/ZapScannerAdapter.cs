using EVOLVE_VAPT.Scanning.Configuration;
using EVOLVE_VAPT.Scanning.Contracts;
using EVOLVE_VAPT.Scanning.Execution;
using Microsoft.Extensions.Options;
using System;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EVOLVE_VAPT.Scanning.Adapters
{
    public sealed class ZapScannerAdapter :
        IScannerAdapter
    {
        private readonly IExternalProcessRunner
            _processRunner;

        private readonly ZapExecutionOptions
            _options;

        private readonly ILogger<ZapScannerAdapter>
            _logger;


        public ZapScannerAdapter(
            IExternalProcessRunner processRunner,
            IOptions<ZapExecutionOptions> options,
            ILogger<ZapScannerAdapter> logger)
        {
            _processRunner =
                processRunner;

            _options =
                options.Value;

            _logger =
                logger;
        }


        public string ScannerTypeCode =>
            "ZAP";


        private static Uri ValidateTarget(
            ScannerExecutionContext context)
        {
            if (!string.Equals(
                context.TargetTypeCode,
                "URL",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"ZAP supports URL targets only. Target type '{context.TargetTypeCode}' is not supported.");
            }


            if (!Uri.TryCreate(
                    context.TargetValue?.Trim(),
                    UriKind.Absolute,
                    out var uri))
            {
                throw new InvalidOperationException(
                    "The ZAP target is not a valid absolute URL.");
            }


            if (!string.Equals(
                    uri.Scheme,
                    Uri.UriSchemeHttp,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    uri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "ZAP targets must use HTTP or HTTPS.");
            }


            if (!string.IsNullOrWhiteSpace(
                uri.UserInfo))
            {
                throw new InvalidOperationException(
                    "Credentials must not be embedded in the ZAP target URL.");
            }


            return uri;
        }


        private static List<ScannerScopeRuleSnapshot>
            ParseScopeRules(
                string? json)
        {
            if (string.IsNullOrWhiteSpace(
                json))
            {
                return new();
            }


            try
            {
                return JsonSerializer.Deserialize<
                           List<ScannerScopeRuleSnapshot>>(
                           json)
                       ?? new();
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    "The ZAP scope snapshot contains invalid JSON.",
                    ex);
            }
        }


        private static bool SameOrigin(
            Uri left,
            Uri right)
        {
            return string.Equals(
                       left.Scheme,
                       right.Scheme,
                       StringComparison.OrdinalIgnoreCase)
                   &&
                   string.Equals(
                       left.Host,
                       right.Host,
                       StringComparison.OrdinalIgnoreCase)
                   &&
                   left.Port ==
                       right.Port;
        }


        /*
         * Convert an approved scope value into a ZAP regex.
         *
         * Scope rules are treated as URL/path prefixes,
         * not arbitrary regular expressions.
         */
        private static string BuildScopeRegex(
            Uri target,
            string ruleValue)
        {
            var value =
                ruleValue.Trim();


            if (string.IsNullOrWhiteSpace(
                value))
            {
                throw new InvalidOperationException(
                    "ZAP scope rules cannot be empty.");
            }


            Uri scopeUri;


            if (Uri.TryCreate(
                    value,
                    UriKind.Absolute,
                    out var absoluteUri))
            {
                if (!string.Equals(
                        absoluteUri.Scheme,
                        Uri.UriSchemeHttp,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        absoluteUri.Scheme,
                        Uri.UriSchemeHttps,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"ZAP scope rule '{value}' must use HTTP or HTTPS.");
                }


                if (!SameOrigin(
                    target,
                    absoluteUri))
                {
                    throw new InvalidOperationException(
                        $"ZAP scope rule '{value}' is outside the approved target origin.");
                }


                scopeUri =
                    absoluteUri;
            }
            else
            {
                if (!value.StartsWith(
                 "/",
                 StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"ZAP scope rule '{value}' must be an absolute same-origin URL or a path beginning with '/'.");
                }


                scopeUri =
                    new Uri(
                        target,
                        value);
            }


            /*
             * Escape the approved value before making it a
             * prefix regex. The RuleValue itself cannot inject
             * arbitrary ZAP regular-expression syntax.
             */
            return
                "^"
                + Regex.Escape(
                    scopeUri.GetLeftPart(
                        UriPartial.Path)
                    .TrimEnd('/'))
                + "(?:/.*)?(?:\\?.*)?$";
        }


        private static string ToYamlScalar(
            string value)
        {
            /*
             * YAML single-quoted values escape a single quote
             * by doubling it.
             */
            return
                "'"
                + value.Replace(
                    "'",
                    "''",
                    StringComparison.Ordinal)
                + "'";
        }


        private string BuildAutomationPlan(
            Uri target,
            IReadOnlyList<ScannerScopeRuleSnapshot> scopeRules,
            string reportDirectory,
            string reportFileName)
        {
            var includePaths =
                new List<string>();

            var excludePaths =
                new List<string>();


            foreach (var rule in scopeRules)
            {
                var type =
                    rule.RuleTypeCode
                        ?.Trim()
                        .ToUpperInvariant();


                var regex =
                    BuildScopeRegex(
                        target,
                        rule.RuleValue);


                switch (type)
                {
                    case "INCLUDE":

                        includePaths.Add(
                            regex);

                        break;


                    case "EXCLUDE":

                        excludePaths.Add(
                            regex);

                        break;


                    default:

                        throw new InvalidOperationException(
                            $"ZAP does not support scope rule type '{rule.RuleTypeCode}'.");
                }
            }


            var yaml =
                new StringBuilder();


            yaml.AppendLine("---");

            yaml.AppendLine("env:");
            yaml.AppendLine("  contexts:");
            yaml.AppendLine("  - name: 'EVOLVE ZAP Passive Scan'");

            yaml.AppendLine("    urls:");
            yaml.Append("    - ");
            yaml.AppendLine(
                ToYamlScalar(
                    target.AbsoluteUri));


            if (includePaths.Count > 0)
            {
                yaml.AppendLine("    includePaths:");

                foreach (var include in
                    includePaths)
                {
                    yaml.Append("    - ");
                    yaml.AppendLine(
                        ToYamlScalar(
                            include));
                }
            }


            if (excludePaths.Count == 0)
            {
                yaml.AppendLine(
                    "    excludePaths: []");
            }
            else
            {
                yaml.AppendLine(
                    "    excludePaths:");

                foreach (var exclude in
                    excludePaths)
                {
                    yaml.Append("    - ");

                    yaml.AppendLine(
                        ToYamlScalar(
                            exclude));
                }
            }
           


            yaml.AppendLine();
            yaml.AppendLine("  parameters:");
            yaml.AppendLine("    failOnError: true");
            yaml.AppendLine("    failOnWarning: false");
            yaml.AppendLine("    continueOnFailure: false");
            yaml.AppendLine("    progressToStdout: true");


            yaml.AppendLine();
            yaml.AppendLine("jobs:");

            yaml.AppendLine("- type: passiveScan-config");
            yaml.AppendLine("  parameters:");
            yaml.AppendLine("    maxAlertsPerRule: 20");
            yaml.AppendLine("    scanOnlyInScope: true");
            yaml.AppendLine(
                "    maxBodySizeInBytesToScan: 1048576");
            yaml.AppendLine("    enableTags: false");


            yaml.AppendLine();
            yaml.AppendLine("- type: spider");
            yaml.AppendLine("  parameters:");
            yaml.AppendLine(
                "    context: 'EVOLVE ZAP Passive Scan'");

            yaml.Append("    url: ");
            yaml.AppendLine(
                ToYamlScalar(
                    target.AbsoluteUri));

            yaml.Append("    maxDuration: ");
            yaml.AppendLine(
                _options.MaxSpiderMinutes
                    .ToString());

            yaml.AppendLine("    maxDepth: 5");
            yaml.AppendLine("    maxChildren: 50");
            yaml.AppendLine("    threadCount: 2");
            yaml.AppendLine("    acceptCookies: true");
            yaml.AppendLine("    parseComments: false");
            yaml.AppendLine("    parseGit: false");
            yaml.AppendLine("    parseDsStore: false");
            yaml.AppendLine("    parseRobotsTxt: true");
            yaml.AppendLine("    parseSitemapXml: true");
            yaml.AppendLine("    parseSVNEntries: false");

            /*
             * Passive MVP must not submit application forms.
             */
            yaml.AppendLine("    processForm: false");
            yaml.AppendLine("    postForm: false");


            yaml.AppendLine();
            yaml.AppendLine("- type: passiveScan-wait");
            yaml.AppendLine("  parameters:");
            yaml.AppendLine("    maxDuration: 5");


            yaml.AppendLine();
            yaml.AppendLine("- type: report");
            yaml.AppendLine("  parameters:");
            yaml.AppendLine(
                "    template: 'traditional-json'");

            yaml.Append("    reportDir: ");
            yaml.AppendLine(
                ToYamlScalar(
                    reportDirectory
                        .Replace(
                            '\\',
                            '/')));

            yaml.Append("    reportFile: ");
            yaml.AppendLine(
                ToYamlScalar(
                    reportFileName));

            yaml.AppendLine(
                "    reportTitle: 'EVOLVE VAPT ZAP Passive Scan'");

            yaml.AppendLine(
                "    reportDescription: 'Controlled passive web assessment generated by EVOLVE VAPT.'");

            yaml.AppendLine(
                "    displayReport: false");

            yaml.AppendLine("  risks:");
            yaml.AppendLine("  - high");
            yaml.AppendLine("  - medium");
            yaml.AppendLine("  - low");
            yaml.AppendLine("  - info");

            yaml.AppendLine("  confidences:");
            yaml.AppendLine("  - high");
            yaml.AppendLine("  - medium");
            yaml.AppendLine("  - low");
            yaml.AppendLine("  - falsepositive");


            return yaml.ToString();
        }


        private static string? TryGetZapVersion(
            string rawReport)
        {
            try
            {
                using var document =
                    JsonDocument.Parse(
                        rawReport);


                if (document.RootElement
                    .TryGetProperty(
                        "@version",
                        out var versionElement))
                {
                    return versionElement
                        .GetString();
                }
            }
            catch (JsonException)
            {
                // The caller will reject malformed JSON.
            }


            return null;
        }


        private static bool IsValidJson(
            string value)
        {
            try
            {
                using var document =
                    JsonDocument.Parse(
                        value);

                return document.RootElement
                           .ValueKind ==
                       JsonValueKind.Object;
            }
            catch (JsonException)
            {
                return false;
            }
        }


        private static string LimitErrorMessage(
            string? value,
            int maxLength = 2000)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return string.Empty;
            }


            var trimmed =
                value.Trim();


            return trimmed.Length <=
                   maxLength
                ? trimmed
                : trimmed[..maxLength]
                  + "...";
        }


        public async Task<ScannerExecutionResult>
            ExecuteAsync(
                ScannerExecutionContext context,
                CancellationToken cancellationToken)
        {
            string? temporaryDirectory =
                null;


            try
            {
                var target =
                    ValidateTarget(
                        context);


                var scopeRules =
                    ParseScopeRules(
                        context.ScopeSnapshotJson);


                temporaryDirectory =
                    Path.Combine(
                        Path.GetTempPath(),
                        "EVOLVE_VAPT",
                        "ZAP",
                        $"Scan-{context.ScanJobID}-{Guid.NewGuid():N}");


                Directory.CreateDirectory(
                    temporaryDirectory);


                var planPath =
                    Path.Combine(
                        temporaryDirectory,
                        "plan.yaml");


                var reportFileName =
                    "zap-report.json";


                var reportPath =
                    Path.Combine(
                        temporaryDirectory,
                        reportFileName);


                var plan =
                    BuildAutomationPlan(
                        target,
                        scopeRules,
                        temporaryDirectory,
                        reportFileName);


                await File.WriteAllTextAsync(
                    planPath,
                    plan,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false),
                    cancellationToken);


                var arguments =
                    new List<string>
                    {
                        $"-Xmx{_options.MaxHeapMegabytes}m",
                        "-jar",
                        _options.JarPath,
                        "-cmd",
                        "-silent",
                        "-autorun",
                        planPath
                    };


                _logger.LogInformation(
                    "Starting ZAP passive scan for Scan Job {ScanJobId}.",
                    context.ScanJobID);


                var processResult =
                    await _processRunner.RunAsync(
                        new ExternalProcessRequest
                        {
                            ExecutablePath =
                                _options.JavaExecutablePath,

                            WorkingDirectory =
                                _options.InstallationDirectory,

                            Arguments =
                                arguments,

                            Timeout =
                                TimeSpan.FromMinutes(
                                    context.TimeoutMinutes),

                            MaxStandardOutputCharacters =
                                _options.MaxStandardOutputCharacters,

                            MaxStandardErrorCharacters =
                                _options.MaxStandardErrorCharacters
                        },
                        cancellationToken);


                if (processResult.Cancelled &&
                    cancellationToken
                        .IsCancellationRequested)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                }


                if (!processResult.Started)
                {
                    return ScannerExecutionResult.Failed(
                        $"ZAP could not be started: {processResult.StartError}");
                }


                if (processResult.TimedOut)
                {
                    return ScannerExecutionResult.Failed(
                        errorMessage:
                            "ZAP execution exceeded the configured timeout.",

                        exitCode:
                            processResult.ExitCode,

                        scannerVersion:
                            "Unknown",

                        outputFormat:
                            "JSON");
                }


                if (!File.Exists(
                    reportPath))
                {
                    var error =
                        LimitErrorMessage(
                            processResult.StandardError);


                    var console =
                        LimitErrorMessage(
                            processResult.StandardOutput);


                    return ScannerExecutionResult.Failed(
                        errorMessage:
                            !string.IsNullOrWhiteSpace(
                                error)
                                ? error
                                : !string.IsNullOrWhiteSpace(
                                    console)
                                    ? $"ZAP did not generate a report. {console}"
                                    : "ZAP did not generate a report.",

                        exitCode:
                            processResult.ExitCode,

                        scannerVersion:
                            "Unknown",

                        outputFormat:
                            "JSON");
                }


                var reportJson =
                    await File.ReadAllTextAsync(
                        reportPath,
                        cancellationToken);


                if (string.IsNullOrWhiteSpace(
                    reportJson) ||
                    !IsValidJson(
                        reportJson))
                {
                    return ScannerExecutionResult.Failed(
                        errorMessage:
                            "ZAP generated an empty or invalid JSON report.",

                        exitCode:
                            processResult.ExitCode,

                        scannerVersion:
                            "Unknown",

                        outputFormat:
                            "JSON",

                        rawOutput:
                            reportJson);
                }


                var zapVersion =
                    TryGetZapVersion(
                        reportJson)
                    ?? "Unknown";


                /*
                 * Automation Framework:
                 * 0 = completed cleanly
                 * 2 = completed with warning(s)
                 *
                 * A valid report is still usable when ZAP
                 * exits with warning status 2.
                 */
                if (processResult.ExitCode != 0 &&
                    processResult.ExitCode != 2)
                {
                    var error =
                        LimitErrorMessage(
                            processResult.StandardError);


                    return ScannerExecutionResult.Failed(
                        errorMessage:
                            string.IsNullOrWhiteSpace(
                                error)
                                ? $"ZAP exited with code {processResult.ExitCode}."
                                : error,

                        exitCode:
                            processResult.ExitCode,

                        scannerVersion:
                            zapVersion,

                        outputFormat:
                            "JSON",

                        rawOutput:
                            reportJson);
                }


                if (processResult.ExitCode == 2)
                {
                    _logger.LogWarning(
                        "ZAP completed Scan Job {ScanJobId} with Automation Framework warning status 2.",
                        context.ScanJobID);
                }


                if (processResult
                    .StandardOutputTruncated)
                {
                    _logger.LogWarning(
                        "ZAP stdout was truncated for Scan Job {ScanJobId}.",
                        context.ScanJobID);
                }


                if (processResult
                    .StandardErrorTruncated)
                {
                    _logger.LogWarning(
                        "ZAP stderr was truncated for Scan Job {ScanJobId}.",
                        context.ScanJobID);
                }


                _logger.LogInformation(
                    "ZAP completed Scan Job {ScanJobId}. Version={ZapVersion}, ExitCode={ExitCode}, DurationMs={DurationMs}.",
                    context.ScanJobID,
                    zapVersion,
                    processResult.ExitCode,
                    processResult.Duration
                        .TotalMilliseconds);


                return ScannerExecutionResult.Completed(
                    exitCode:
                        processResult.ExitCode ?? 0,

                    scannerVersion:
                        zapVersion,

                    outputFormat:
                        "JSON",

                    rawOutput:
                        reportJson);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
                when (
                    ex is ArgumentException ||
                    ex is FileNotFoundException ||
                    ex is DirectoryNotFoundException ||
                    ex is InvalidOperationException ||
                    ex is JsonException)
            {
                _logger.LogError(
                    ex,
                    "ZAP execution configuration or validation failed for Scan Job {ScanJobId}.",
                    context.ScanJobID);


                return ScannerExecutionResult.Failed(
                    $"ZAP execution failed validation: {ex.Message}");
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(
                        temporaryDirectory) &&
                    Directory.Exists(
                        temporaryDirectory))
                {
                    try
                    {
                        Directory.Delete(
                            temporaryDirectory,
                            recursive: true);
                    }
                    catch (Exception ex)
                    {
                        /*
                         * Temporary cleanup failure must not
                         * replace the scanner result.
                         */
                        _logger.LogWarning(
                            ex,
                            "Unable to delete temporary ZAP directory for Scan Job {ScanJobId}.",
                            context.ScanJobID);
                    }
                }
            }
        }
    }
}