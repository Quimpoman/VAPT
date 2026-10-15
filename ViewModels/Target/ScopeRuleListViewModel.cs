namespace EVOLVE_VAPT.ViewModels.Target
{
    /// <summary>
    /// Represents one active Include/Exclude rule
    /// displayed under an Assessment Target.
    /// </summary>
    public class ScopeRuleListViewModel
    {
        public int AssessmentScopeRuleID { get; set; }

        public int AssessmentTargetID { get; set; }

        public string ScopeRuleTypeCode { get; set; } = string.Empty;

        public string ScopeRuleTypeName { get; set; } = string.Empty;

        public string RuleValue { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? CreatedByEmail { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}