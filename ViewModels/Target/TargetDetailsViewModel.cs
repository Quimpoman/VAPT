namespace EVOLVE_VAPT.ViewModels.Target
{
    /// <summary>
    /// Read model used by the Target Details page.
    /// It combines target information with its
    /// assessment-specific authorization state.
    /// </summary>
    public class TargetDetailsViewModel
    {
        public int AssessmentTargetID { get; set; }

        public int AssessmentID { get; set; }

        public string AssessmentCode { get; set; } = string.Empty;

        public string AssessmentName { get; set; } = string.Empty;


        public int TargetID { get; set; }

        public string TargetName { get; set; } = string.Empty;

        public string TargetValue { get; set; } = string.Empty;

        public string TargetTypeCode { get; set; } = string.Empty;

        public string TargetTypeName { get; set; } = string.Empty;


        public string AuthorizationStatusCode { get; set; } = string.Empty;

        public string AuthorizationStatusName { get; set; } = string.Empty;

        public string? AuthorizationReference { get; set; }

        public string? AuthorizationNotes { get; set; }

        public string? AuthorizedByEmail { get; set; }

        public DateTime? AuthorizedDate { get; set; }

        public DateTime? AuthorizationValidFrom { get; set; }

        public DateTime? AuthorizationValidUntil { get; set; }


        public bool IsActive { get; set; }

        public string? CreatedByEmail { get; set; }

        public DateTime CreatedDate { get; set; }

        public string? EditedByEmail { get; set; }

        public DateTime? EditedDate { get; set; }

        /// <summary>
        /// Current active Include/Exclude rules belonging
        /// to this Assessment Target.
        /// </summary>
        public List<ScopeRuleListViewModel> ScopeRules { get; set; } = new();

    }
}