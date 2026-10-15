using System.ComponentModel.DataAnnotations;

namespace EVOLVE_VAPT.ViewModels.Administration
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string UserID { get; set; } =
            string.Empty;


        public string Email { get; set; } =
            string.Empty;


        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } =
            string.Empty;


        [Required]
        [DataType(DataType.Password)]
        [Compare(
            nameof(NewPassword),
            ErrorMessage =
                "The passwords do not match.")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; } =
            string.Empty;
    }
}