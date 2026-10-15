namespace EVOLVE_VAPT.ViewModels.Retest
{
    public class RetestDetailsViewModel
    {
        public long FindingRetestID { get; set; }

        public long FindingID { get; set; }

        public long FindingRemediationID { get; set; }

        public int CycleNo { get; set; }


        public string FindingTitle { get; set; } =
            string.Empty;

        public string SourceFindingIdentifier { get; set; } =
            string.Empty;

        public string SeverityCode { get; set; } =
            string.Empty;

        public string SeverityName { get; set; } =
            string.Empty;

        public string TargetValue { get; set; } =
            string.Empty;


        public string? FindingDescription { get; set; }

        public string? OriginalEvidence { get; set; }

        public string? Recommendation { get; set; }

        public string? Reference { get; set; }


        public string AssessmentCode { get; set; } =
            string.Empty;

        public string AssessmentName { get; set; } =
            string.Empty;


        public string OriginalJobCode { get; set; } =
            string.Empty;

        public string? RetestJobCode { get; set; }


        public string RetestStatusCode { get; set; } =
            string.Empty;

        public string RetestStatusName { get; set; } =
            string.Empty;

        public string? RetestOutcomeCode { get; set; }

        public string? RetestOutcomeName { get; set; }


        public string? ScanJobStatusCode { get; set; }

        public string? ScanJobStatusName { get; set; }


        public string? RemediationNotes { get; set; }

        public DateOnly? RemediationTargetDate { get; set; }

        public string? SubmissionNotes { get; set; }


        public string? ScannerTypeCode { get; set; }

        public string? ScannerVersion { get; set; }

        public bool? ScanSuccessful { get; set; }

        public string? OutputFormat { get; set; }

        public string? RawOutput { get; set; }

        public string? ScanErrorMessage { get; set; }


        public string? RequestedByEmail { get; set; }

        public DateTime RequestedDate { get; set; }


        public string? ReviewedByEmail { get; set; }

        public DateTime? ReviewedDate { get; set; }

        public string? ReviewNotes { get; set; }
    }
}