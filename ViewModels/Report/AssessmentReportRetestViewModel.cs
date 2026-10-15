namespace EVOLVE_VAPT.ViewModels.Report
{
    public class AssessmentReportRetestViewModel
    {
        public long FindingRetestID { get; set; }

        public int CycleNo { get; set; }

        public string RetestStatusName { get; set; } =
            string.Empty;

        public string? RetestOutcomeName { get; set; }

        public string? RetestJobCode { get; set; }

        public string? ScanJobStatusName { get; set; }

        public DateTime RequestedDate { get; set; }

        public DateTime? ReviewedDate { get; set; }

        public string? ReviewedByEmail { get; set; }

        public string? ReviewNotes { get; set; }
    }
}