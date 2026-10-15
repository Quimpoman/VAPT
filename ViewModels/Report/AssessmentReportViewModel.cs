namespace EVOLVE_VAPT.ViewModels.Report
{
    public class AssessmentReportViewModel
    {
        public int AssessmentID { get; set; }

        public string AssessmentCode { get; set; } =
            string.Empty;

        public string AssessmentName { get; set; } =
            string.Empty;

        public string? Description { get; set; }

        public string? Methodology { get; set; }

        public string? Environment { get; set; }

        public string AssessmentStatusName { get; set; } =
            string.Empty;


        // Executive summary
        public int TotalFindings { get; set; }

        public int CriticalFindings { get; set; }

        public int HighFindings { get; set; }

        public int MediumFindings { get; set; }

        public int LowFindings { get; set; }

        public int InformationalFindings { get; set; }

        public int OpenFindings { get; set; }

        public int ClosedFindings { get; set; }

        public int FalsePositiveFindings { get; set; }


        public List<AssessmentReportTargetViewModel>
            Targets
        { get; set; } = new();

        public List<AssessmentReportScanJobViewModel>
            ScanJobs
        { get; set; } = new();

        public List<AssessmentReportFindingViewModel>
            Findings
        { get; set; } = new();

        public DateTime GeneratedDate { get; set; }

        public string GeneratedBy { get; set; } =
            string.Empty;

        public DateTime? PlannedStartDate { get; set; }

        public DateTime? PlannedEndDate { get; set; }

        public DateTime? ActualStartDate { get; set; }

        public DateTime? ActualEndDate { get; set; }

        public string? OwnerEmail { get; set; }

        public string? ReviewerEmail { get; set; }
    }
}