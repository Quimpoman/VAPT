namespace EVOLVE_VAPT.ViewModels.Target
{
    /// <summary>
    /// Represents one target displayed in the
    /// Targets & Scope listing for an assessment.
    ///
    /// This is intentionally separate from the EF entity
    /// so the UI only receives the fields it requires.
    /// </summary>
    public class AssessmentTargetListViewModel
    {
        public int AssessmentTargetID { get; set; }

        public int AssessmentID { get; set; }

        public int TargetID { get; set; }

        public string TargetName { get; set; } = string.Empty;

        public string TargetValue { get; set; } = string.Empty;

        public string TargetTypeName { get; set; } = string.Empty;

        public string AuthorizationStatusName { get; set; } = string.Empty;

        public string? AuthorizationReference { get; set; }

        public string? AuthorizedByEmail { get; set; }

        public DateTime? AuthorizedDate { get; set; }

        public DateTime? AuthorizationValidFrom { get; set; }

        public DateTime? AuthorizationValidUntil { get; set; }

        public bool IsActive { get; set; }
    }
}