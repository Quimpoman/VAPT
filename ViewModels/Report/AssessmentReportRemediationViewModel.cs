namespace EVOLVE_VAPT.ViewModels.Report
{
    public class AssessmentReportRemediationViewModel
    {
        public long FindingRemediationID { get; set; }

        public int CycleNo { get; set; }

        public string RemediationStatusName { get; set; } =
            string.Empty;

        public string? AssignedToEmail { get; set; }

        public DateOnly? TargetDate { get; set; }

        public string? RemediationNotes { get; set; }

        public DateTime StartedDate { get; set; }

        public DateTime? SubmittedForRetestDate { get; set; }

        public string? SubmissionNotes { get; set; }
    }
}