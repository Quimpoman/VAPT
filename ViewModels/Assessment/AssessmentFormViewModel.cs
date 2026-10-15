using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EVOLVE_VAPT.ViewModels.Assessment
{
    public class AssessmentFormViewModel : IValidatableObject
    {
        public int AssessmentID { get; set; }


        [Required]
        [StringLength(50)]
        [Display(Name = "Assessment Code")]
        public string AssessmentCode { get; set; } = string.Empty;
        
        
        [Required]
        [StringLength(200)]
        [Display(Name = "Assessment Name")]
        public string AssessmentName { get; set; } = string.Empty;


        [Display(Name = "Description")]
        public string? Description { get; set; }


        [StringLength(200)]
        [Display(Name = "Methodology")]
        public string? Methodology { get; set; }


        [StringLength(100)]
        [Display(Name = "Environment")]
        public string? Environment { get; set; }


        [Required]
        [Display(Name = "Status")]
        public int AssessmentStatusID { get; set; }


        [DataType(DataType.Date)]
        [Display(Name = "Planned Start Date")]
        public DateTime? PlannedStartDate { get; set; }


        [DataType(DataType.Date)]
        [Display(Name = "Planned End Date")]
        public DateTime? PlannedEndDate { get; set; }


        [DataType(DataType.Date)]
        [Display(Name = "Actual Start Date")]
        public DateTime? ActualStartDate { get; set; }


        [DataType(DataType.Date)]
        [Display(Name = "Actual End Date")]
        public DateTime? ActualEndDate { get; set; }


        [Display(Name = "Owner")]
        public string? OwnerUserID { get; set; }


        [Display(Name = "Reviewer")]
        public string? ReviewerUserID { get; set; }


        public List<SelectListItem> StatusOptions { get; set; } = new();

        public List<SelectListItem> UserOptions { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (PlannedStartDate.HasValue &&
                PlannedEndDate.HasValue &&
                PlannedEndDate < PlannedStartDate)
            {
                yield return new ValidationResult(
                    "Planned End Date cannot be earlier than Planned Start Date.",
                    new[]
                    {
                nameof(PlannedEndDate)
                    });
            }

            if (ActualStartDate.HasValue &&
                ActualEndDate.HasValue &&
                ActualEndDate < ActualStartDate)
            {
                yield return new ValidationResult(
                    "Actual End Date cannot be earlier than Actual Start Date.",
                    new[]
                    {
                nameof(ActualEndDate)
                    });
            }
        }

    }
}