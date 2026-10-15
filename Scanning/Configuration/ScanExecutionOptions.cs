namespace EVOLVE_VAPT.Scanning.Configuration
{
    public class ScanExecutionOptions
    {
        public const string SectionName =
            "ScanExecution";


        public int MaxConcurrentJobs { get; set; } =
            1;


        public int HeartbeatSeconds { get; set; } =
            20;


        public int StaleAfterMinutes { get; set; } =
            2;
    }
}