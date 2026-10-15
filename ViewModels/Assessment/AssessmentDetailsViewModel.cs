using EVOLVE_VAPT.ViewModels.ScanJob;
using EVOLVE_VAPT.ViewModels.Target;



namespace EVOLVE_VAPT.ViewModels.Assessment
{
    public class AssessmentDetailsViewModel
    {
        public int AssessmentID { get; set; }

        public string AssessmentCode { get; set; } = string.Empty;

        public string AssessmentName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Methodology { get; set; }

        public string? Environment { get; set; }

        public string AssessmentStatusName { get; set; } = string.Empty;

        public DateTime? PlannedStartDate { get; set; }

        public DateTime? PlannedEndDate { get; set; }

        public DateTime? ActualStartDate { get; set; }

        public DateTime? ActualEndDate { get; set; }

        public string? OwnerEmail { get; set; }

        public string? ReviewerEmail { get; set; }

        public bool IsActive { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public string? EditedBy { get; set; }

        public DateTime? EditedDate { get; set; }

        public string? CreatedByEmail { get; set; }

        public string? EditedByEmail { get; set; }

        public IReadOnlyList<AssessmentTargetListViewModel>
    Targets
        { get; set; } =
        Array.Empty<AssessmentTargetListViewModel>();


        public IReadOnlyList<ScanJobListViewModel>
            ScanJobs
        { get; set; } =
                Array.Empty<ScanJobListViewModel>();
    }
}