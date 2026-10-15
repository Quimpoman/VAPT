using System.ComponentModel.DataAnnotations;

namespace EVOLVE_VAPT.ViewModels.Administration
{
    public class UserRoleEditViewModel
    {
        [Required]
        public string UserID { get; set; } =
            string.Empty;


        public string Email { get; set; } =
            string.Empty;


        [Required]
        [Display(Name = "Role")]
        public string SelectedRole { get; set; } =
            string.Empty;


        public IReadOnlyList<string> AvailableRoles
        {
            get;
            set;
        } =
            Array.Empty<string>();
    }
}