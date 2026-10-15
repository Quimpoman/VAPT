namespace EVOLVE_VAPT.ViewModels.Administration
{
    public class UserListItemViewModel
    {
        public string UserID { get; set; } =
            string.Empty;

        public string Email { get; set; } =
            string.Empty;

        public string UserName { get; set; } =
            string.Empty;

        public bool EmailConfirmed { get; set; }

        public bool IsAdministrativelyDisabled { get; set; }

        public bool IsTemporarilyLocked { get; set; }

        public string Status { get; set; } =
            string.Empty;

        public IReadOnlyList<string> Roles { get; set; } =
            Array.Empty<string>();
    }
}