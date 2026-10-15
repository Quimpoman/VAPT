using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace EVOLVE_VAPT.ViewModels.ScanProfile
{
    public class ScanProfileFormViewModel : IValidatableObject
    {
        public int ScanProfileID { get; set; }

        [Required]
        [Display(Name = "Scanner")]
        public int ScannerTypeID { get; set; }


        [Required]
        [StringLength(50)]
        [Display(Name = "Profile Code")]
        public string ProfileCode { get; set; } = string.Empty;


        [Required]
        [StringLength(150)]
        [Display(Name = "Profile Name")]
        public string ProfileName { get; set; } = string.Empty;


        [StringLength(1000)]
        public string? Description { get; set; }


        [Display(Name = "Intrusive Profile")]
        public bool IsIntrusive { get; set; }


        [Display(Name = "Requires Explicit Approval")]
        public bool RequiresExplicitApproval { get; set; }


        [Range(
            1,
            1440,
            ErrorMessage =
                "Timeout must be between 1 and 1440 minutes.")]
        [Display(Name = "Timeout (Minutes)")]
        public int TimeoutMinutes { get; set; } = 30;


        [Range(
            1,
            100,
            ErrorMessage =
                "Maximum concurrency must be between 1 and 100.")]
        [Display(Name = "Maximum Concurrency")]
        public int MaxConcurrency { get; set; } = 1;


        public List<SelectListItem> ScannerTypeOptions { get; set; }
            = new();


        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            /*
             * Intrusive profiles require an explicit approval
             * control before they can eventually be executed.
             */
            if (IsIntrusive &&
                !RequiresExplicitApproval)
            {
                yield return new ValidationResult(
                    "Intrusive profiles must require explicit approval.",
                    new[]
                    {
                        nameof(RequiresExplicitApproval)
                    }
                );
            }
        }
    }
}