namespace EVOLVE_VAPT.ViewModels.Finding
{
    public class FindingListViewModel
    {
        public long FindingID { get; set; }

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

        public string AssessmentCode { get; set; } =
            string.Empty;

        public string JobCode { get; set; } =
            string.Empty;

        public string ScannerTypeCode { get; set; } =
            string.Empty;

        public DateTime CreatedDate { get; set; }
    }
}