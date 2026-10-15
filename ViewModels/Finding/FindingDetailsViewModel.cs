namespace EVOLVE_VAPT.ViewModels.Finding
{
    public class RemediationFormViewModel
    {
        public long FindingID { get; set; }

        public long ScanResultID { get; set; }

        public long ScanJobID { get; set; }

        public int AssessmentTargetID { get; set; }


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

        public string AssessmentCode { get; set; } =
            string.Empty;

        public string AssessmentName { get; set; } =
            string.Empty;

        public string TargetName { get; set; } =
            string.Empty;

        public string JobCode { get; set; } =
            string.Empty;


        public string? Description { get; set; }

        public string? Evidence { get; set; }

        public string? Recommendation { get; set; }

        public string? Reference { get; set; }


        public string ScannerTypeCode { get; set; } =
            string.Empty;

        public string? ScannerVersion { get; set; }


        public string? ReviewedByEmail { get; set; }

        public DateTime? ReviewedDate { get; set; }

        public string? ValidationNotes { get; set; }


        public DateTime CreatedDate { get; set; }
    }
}