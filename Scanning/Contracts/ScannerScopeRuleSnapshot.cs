namespace EVOLVE_VAPT.Scanning.Contracts
{
    public class ScannerScopeRuleSnapshot
    {
        public int AssessmentScopeRuleID { get; set; }

        public string RuleTypeCode { get; set; } =
            string.Empty;

        public string RuleValue { get; set; } =
            string.Empty;

        public string? Description { get; set; }
    }
}