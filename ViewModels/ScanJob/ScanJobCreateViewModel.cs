using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace EVOLVE_VAPT.ViewModels.ScanJob
{
    public class ScanJobCreateViewModel
    {
        [Required]
        [Display(Name = "Assessment Target")]
        public int AssessmentTargetID { get; set; }


        [Required]
        [Display(Name = "Scan Profile")]
        public int ScanProfileID { get; set; }


        public List<SelectListItem> TargetOptions { get; set; }
            = new();


        public List<SelectListItem> ScanProfileOptions { get; set; }
            = new();
    }
}