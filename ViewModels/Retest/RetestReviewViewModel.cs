using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace EVOLVE_VAPT.ViewModels.Retest
{
    public class RetestReviewViewModel
    {
        public long FindingRetestID { get; set; }


        [Required]
        [Display(Name = "Retest Outcome")]
        public int? RetestOutcomeID { get; set; }


        [Required]
        [StringLength(2000)]
        [Display(Name = "Review Notes")]
        public string ReviewNotes { get; set; } =
            string.Empty;


        public List<SelectListItem>
            OutcomeOptions
        { get; set; } = new();
    }
}