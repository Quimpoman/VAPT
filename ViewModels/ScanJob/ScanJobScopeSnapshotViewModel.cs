namespace EVOLVE_VAPT.ViewModels.ScanJob
{
    public class ScanJobScopeSnapshotViewModel
    {
        public int AssessmentScopeRuleID { get; set; }

        public string RuleTypeCode { get; set; } = string.Empty;

        public string RuleValue { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
