using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace EVOLVE_VAPT.ViewModels.Remediation
{
    public class RemediationFormViewModel
    {
        public long? FindingRemediationID { get; set; }

        public long FindingID { get; set; }


        public string FindingTitle { get; set; } =
            string.Empty;

        public string SeverityName { get; set; } =
            string.Empty;

        public string TargetValue { get; set; } =
            string.Empty;


        [Display(Name = "Assigned To")]
        public string? AssignedToUserID { get; set; }


        [Display(Name = "Target Date")]
        [DataType(DataType.Date)]
        public DateOnly? TargetDate { get; set; }


        [Display(Name = "Remediation Notes")]
        public string? RemediationNotes { get; set; }


        public List<SelectListItem>
            UserOptions
        { get; set; } = new();
    }
}