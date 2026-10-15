namespace EVOLVE_VAPT.ViewModels.Retest
{
    public class RetestListViewModel
    {
        public long FindingRetestID { get; set; }

        public long FindingID { get; set; }

        public int CycleNo { get; set; }


        public string FindingTitle { get; set; } =
            string.Empty;

        public string SeverityCode { get; set; } =
            string.Empty;

        public string SeverityName { get; set; } =
            string.Empty;


        public string AssessmentCode { get; set; } =
            string.Empty;

        public string TargetValue { get; set; } =
            string.Empty;


        public string RetestStatusCode { get; set; } =
            string.Empty;

        public string RetestStatusName { get; set; } =
            string.Empty;

        public string? RetestOutcomeName { get; set; }


        public string? RetestJobCode { get; set; }

        public string? ScanJobStatusCode { get; set; }

        public string? ScanJobStatusName { get; set; }


        public string? RequestedByEmail { get; set; }

        public DateTime RequestedDate { get; set; }
    }
}