namespace EVOLVE_VAPT.Scanning.Configuration
{
    public sealed class NucleiExecutionOptions
    {
        public const string SectionName =
            "Scanners:Nuclei";


        public string ExecutablePath { get; set; } =
            string.Empty;


        public string TemplatesPath { get; set; } =
            string.Empty;


        public int MaxStandardOutputCharacters { get; set; } =
            10_000_000;


        public int MaxStandardErrorCharacters { get; set; } =
            500_000;


        public int MaxRequestsPerSecond { get; set; } =
            25;


        public int MaxTemplateConcurrency { get; set; } =
            10;


        public int RequestTimeoutSeconds { get; set; } =
            10;


        public int Retries { get; set; } =
            1;
    }
}