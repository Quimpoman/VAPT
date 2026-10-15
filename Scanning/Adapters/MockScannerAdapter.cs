using System.Text.Json;
using EVOLVE_VAPT.Scanning.Contracts;

namespace EVOLVE_VAPT.Scanning.Adapters
{
    /// <summary>
    /// Simulated scanner used to prove the complete
    /// EVOLVE_VAPT orchestration pipeline safely.
    ///
    /// No external command or network scan is performed.
    /// </summary>
    public class MockScannerAdapter : IScannerAdapter
    {
        public string ScannerTypeCode => "MOCK";


        public async Task<ScannerExecutionResult> ExecuteAsync(
            ScannerExecutionContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                var options =
                    ParseOptions(
                        context.ProfileOptionsSnapshotJson);


                /*
                 * Resolve the effective Mock Scanner options.
                 *
                 * These defaults mirror the reference-table
                 * defaults we previously configured.
                 */
                var generateFindings =
                    GetBooleanOption(
                        options,
                        "GENERATE_FINDINGS",
                        defaultValue: true);


                var findingCount =
                    GetIntegerOption(
                        options,
                        "FINDING_COUNT",
                        defaultValue: 3);


                var simulatedDelay =
                    GetIntegerOption(
                        options,
                        "SIMULATED_DELAY",
                        defaultValue: 5);


                /*
                 * Defense in depth:
                 * do not trust integer configuration simply
                 * because it came from the database.
                 */
                if (findingCount < 0 ||
                    findingCount > 100)
                {
                    return ScannerExecutionResult.Failed(
                        "Mock FINDING_COUNT must be between 0 and 100.");
                }


                if (simulatedDelay < 0 ||
                    simulatedDelay > 60)
                {
                    return ScannerExecutionResult.Failed(
                        "Mock SIMULATED_DELAY must be between 0 and 60 seconds.");
                }


                /*
                 * Simulate asynchronous scanner execution.
                 *
                 * CancellationToken allows the future worker
                 * timeout/shutdown mechanism to interrupt it.
                 */
                if (simulatedDelay > 0)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(
                            simulatedDelay),
                        cancellationToken);
                }


                var findings =
                    generateFindings
                        ? GenerateMockFindings(
                            context,
                            findingCount)
                        : new List<object>();


                /*
                 * Validate and embed the immutable Scope
                 * Snapshot in the simulated scanner output.
                 */
                using var scopeDocument =
                    JsonDocument.Parse(
                        context.ScopeSnapshotJson);


                var rawResult =
                    new
                    {
                        SchemaVersion =
                            "mock-1.0",

                        Scanner =
                            "MOCK",

                        ScannerVersion =
                            "1.0",

                        JobCode =
                            context.JobCode,

                        ScanJobID =
                            context.ScanJobID,

                        Target = new
                        {
                            Type =
                                context.TargetTypeCode,

                            Value =
                                context.TargetValue
                        },

                        Scope =
                            scopeDocument.RootElement
                                .Clone(),

                        GeneratedAtUtc =
                            DateTime.UtcNow,

                        FindingCount =
                            findings.Count,

                        Findings =
                            findings
                    };


                var rawJson =
                    JsonSerializer.Serialize(
                        rawResult,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });


                return ScannerExecutionResult.Completed(
                 rawJson,
                 scannerVersion: "1.0",
                 exitCode: 0,
                 outputFormat: "JSON");
            }
            catch (OperationCanceledException)
            {
                /*
                 * Cancellation must propagate to the worker.
                 * The worker will determine whether this was
                 * timeout, shutdown or future user cancellation.
                 */
                throw;
            }
            catch (JsonException ex)
            {
                return ScannerExecutionResult.Failed(
                    $"Mock Scanner received invalid snapshot JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ScannerExecutionResult.Failed(
                    $"Mock Scanner failed: {ex.Message}");
            }
        }


        private static List<ScannerOptionSnapshot>
            ParseOptions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new();
            }


            return JsonSerializer.Deserialize<
                List<ScannerOptionSnapshot>>(json)
                ?? new();
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


        private static int GetIntegerOption(
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
                out var value)
                    ? value
                    : defaultValue;
        }


        private static List<object> GenerateMockFindings(
            ScannerExecutionContext context,
            int findingCount)
        {
            var findings =
                new List<object>();


            var severities =
                new[]
                {
                    "INFO",
                    "LOW",
                    "MEDIUM",
                    "HIGH",
                    "CRITICAL"
                };


            for (var i = 1;
                 i <= findingCount;
                 i++)
            {
                /*
                 * Deterministic data is easier to test than
                 * randomly generated Mock Scanner findings.
                 */
                var severity =
                    severities[
                        (i - 1) %
                        severities.Length];


                findings.Add(
                    new
                    {
                        FindingIdentifier =
                            $"MOCK-{context.ScanJobID}-{i:D3}",

                        Title =
                            $"Mock Vulnerability {i}",

                        Severity =
                            severity,

                        Target =
                            context.TargetValue,

                        Description =
                            $"Simulated {severity} finding generated by the EVOLVE_VAPT Mock Scanner.",

                        Evidence =
                            $"Mock evidence for finding {i}.",

                        Recommendation =
                            "This is simulated data. No remediation action is required.",

                        Reference =
                            $"MOCK-REF-{i:D3}"
                    });
            }


            return findings;
        }


    }
}