namespace EVOLVE_VAPT.Scanning.Configuration
{
    public sealed class NmapExecutionOptions
    {
        public const string SectionName =
            "Scanners:Nmap";


        public string ExecutablePath { get; set; } =
            string.Empty;


        public int MaxStandardOutputCharacters { get; set; } =
            5_000_000;


        public int MaxStandardErrorCharacters { get; set; } =
            250_000;


        // Hard upper limit regardless of Scan Profile options.
        public int MaxTopPorts { get; set; } =
            1000;


        // Maximum IPv4 range: 256 addresses.
        public int MinimumIpv4CidrPrefix { get; set; } =
            24;


        // Maximum IPv6 range: 256 addresses.
        public int MinimumIpv6CidrPrefix { get; set; } =
            120;
    }
}