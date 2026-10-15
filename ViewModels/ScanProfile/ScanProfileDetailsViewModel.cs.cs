namespace EVOLVE_VAPT.ViewModels.ScanProfile
{
    public class ScanProfileDetailsViewModel
    {
        public int ScanProfileID { get; set; }

        public int ScannerTypeID { get; set; }

        public string ProfileCode { get; set; } = string.Empty;

        public string ProfileName { get; set; } = string.Empty;

        public string ScannerTypeCode { get; set; } = string.Empty;

        public string ScannerTypeName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsIntrusive { get; set; }

        public bool RequiresExplicitApproval { get; set; }

        public int TimeoutMinutes { get; set; }

        public int MaxConcurrency { get; set; }

        public string? CreatedByEmail { get; set; }

        public DateTime CreatedDate { get; set; }


        // Options available for the scanner selected by this profile.
        public List<ScanProfileOptionViewModel> Options { get; set; }
            = new();
    }
}