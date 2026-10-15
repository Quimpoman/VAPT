using System.ComponentModel.DataAnnotations;

namespace EVOLVE_VAPT.ViewModels.Administration
{
    public class UserCreateViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } =
            string.Empty;


        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; } =
            string.Empty;


        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Initial Password")]
        public string InitialPassword { get; set; } =
            string.Empty;


        [Required]
        [DataType(DataType.Password)]
        [Compare(
            nameof(InitialPassword),
            ErrorMessage =
                "The passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } =
            string.Empty;


        public IReadOnlyList<string> AvailableRoles
        {
            get;
            set;
        } =
            Array.Empty<string>();
    }
}