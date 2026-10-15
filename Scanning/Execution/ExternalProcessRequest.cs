namespace EVOLVE_VAPT.Scanning.Execution
{
    public sealed class ExternalProcessRequest
    {
        // Must come from trusted application configuration,
        // never directly from user input.
        public string ExecutablePath { get; init; } =
            string.Empty;


        public IReadOnlyList<string> Arguments { get; init; } =
            Array.Empty<string>();


        public string? WorkingDirectory { get; init; }


        public TimeSpan Timeout { get; init; } =
            TimeSpan.FromMinutes(5);


        // Prevent a scanner from exhausting application memory
        // by returning unexpectedly large console output.
        public int MaxStandardOutputCharacters { get; init; } =
            1_000_000;


        public int MaxStandardErrorCharacters { get; init; } =
            250_000;
    }
}