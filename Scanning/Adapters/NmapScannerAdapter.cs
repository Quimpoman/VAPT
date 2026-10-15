using EVOLVE_VAPT.Scanning.Configuration;
using EVOLVE_VAPT.Scanning.Contracts;
using EVOLVE_VAPT.Scanning.Execution;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Xml;

namespace EVOLVE_VAPT.Scanning.Adapters
{
    public sealed class NmapScannerAdapter
        : IScannerAdapter
    {
        private readonly IExternalProcessRunner
            _processRunner;

        private readonly NmapExecutionOptions
            _options;

        private readonly ILogger<NmapScannerAdapter>
            _logger;


        public NmapScannerAdapter(
            IExternalProcessRunner processRunner,
            IOptions<NmapExecutionOptions> options,
            ILogger<NmapScannerAdapter> logger)
        {
            _processRunner =
                processRunner;

            _options =
                options.Value;

            _logger =
                logger;
        }


        public string ScannerTypeCode =>
            "NMAP";
        private static List<ScannerOptionSnapshot> ParseOptions(
        string? json)
        {
            if (string.IsNullOrWhiteSpace(
                json))
            {
                return new();
            }


            return JsonSerializer.Deserialize<
                       List<ScannerOptionSnapshot>>(
                       json)
                   ?? new();
        }
        private static List<ScannerScopeRuleSnapshot> ParseScopeRules(
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

        private string NormalizeIncludeRuleValue(
    string targetTypeCode,
    string ruleValue)
        {
            var value =
                ruleValue.Trim();


            if (string.IsNullOrWhiteSpace(
                value))
            {
                throw new InvalidOperationException(
                    "Nmap INCLUDE scope rule cannot be empty.");
            }


            switch (
                targetTypeCode.ToUpperInvariant())
            {
                case "DOMAIN":

                    if (Uri.CheckHostName(value) !=
                        UriHostNameType.Dns)
                    {
                        throw new InvalidOperationException(
                            $"Nmap INCLUDE scope rule '{value}' is not a valid domain.");
                    }

                    return value
                        .TrimEnd('.')
                        .ToLowerInvariant();


                case "IPV4":

                    if (!IPAddress.TryParse(
                            value,
                            out var ipv4) ||
                        ipv4.AddressFamily !=
                            AddressFamily.InterNetwork)
                    {
                        throw new InvalidOperationException(
                            $"Nmap INCLUDE scope rule '{value}' is not a valid IPv4 address.");
                    }

                    return ipv4.ToString();


                case "IPV6":

                    if (!IPAddress.TryParse(
                            value,
                            out var ipv6) ||
                        ipv6.AddressFamily !=
                            AddressFamily.InterNetworkV6)
                    {
                        throw new InvalidOperationException(
                            $"Nmap INCLUDE scope rule '{value}' is not a valid IPv6 address.");
                    }

                    return ipv6.ToString();


                case "CIDR":

                    return ValidateCidrTarget(
                        value);


                default:

                    throw new InvalidOperationException(
                        $"Nmap does not support scope validation for target type '{targetTypeCode}'.");
            }
        }

        private void ValidateIncludeScope(
    ScannerExecutionContext context,
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


            if (includeRules.Count == 0)
            {
                throw new InvalidOperationException(
                    "Nmap execution requires an INCLUDE scope rule.");
            }


            /*
             * First Nmap MVP intentionally supports one exact
             * INCLUDE boundary. More complex range combinations
             * can be added later without weakening scope safety.
             */
            if (includeRules.Count != 1)
            {
                throw new InvalidOperationException(
                    "Nmap currently requires exactly one INCLUDE scope rule matching the target.");
            }


            var normalizedInclude =
                NormalizeIncludeRuleValue(
                    context.TargetTypeCode,
                    includeRules[0].RuleValue);


            var targetForComparison =
                context.TargetTypeCode
                    .Equals(
                        "DOMAIN",
                        StringComparison.OrdinalIgnoreCase)
                    ? normalizedTarget
                        .TrimEnd('.')
                        .ToLowerInvariant()
                    : normalizedTarget;


            if (!string.Equals(
                normalizedInclude,
                targetForComparison,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Nmap target '{normalizedTarget}' does not exactly match its approved INCLUDE scope '{includeRules[0].RuleValue}'.");
            }
        }

        private List<string> GetNmapExclusions(
    ScannerExecutionContext context,
    string normalizedTarget,
    IReadOnlyList<ScannerScopeRuleSnapshot> scopeRules)
        {
            var excludeRules =
                scopeRules
                    .Where(x =>
                        string.Equals(
                            x.RuleTypeCode,
                            "EXCLUDE",
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();


            if (excludeRules.Count == 0)
            {
                return new();
            }


            /*
             * For the initial implementation, exclusions are
             * supported only when scanning a CIDR range.
             */
            if (!string.Equals(
                context.TargetTypeCode,
                "CIDR",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Nmap EXCLUDE scope rules are currently supported only for CIDR targets.");
            }


            var exclusions =
                new List<string>();


            foreach (var rule in excludeRules)
            {
                var exclusion =
                    ValidateCidrExclusion(
                        normalizedTarget,
                        rule.RuleValue);


                exclusions.Add(
                    exclusion);
            }


            return exclusions;
        }
        private static string ValidateCidrExclusion(
    string targetCidr,
    string ruleValue)
        {
            var targetParts =
                targetCidr.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);


            if (targetParts.Length != 2 ||
                !IPAddress.TryParse(
                    targetParts[0],
                    out var targetAddress) ||
                !int.TryParse(
                    targetParts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var targetPrefix))
            {
                throw new InvalidOperationException(
                    "The normalized Nmap CIDR target is invalid.");
            }


            var exclusionValue =
                ruleValue.Trim();


            if (string.IsNullOrWhiteSpace(
                exclusionValue))
            {
                throw new InvalidOperationException(
                    "Nmap EXCLUDE scope rule cannot be empty.");
            }


            /*
             * Single-host exclusion.
             */
            if (IPAddress.TryParse(
                exclusionValue,
                out var excludedAddress))
            {
                if (excludedAddress.AddressFamily !=
                    targetAddress.AddressFamily)
                {
                    throw new InvalidOperationException(
                        $"EXCLUDE rule '{exclusionValue}' uses a different address family from the target.");
                }


                if (!IsAddressInNetwork(
                    excludedAddress,
                    targetAddress,
                    targetPrefix))
                {
                    throw new InvalidOperationException(
                        $"EXCLUDE rule '{exclusionValue}' is outside the approved target CIDR.");
                }


                return excludedAddress.ToString();
            }


            /*
             * CIDR exclusion.
             */
            var exclusionParts =
                exclusionValue.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);


            if (exclusionParts.Length != 2 ||
                !IPAddress.TryParse(
                    exclusionParts[0],
                    out var excludedNetwork) ||
                !int.TryParse(
                    exclusionParts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var excludedPrefix))
            {
                throw new InvalidOperationException(
                    $"EXCLUDE rule '{exclusionValue}' is not a valid IP address or CIDR.");
            }


            if (excludedNetwork.AddressFamily !=
                targetAddress.AddressFamily)
            {
                throw new InvalidOperationException(
                    $"EXCLUDE rule '{exclusionValue}' uses a different address family from the target.");
            }


            var maximumPrefix =
                targetAddress.AddressFamily ==
                    AddressFamily.InterNetwork
                    ? 32
                    : 128;


            if (excludedPrefix < 0 ||
                excludedPrefix > maximumPrefix)
            {
                throw new InvalidOperationException(
                    $"EXCLUDE CIDR '{exclusionValue}' has an invalid prefix length.");
            }


            /*
             * An exclusion must be equal to or narrower than
             * the approved target range.
             */
            if (excludedPrefix <
                targetPrefix)
            {
                throw new InvalidOperationException(
                    $"EXCLUDE CIDR '{exclusionValue}' is broader than the approved target CIDR.");
            }


            if (!IsAddressInNetwork(
                excludedNetwork,
                targetAddress,
                targetPrefix))
            {
                throw new InvalidOperationException(
                    $"EXCLUDE CIDR '{exclusionValue}' is outside the approved target CIDR.");
            }


            return
                $"{excludedNetwork}/{excludedPrefix}";
        }

        private static bool IsAddressInNetwork(
    IPAddress address,
    IPAddress networkAddress,
    int prefixLength)
        {
            if (address.AddressFamily !=
                networkAddress.AddressFamily)
            {
                return false;
            }


            var addressBytes =
                address.GetAddressBytes();

            var networkBytes =
                networkAddress.GetAddressBytes();


            var maximumPrefix =
                addressBytes.Length * 8;


            if (prefixLength < 0 ||
                prefixLength > maximumPrefix)
            {
                return false;
            }


            var fullBytes =
                prefixLength / 8;

            var remainingBits =
                prefixLength % 8;


            for (var i = 0;
                 i < fullBytes;
                 i++)
            {
                if (addressBytes[i] !=
                    networkBytes[i])
                {
                    return false;
                }
            }


            if (remainingBits == 0)
            {
                return true;
            }


            var mask =
                (byte)(0xFF <<
                    (8 - remainingBits));


            return
                (addressBytes[fullBytes] & mask)
                ==
                (networkBytes[fullBytes] & mask);
        }

        private static bool GetBooleanOption(
    IEnumerable<ScannerOptionSnapshot> options,
    string optionCode,
    bool defaultValue)
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


            return bool.TryParse(
                option.OptionValue,
                out var value)
                    ? value
                    : defaultValue;
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
        private string ValidateAndNormalizeTarget(
    ScannerExecutionContext context)
        {
            var target =
                context.TargetValue.Trim();


            if (string.IsNullOrWhiteSpace(
                target))
            {
                throw new InvalidOperationException(
                    "Nmap target is empty.");
            }


            switch (
                context.TargetTypeCode
                    .ToUpperInvariant())
            {
                case "DOMAIN":

                    if (Uri.CheckHostName(target) !=
                        UriHostNameType.Dns)
                    {
                        throw new InvalidOperationException(
                            "The Nmap domain target is invalid.");
                    }

                    return target;


                case "IPV4":

                    if (!IPAddress.TryParse(
                            target,
                            out var ipv4) ||
                        ipv4.AddressFamily !=
                            AddressFamily.InterNetwork)
                    {
                        throw new InvalidOperationException(
                            "The Nmap IPv4 target is invalid.");
                    }

                    return ipv4.ToString();


                case "IPV6":

                    if (!IPAddress.TryParse(
                            target,
                            out var ipv6) ||
                        ipv6.AddressFamily !=
                            AddressFamily.InterNetworkV6)
                    {
                        throw new InvalidOperationException(
                            "The Nmap IPv6 target is invalid.");
                    }

                    return ipv6.ToString();


                case "CIDR":

                    return ValidateCidrTarget(
                        target);


                default:

                    throw new InvalidOperationException(
                        $"Target type '{context.TargetTypeCode}' is not supported by the Nmap adapter.");
            }
        }
        private string ValidateCidrTarget(
    string target)
        {
            var parts =
                target.Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);


            if (parts.Length != 2 ||
                !IPAddress.TryParse(
                    parts[0],
                    out var address) ||
                !int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var prefix))
            {
                throw new InvalidOperationException(
                    "The Nmap CIDR target is invalid.");
            }


            if (address.AddressFamily ==
                AddressFamily.InterNetwork)
            {
                if (prefix < _options.MinimumIpv4CidrPrefix ||
                    prefix > 32)
                {
                    throw new InvalidOperationException(
                        $"IPv4 CIDR scans must be /{_options.MinimumIpv4CidrPrefix} or narrower.");
                }
            }
            else if (address.AddressFamily ==
                     AddressFamily.InterNetworkV6)
            {
                if (prefix < _options.MinimumIpv6CidrPrefix ||
                    prefix > 128)
                {
                    throw new InvalidOperationException(
                        $"IPv6 CIDR scans must be /{_options.MinimumIpv6CidrPrefix} or narrower.");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "Unsupported CIDR address family.");
            }


            return
                $"{address}/{prefix}";
        }
        private List<string> BuildArguments(
        ScannerExecutionContext context,
        string target,
        int topPorts,
        bool serviceDetection,
        bool skipHostDiscovery,
        IReadOnlyList<string> exclusions)
        {


            var arguments =
                new List<string>();


            /*
             * TCP Connect scan is the initial safe baseline.
             * We are intentionally not enabling SYN, UDP,
             * OS detection, NSE scripts, or -A yet.
             */
            arguments.Add("-sT");


            if (IsIpv6Target(
                context.TargetTypeCode,
                target))
            {
                arguments.Add("-6");
            }


            arguments.Add(
                "--top-ports");

            arguments.Add(
                topPorts.ToString(
                    CultureInfo.InvariantCulture));


            if (serviceDetection)
            {
                arguments.Add("-sV");
            }


            if (skipHostDiscovery)
            {
                arguments.Add("-Pn");
            }


            // Conservative Nmap timing template.
            arguments.Add("-T3");


            // We currently care about exposed services.
            arguments.Add("--open");

            if (exclusions.Count > 0)
            {
                /*
                 * Nmap accepts multiple exclusions as a
                 * comma-separated value following --exclude.
                 */
                arguments.Add(
                    "--exclude");

                arguments.Add(
                    string.Join(
                        ",",
                        exclusions));
            }

            /*
             * Write XML directly to stdout so the process
             * runner can preserve the raw scanner result.
             */
            arguments.Add("-oX");

            arguments.Add("-");


            // Target is always the final argument after validation.
            arguments.Add(target);


            return arguments;
        }
        private static bool IsIpv6Target(
    string targetTypeCode,
    string target)
        {
            if (string.Equals(
                targetTypeCode,
                "IPV6",
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }


            if (!string.Equals(
                targetTypeCode,
                "CIDR",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }


            var addressPart =
                target.Split('/')[0];


            return IPAddress.TryParse(
                       addressPart,
                       out var address)
                   &&
                   address.AddressFamily ==
                       AddressFamily.InterNetworkV6;
        }
        private static bool TryReadNmapVersion(
    string xml,
    out string version)
        {
            version =
                "Unknown";


            if (string.IsNullOrWhiteSpace(
                xml))
            {
                return false;
            }


            try
            {
                var settings =
                    new XmlReaderSettings
                    {
                        /*
                         * Nmap XML may contain a DOCTYPE.
                         * Ignore it and never resolve external resources.
                         */
                        DtdProcessing =
                            DtdProcessing.Ignore,

                        XmlResolver =
                            null
                    };


                using var stringReader =
                    new StringReader(
                        xml);


                using var reader =
                    XmlReader.Create(
                        stringReader,
                        settings);


                while (reader.Read())
                {
                    if (reader.NodeType ==
                            XmlNodeType.Element &&
                        string.Equals(
                            reader.LocalName,
                            "nmaprun",
                            StringComparison.Ordinal))
                    {
                        version =
                            reader.GetAttribute(
                                "version")
                            ?? "Unknown";


                        return true;
                    }
                }
            }
            catch (XmlException)
            {
                return false;
            }


            return false;
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


            if (trimmed.Length <=
                maxLength)
            {
                return trimmed;
            }


            return
                trimmed[..maxLength]
                + "...";
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
                var scopeRules = ParseScopeRules(context.ScopeSnapshotJson);


                ValidateIncludeScope(
                    context,
                    target,
                    scopeRules);


                var exclusions =
                    GetNmapExclusions(
                        context,
                        target,
                        scopeRules);


                var profileOptions =
                    ParseOptions(
                        context.ProfileOptionsSnapshotJson);


                var topPorts =
                    GetIntOption(
                        profileOptions,
                        "NMAP_TOP_PORTS",
                        100);


                var serviceDetection =
                    GetBooleanOption(
                        profileOptions,
                        "NMAP_SERVICE_DETECTION",
                        true);


                var skipHostDiscovery =
                    GetBooleanOption(
                        profileOptions,
                        "NMAP_SKIP_HOST_DISCOVERY",
                        false);


                if (topPorts < 1 ||
                    topPorts >
                        _options.MaxTopPorts)
                {
                    return ScannerExecutionResult.Failed(
                        $"Nmap top-port count must be between 1 and {_options.MaxTopPorts}.");
                }


                var arguments = BuildArguments(
                context,
                target,
                topPorts,
                serviceDetection,
                skipHostDiscovery,
                exclusions);


                _logger.LogInformation(
                "Starting Nmap for Scan Job {ScanJobId}. Target={Target}, ExclusionCount={ExclusionCount}.",
                context.ScanJobID,
                target,
                exclusions.Count);


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


                /*
                 * Propagate worker cancellation so the worker can
                 * apply its normal timeout/shutdown handling.
                 */
                if (processResult.Cancelled &&
                    cancellationToken.IsCancellationRequested)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                }


                if (!processResult.Started)
                {
                    return ScannerExecutionResult.Failed(
                        $"Nmap could not be started: {processResult.StartError}");
                }


                if (processResult.TimedOut)
                {
                    return ScannerExecutionResult.Failed(
                        errorMessage:
                            "Nmap execution exceeded the configured timeout.",

                        exitCode:
                            processResult.ExitCode,

                        outputFormat:
                            "XML",

                        rawOutput:
                            processResult.StandardOutput);
                }


                /*
                 * Truncated XML cannot be trusted by the
                 * normalizer because it may be incomplete.
                 */
                if (processResult.StandardOutputTruncated)
                {
                    return ScannerExecutionResult.Failed(
                        errorMessage:
                            "Nmap XML output exceeded the configured output limit and was truncated.",

                        exitCode:
                            processResult.ExitCode,

                        outputFormat:
                            "XML",

                        rawOutput:
                            processResult.StandardOutput);
                }


                if (!TryReadNmapVersion(
                        processResult.StandardOutput,
                        out var nmapVersion))
                {
                    var error =
                        LimitErrorMessage(
                            processResult.StandardError);


                    return ScannerExecutionResult.Failed(
                        errorMessage:
                            string.IsNullOrWhiteSpace(
                                error)
                                ? "Nmap did not return valid XML output."
                                : $"Nmap did not return valid XML output. {error}",

                        exitCode:
                            processResult.ExitCode,

                        scannerVersion:
                            "Unknown",

                        outputFormat:
                            "XML",

                        rawOutput:
                            processResult.StandardOutput);
                }


                if (processResult.ExitCode != 0)
                {
                    var error =
                        LimitErrorMessage(
                            processResult.StandardError);


                    return ScannerExecutionResult.Failed(
                        errorMessage:
                            string.IsNullOrWhiteSpace(
                                error)
                                ? $"Nmap exited with code {processResult.ExitCode}."
                                : error,

                        exitCode:
                            processResult.ExitCode,

                        scannerVersion:
                            nmapVersion,

                        outputFormat:
                            "XML",

                        rawOutput:
                            processResult.StandardOutput);
                }


                if (processResult.StandardErrorTruncated)
                {
                    _logger.LogWarning(
                        "Nmap stderr was truncated for Scan Job {ScanJobId}.",
                        context.ScanJobID);
                }


                _logger.LogInformation(
                    "Nmap completed successfully for Scan Job {ScanJobId}. Version={NmapVersion}, DurationMs={DurationMs}.",
                    context.ScanJobID,
                    nmapVersion,
                    processResult.Duration.TotalMilliseconds);


                return ScannerExecutionResult.Completed(
                    exitCode:
                        processResult.ExitCode ?? 0,

                    scannerVersion:
                        nmapVersion,

                    outputFormat:
                        "XML",

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
                    "Nmap execution configuration or validation failed for Scan Job {ScanJobId}.",
                    context.ScanJobID);


                return ScannerExecutionResult.Failed(
                    $"Nmap execution failed validation: {ex.Message}");
            }
        }
    }
}