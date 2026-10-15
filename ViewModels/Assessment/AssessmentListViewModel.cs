using EVOLVE_VAPT.ViewModels.ScanJob;

namespace EVOLVE_VAPT.ViewModels.Assessment
{
    public class AssessmentListViewModel
    {
        public int AssessmentID { get; set; }

        public string AssessmentCode { get; set; } = string.Empty;

        public string AssessmentName { get; set; } = string.Empty;

        public string AssessmentStatusName { get; set; } = string.Empty;

        public string? Environment { get; set; }

        public DateTime? PlannedStartDate { get; set; }

        public DateTime? PlannedEndDate { get; set; }

        public string? OwnerEmail { get; set; }

        public string? ReviewerEmail { get; set; }

        public bool IsActive { get; set; }

   

    }
}