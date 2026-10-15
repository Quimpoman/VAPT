using System.ComponentModel.DataAnnotations;

namespace EVOLVE_VAPT.ViewModels.Remediation
{
    public class RetestListViewModel
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


        public string RemediationStatusCode { get; set; } =
            string.Empty;

        public string RemediationStatusName { get; set; } =
            string.Empty;


        public string AssessmentCode { get; set; } =
            string.Empty;

        public string TargetValue { get; set; } =
            string.Empty;


        public string? AssignedToEmail { get; set; }

        public DateOnly? TargetDate { get; set; }

        public DateTime StartedDate { get; set; }
    }
}