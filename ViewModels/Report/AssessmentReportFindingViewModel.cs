namespace EVOLVE_VAPT.ViewModels.Report
{
    public class AssessmentReportFindingViewModel
    {
        public long FindingID { get; set; }

        public string SourceFindingIdentifier { get; set; } =
            string.Empty;

        public string Title { get; set; } =
            string.Empty;

        public string SeverityCode { get; set; } =
            string.Empty;

        public string SeverityName { get; set; } =
            string.Empty;

        public string FindingStatusCode { get; set; } =
            string.Empty;

        public string FindingStatusName { get; set; } =
            string.Empty;

        public string TargetValue { get; set; } =
            string.Empty;

        public string ScannerTypeCode { get; set; } =
            string.Empty;

        public string? ScannerVersion { get; set; }

        public string? Description { get; set; }

        public string? Evidence { get; set; }

        public string? Recommendation { get; set; }

        public string? Reference { get; set; }

        public DateTime CreatedDate { get; set; }


        public List<AssessmentReportRemediationViewModel>
            Remediations
        { get; set; } = new();

        public List<AssessmentReportRetestViewModel>
            Retests
        { get; set; } = new();
    }
}