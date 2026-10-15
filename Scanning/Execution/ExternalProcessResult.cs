namespace EVOLVE_VAPT.Scanning.Execution
{
    public sealed class ExternalProcessResult
    {
        public bool Started { get; init; }

        public int? ExitCode { get; init; }

        public bool TimedOut { get; init; }

        public bool Cancelled { get; init; }


        public string StandardOutput { get; init; } =
            string.Empty;

        public string StandardError { get; init; } =
            string.Empty;


        public bool StandardOutputTruncated { get; init; }

        public bool StandardErrorTruncated { get; init; }


        public string? StartError { get; init; }


        public TimeSpan Duration { get; init; }


        public bool Succeeded =>
            Started &&
            !TimedOut &&
            !Cancelled &&
            ExitCode == 0;
    }
}