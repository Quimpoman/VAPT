using System.ComponentModel.DataAnnotations;

namespace EVOLVE_VAPT.ViewModels.Remediation
{
    public class RemediationDetailsViewModel
    {
        public long FindingRemediationID { get; set; }

        public long FindingID { get; set; }

        public int CycleNo { get; set; }


        public string FindingTitle { get; set; } =
            string.Empty;

        public string SeverityCode { get; set; } =
            string.Empty;

        public string SeverityName { get; set; } =
            string.Empty;


        public string FindingStatusCode { get; set; } =
            string.Empty;

        public string FindingStatusName { get; set; } =
            string.Empty;


        public string RemediationStatusCode { get; set; } =
            string.Empty;

        public string RemediationStatusName { get; set; } =
            string.Empty;


        public string AssessmentCode { get; set; } =
            string.Empty;

        public string AssessmentName { get; set; } =
            string.Empty;

        public string TargetValue { get; set; } =
            string.Empty;

        public string JobCode { get; set; } =
            string.Empty;


        public string? AssignedToEmail { get; set; }

        public string? RemediationNotes { get; set; }


        public DateOnly? TargetDate { get; set; }

        public DateTime StartedDate { get; set; }


        public DateTime? SubmittedForRetestDate { get; set; }

        public string? SubmittedByEmail { get; set; }

        public string? SubmissionNotes { get; set; }


        public DateTime CreatedDate { get; set; }

        public string? CreatedByEmail { get; set; }
    }
}