namespace EVOLVE_VAPT.Scanning.Configuration
{
    public sealed class ZapExecutionOptions
    {
        public const string SectionName = "Scanners:Zap";


        public string JavaExecutablePath { get; set; } =
            string.Empty;


        public string InstallationDirectory { get; set; } =
            string.Empty;


        public string JarPath { get; set; } =
            string.Empty;


        public int MaxHeapMegabytes { get; set; } =
            512;


        public int MaxStandardOutputCharacters { get; set; } =
            5_000_000;


        public int MaxStandardErrorCharacters { get; set; } =
            500_000;


        public int RequestTimeoutSeconds { get; set; } =
            15;


        public int MaxSpiderMinutes { get; set; } =
            10;
    }
}