namespace EVOLVE_VAPT.ViewModels.Dashboard
{
    public class DashboardRecentScanJobViewModel
    {
        public long ScanJobID { get; set; }

        public string JobCode { get; set; } =
            string.Empty;

        public string ScanJobStatusCode { get; set; } =
            string.Empty;

        public string ScanJobStatusName { get; set; } =
            string.Empty;

        public string ScanJobPurposeCode { get; set; } =
            string.Empty;

        public string ScannerTypeCode { get; set; } =
            string.Empty;

        public string TargetValue { get; set; } =
            string.Empty;

        public DateTime RequestedDate { get; set; }
    }
}