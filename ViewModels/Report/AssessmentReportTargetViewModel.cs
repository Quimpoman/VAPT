namespace EVOLVE_VAPT.ViewModels.Report
{
    public class AssessmentReportTargetViewModel
    {
        public int AssessmentTargetID { get; set; }

        public string TargetName { get; set; } =
            string.Empty;

        public string TargetTypeCode { get; set; } =
            string.Empty;

        public string TargetValue { get; set; } =
            string.Empty;

        public string AuthorizationStatusName { get; set; } =
            string.Empty;

        public string? AuthorizationReference { get; set; }

        public DateTime? AuthorizationValidFrom { get; set; }

        public DateTime? AuthorizationValidUntil { get; set; }


        public List<AssessmentReportScopeRuleViewModel>
            ScopeRules
        { get; set; } = new();
    }
}