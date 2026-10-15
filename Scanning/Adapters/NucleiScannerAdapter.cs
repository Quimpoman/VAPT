using EVOLVE_VAPT.Scanning.Configuration;
using EVOLVE_VAPT.Scanning.Contracts;
using EVOLVE_VAPT.Scanning.Execution;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EVOLVE_VAPT.Scanning.Adapters
{
    public sealed class NucleiScannerAdapter :
        IScannerAdapter
    {
        private readonly IExternalProcessRunner
            _processRunner;

        private readonly NucleiExecutionOptions
            _options;

        private readonly ILogger<NucleiScannerAdapter>
            _logger;


        public NucleiScannerAdapter(
            IExternalProcessRunner processRunner,
            IOptions<NucleiExecutionOptions> options,
            ILogger<NucleiScannerAdapter> logger)
        {
            _processRunner =
                processRunner;

            _options =
                options.Value;

            _logger =
                logger;
        }


        public string ScannerTypeCode =>
            "NUCLEI";
        private static List<ScannerOptionSnapshot>
    ParseOptions(
        string? json)
        {
            if (string.IsNullOrWhiteSpace(
                json))
            {
                return new();
            }


            return JsonSerializer.Deserialize<
                       List<ScannerOptionSnapshot>>(
                       json,
                       new JsonSerializerOptions
                       {
                           PropertyNameCaseInsensitive =
                               true
                       })
                   ?? new();
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


            return JsonSerializer.Deserialize<
                       List<ScannerScopeRuleSnapshot>>(
                       json,
                       new JsonSerializerOptions
                       {
                           PropertyNameCaseInsensitive =
                               true
                       })
                   ?? new();
        }
        private static int GetIntOption(
    IEnumerable<ScannerOptionSnapshot> options,
    string optionCode,
    int defaultValue)
        {
            var option =
                options.FirstOrDefault(x =>
                    string.Equals(
                        x.OptionCode,
                        optionCode,
                        StringComparison.OrdinalIgnoreCase));


            if (option == null ||
                string.IsNullOrWhiteSpace(
                    option.OptionValue))
            {
                return defaultValue;
            }


            return int.TryParse(
                option.OptionValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value)
                    ? value
                    : defaultValue;
        }


        private static string GetStringOption(
            IEnumerable<ScannerOptionSnapshot> options,
            string optionCode,
            string defaultValue)
        {
            var option =
                options.FirstOrDefault(x =>
                    string.Equals(
                        x.OptionCode,
                        optionCode,
                        StringComparison.OrdinalIgnoreCase));


            return string.IsNullOrWhiteSpace(
                option?.OptionValue)
                    ? defaultValue
                    : option.OptionValue.Trim();
        }
        private static string ValidateAndNormalizeTarget(
    ScannerExecutionContext context)
        {
            if (!string.Equals(
                context.TargetTypeCode,
                "URL",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Target type '{context.TargetTypeCode}' is not supported by the initial Nuclei HTTP adapter.");
            }


            var target =
                context.TargetValue.Trim();


            if (!Uri.TryCreate(
                    target,
                    UriKind.Absolute,
                    out var uri))
            {
                throw new InvalidOperationException(
                    "Nuclei target is not a valid absolute URL.");
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
                    "Nuclei HTTP targets must use HTTP or HTTPS.");
            }


            if (string.IsNullOrWhiteSpace(
                uri.Host))
            {
                throw new InvalidOperationException(
                    "Nuclei target does not contain a valid host.");
            }


            // Do not permit credentials embedded directly in target URLs.
            if (!string.IsNullOrWhiteSpace(
                uri.UserInfo))
            {
                throw new InvalidOperationException(
                    "Nuclei target URLs cannot contain embedded credentials.");
            }


            if (!string.IsNullOrWhiteSpace(
                uri.Fragment))
            {
                throw new InvalidOperationException(
                    "Nuclei target URLs cannot contain URL fragments.");
            }


            return uri.AbsoluteUri;
        }
        private static void ValidateScope(
    string normalizedTarget,
    IReadOnlyList<ScannerScopeRuleSnapshot> scopeRules)
        {
            var includeRules =
                scopeRules
                    .Where(x =>
                        string.Equals(
                            x.RuleTypeCode,
                            "INCLUDE",
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();


            if (includeRules.Count != 1)
            {
                throw new InvalidOperationException(
                    "Nuclei currently requires exactly one INCLUDE scope rule.");
            }


            if (!Uri.TryCreate(
                    includeRules[0].RuleValue.Trim(),
                    UriKind.Absolute,
                    out var includeUri))
            {
                throw new InvalidOperationException(
                    "Nuclei INCLUDE scope rule is not a valid absolute URL.");
            }


            var normalizedInclude =
                includeUri.AbsoluteUri;


            if (!string.Equals(
                normalizedTarget,
                normalizedInclude,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Nuclei target '{normalizedTarget}' does not exactly match its approved INCLUDE scope.");
            }


            var excludeRules =
                scopeRules.Where(x =>
                    string.Equals(
                        x.RuleTypeCode,
                        "EXCLUDE",
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();


            /*
             * URL path/exclusion semantics require more careful handling.
             * Fail closed until we intentionally implement them.
             */
            if (excludeRules.Count > 0)
            {
                throw new InvalidOperationException(
                    "The initial Nuclei adapter does not yet support EXCLUDE scope rules.");
            }
        }
        private static string ValidateSeverities(
    string value)
        {
            var allowed =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase)
                {
            "info",
            "low",
            "medium",
            "high",
            "critical"
                };


            var severities =
                value.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);


            if (severities.Length == 0)
            {
                throw new InvalidOperationException(
                    "At least one Nuclei severity must be selected.");
            }


            foreach (var severity in severities)
            {
                if (!allowed.Contains(
                    severity))
                {
                    throw new InvalidOperationException(
                        $"Unsupported Nuclei severity '{severity}'.");
                }
            }


            return string.Join(
                ",",
                severities.Select(x =>
                    x.ToLowerInvariant()));
        }
        private List<string> BuildArguments(
    string target,
    string severities,
    int rateLimit,
    int templateConcurrency)
        {
            var arguments =
                new List<string>
                {
            "-u",
            target,

            "-t",
            _options.TemplatesPath,

            /*
             * Initial integration runs HTTP protocol
             * templates only.
             */
            "-pt",
            "http",

            "-severity",
            severities,

            "-rl",
            rateLimit.ToString(
                CultureInfo.InvariantCulture),

            "-c",
            templateConcurrency.ToString(
                CultureInfo.InvariantCulture),

            "-timeout",
            _options.RequestTimeoutSeconds.ToString(
                CultureInfo.InvariantCulture),

            "-retries",
            _options.Retries.ToString(
                CultureInfo.InvariantCulture),

            /*
             * Machine-readable findings.
             */
            "-jsonl",

            "-silent",
            "-nc",

            /*
             * Don't include potentially huge/sensitive HTTP
             * request and response bodies in the MVP JSONL.
             */
            "-omit-raw",

            "-omit-template",

            /*
             * Do not permit OAST/Interactsh callbacks yet.
             */
            "-ni",

            /*
             * Do not follow redirects outside the exact
             * approved target.
             */
            "-dr",

            /*
             * Ignore stdin entirely.
             */
            "-no-stdin",

            /*
             * Scan jobs must never modify scanner software
             * or templates automatically.
             */
            "-duc",

            /*
             * Only allow signed templates for this MVP.
             */
            "-dut",

            /*
             * Defense in depth. Several of these are already
             * excluded by the official templates defaults.
             */
            "-etags",
            "dos,fuzz,bruteforce,local,txt-service,intrusive"
                };


            return arguments;
        }
        private async Task<string> GetNucleiVersionAsync(
    CancellationToken cancellationToken)
        {
            var result =
                await _processRunner.RunAsync(
                    new ExternalProcessRequest
                    {
                        ExecutablePath =
                            _options.ExecutablePath,

                        Arguments =
                            new[]
                            {
                        "-version",
                        "-nc",
                        "-duc"
                            },

                        Timeout =
                            TimeSpan.FromSeconds(15),

                        MaxStandardOutputCharacters =
                            10_000,

                        MaxStandardErrorCharacters =
                            10_000
                    },
                    cancellationToken);


            if (!result.Started ||
                result.TimedOut)
            {
                return "Unknown";
            }


            var combined =
                string.Join(
                    Environment.NewLine,
                    result.StandardOutput,
                    result.StandardError);


            /*
             * Handles typical forms such as:
             * v3.4.10
             * 3.4.10
             */
            var match =
                Regex.Match(
                    combined,
                    @"v?\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.\-]+)?");


            if (match.Success)
            {
                return match.Value
                    .TrimStart('v', 'V');
            }


            return "Unknown";
        }
        public async Task<ScannerExecutionResult> ExecuteAsync(
    ScannerExecutionContext context,
    CancellationToken cancellationToken)
        {
            try
            {
                var target =
                    ValidateAndNormalizeTarget(
                        context);


                var scopeRules =
                    ParseScopeRules(
                        context.ScopeSnapshotJson);


                ValidateScope(
                    target,
                    scopeRules);


                if (!Directory.Exists(
                    _options.TemplatesPath))
                {
                    return ScannerExecutionResult.Failed(
                        $"Configured Nuclei templates directory '{_options.TemplatesPath}' does not exist.");
                }


                var profileOptions =
                    ParseOptions(
                        context.ProfileOptionsSnapshotJson);


                var rateLimit =
                    GetIntOption(
                        profileOptions,
                        "NUCLEI_RATE_LIMIT",
                        _options.MaxRequestsPerSecond);


                if (rateLimit < 1 ||
                    rateLimit >
                        _options.MaxRequestsPerSecond)
                {
                    return ScannerExecutionResult.Failed(
                        $"Nuclei rate limit must be between 1 and {_options.MaxRequestsPerSecond} requests per second.");
                }


                /*
                 * Reuse the Scan Profile's immutable concurrency
                 * snapshot as Nuclei's template concurrency limit.
                 */
                var templateConcurrency =
                    context.MaxConcurrency;


                if (templateConcurrency < 1 ||
                    templateConcurrency >
                        _options.MaxTemplateConcurrency)
                {
                    return ScannerExecutionResult.Failed(
                        $"Nuclei concurrency must be between 1 and {_options.MaxTemplateConcurrency}.");
                }


                var severities =
                    ValidateSeverities(
                        GetStringOption(
                            profileOptions,
                            "NUCLEI_SEVERITIES",
                            "info,low,medium,high,critical"));


                var nucleiVersion =
                    await GetNucleiVersionAsync(
                        cancellationToken);


                var arguments =
                    BuildArguments(
                        target,
                        severities,
                        rateLimit,
                        templateConcurrency);


                _logger.LogInformation(
                    "Starting Nuclei for Scan Job {ScanJobId}. Target={Target}.",
                    context.ScanJobID,
                    target);


                var processResult =
                    await _processRunner.RunAsync(
                        new ExternalProcessRequest
                        {
                            ExecutablePath =
                                _options.ExecutablePath,

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
                    cancellationToken.IsCancellationRequested)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                }


                if (!processResult.Started)
                {
                    return ScannerExecutionResult.Failed(
                        $"Nuclei could not be started: {processResult.StartError}");
                }


                if (processResult.TimedOut)
                {
                    return ScannerExecutionResult.Failed(
                        errorMessage:
                            "Nuclei execution exceeded the configured timeout.",

                        exitCode:
                            processResult.ExitCode,

                        scannerVersion:
                            nucleiVersion,

                        outputFormat:
                            "JSONL",

                        rawOutput:
                            processResult.StandardOutput);
                }


                if (processResult.StandardOutputTruncated)
                {
                    return ScannerExecutionResult.Failed(
                        errorMessage:
                            "Nuclei JSONL output exceeded the configured output limit and was truncated.",

                        exitCode:
                            processResult.ExitCode,

                        scannerVersion:
                            nucleiVersion,

                        outputFormat:
                            "JSONL",

                        rawOutput:
                            processResult.StandardOutput);
                }


                if (processResult.ExitCode != 0)
                {
                    var error =
                        string.IsNullOrWhiteSpace(
                            processResult.StandardError)
                            ? $"Nuclei exited with code {processResult.ExitCode}."
                            : processResult.StandardError.Trim();


                    if (error.Length > 2000)
                    {
                        error =
                            error[..2000] + "...";
                    }


                    return ScannerExecutionResult.Failed(
                        errorMessage:
                            error,

                        exitCode:
                            processResult.ExitCode,

                        scannerVersion:
                            nucleiVersion,

                        outputFormat:
                            "JSONL",

                        rawOutput:
                            processResult.StandardOutput);
                }


                _logger.LogInformation(
                    "Nuclei completed successfully for Scan Job {ScanJobId}. Version={Version}, DurationMs={DurationMs}.",
                    context.ScanJobID,
                    nucleiVersion,
                    processResult.Duration.TotalMilliseconds);


                /*
                 * Empty stdout is valid. It means Nuclei executed
                 * successfully but no templates matched.
                 */
                return ScannerExecutionResult.Completed(
                    exitCode:
                        processResult.ExitCode ?? 0,

                    scannerVersion:
                        nucleiVersion,

                    outputFormat:
                        "JSONL",

                    rawOutput:
                        processResult.StandardOutput);
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
                    "Nuclei validation/configuration failed for Scan Job {ScanJobId}.",
                    context.ScanJobID);


                return ScannerExecutionResult.Failed(
                    $"Nuclei execution failed validation: {ex.Message}");
            }
        }
    }
}
