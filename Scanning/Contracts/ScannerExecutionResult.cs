namespace EVOLVE_VAPT.Scanning.Contracts
{
    /// <summary>
    /// Raw result returned by a scanner adapter.
    ///
    /// We intentionally do not convert this directly into
    /// EVOLVE_VAPT Findings yet. Normalization will be a
    /// separate pipeline stage.
    /// </summary>
    public class ScannerExecutionResult
    {
        public bool Success { get; init; }

        public int? ExitCode { get; init; }

        public string? ScannerVersion { get; init; }

        public string OutputFormat { get; init; } =
            "TEXT";

        public string RawOutput { get; init; } =
            string.Empty;

        public string? ErrorMessage { get; init; }


        public static ScannerExecutionResult Completed(
            string rawOutput,
            string? scannerVersion = null,
            int? exitCode = 0,
            string outputFormat = "TEXT")
        {
            return new ScannerExecutionResult
            {
                Success = true,
                RawOutput = rawOutput,
                ScannerVersion = scannerVersion,
                ExitCode = exitCode,
                OutputFormat = outputFormat
            };
        }


        public static ScannerExecutionResult Failed(
        string errorMessage,
        int? exitCode = null,
        string? scannerVersion = null,
        string? outputFormat = null,
        string? rawOutput = null)
        {
            return new ScannerExecutionResult
            {
                Success = false,

                ExitCode =
                    exitCode,

                ScannerVersion =
                    scannerVersion,

                OutputFormat =
                    outputFormat,

                RawOutput =
                    rawOutput,

                ErrorMessage =
                    errorMessage
            };
        }
    }
}