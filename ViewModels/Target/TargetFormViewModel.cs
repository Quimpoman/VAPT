using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;



namespace EVOLVE_VAPT.ViewModels.Target
{
    /// <summary>
    /// Represents the fields a user may submit
    /// when adding a target to an assessment.
    ///
    /// Authorization status is intentionally excluded.
    /// New targets always begin as Pending Approval.
    /// </summary>
    public class TargetFormViewModel : IValidatableObject
    {
        /// <summary>
        /// Identifies the relationship between the target
        /// and the Assessment being edited.
        /// </summary>
        public int AssessmentTargetID { get; set; }


        /// <summary>
        /// Parent Assessment. This is preserved during editing
        /// so we know where to redirect after saving.
        /// </summary>
        public int AssessmentID { get; set; }


        [Required]
        [Display(Name = "Target Type")]
        public int TargetTypeID { get; set; }


        [Required]
        [StringLength(200)]
        [Display(Name = "Target Name")]
        public string TargetName { get; set; } = string.Empty;


        [Required]
        [StringLength(1000)]
        [Display(Name = "Target Value")]
        public string TargetValue { get; set; } = string.Empty;


        [Display(Name = "Description")]
        public string? Description { get; set; }


        [StringLength(250)]
        [Display(Name = "Authorization Reference")]
        public string? AuthorizationReference { get; set; }


        [Display(Name = "Authorization Notes")]
        public string? AuthorizationNotes { get; set; }


        [DataType(DataType.Date)]
        [Display(Name = "Authorization Valid From")]
        public DateTime? AuthorizationValidFrom { get; set; }


        [DataType(DataType.Date)]
        [Display(Name = "Authorization Valid Until")]
        public DateTime? AuthorizationValidUntil { get; set; }


        // Populated by TargetService for the Target Type dropdown.
        public List<SelectListItem> TargetTypeOptions { get; set; } = new();


        /// <summary>
        /// Performs form-level business validation before
        /// the data reaches TargetService.
        /// </summary>
        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            // An authorization period must never end before it begins.
            if (AuthorizationValidFrom.HasValue &&
                AuthorizationValidUntil.HasValue &&
                AuthorizationValidUntil < AuthorizationValidFrom)
            {
                yield return new ValidationResult(
                    "Authorization Valid Until cannot be earlier than Authorization Valid From.",
                    new[]
                    {
                        nameof(AuthorizationValidUntil)
                    });
            }
        }
    }
}