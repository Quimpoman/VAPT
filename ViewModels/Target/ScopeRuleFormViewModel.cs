using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace EVOLVE_VAPT.ViewModels.Target
{
    /// <summary>
    /// Represents user-submitted data when defining
    /// an Include or Exclude boundary for a target.
    /// </summary>
    public class ScopeRuleFormViewModel
    {
        public int AssessmentTargetID { get; set; }


        [Required]
        [Display(Name = "Rule Type")]
        public int ScopeRuleTypeID { get; set; }


        [Required]
        [StringLength(1000)]
        [Display(Name = "Rule Value")]
        public string RuleValue { get; set; } = string.Empty;


        [StringLength(500)]
        public string? Description { get; set; }


        // Populated from ref_ScopeRuleType.
        public List<SelectListItem> RuleTypeOptions { get; set; } = new();
    }
}