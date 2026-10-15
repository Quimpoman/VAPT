namespace EVOLVE_VAPT.ViewModels.ScanJob
{
    public class ScanJobListViewModel
    {
        public long ScanJobID { get; set; }

        public string JobCode { get; set; } = string.Empty;

        public string AssessmentCode { get; set; } = string.Empty;

        public string AssessmentName { get; set; } = string.Empty;

        public string TargetName { get; set; } = string.Empty;

        public string TargetValue { get; set; } = string.Empty;

        public string ScanProfileName { get; set; } = string.Empty;

        public string ScannerTypeCode { get; set; } = string.Empty;

        public string ScanJobStatusCode { get; set; } = string.Empty;

        public string ScanJobStatusName { get; set; } = string.Empty;

        public bool RequiresExplicitApproval { get; set; }

        public string? RequestedByEmail { get; set; }

        public DateTime RequestedDate { get; set; }
    }
}