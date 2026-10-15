namespace EVOLVE_VAPT.ViewModels.ScanProfile
{
    public class ScanProfileOptionViewModel
    {
        public int ScannerOptionID { get; set; }

        public string OptionCode { get; set; } = string.Empty;

        public string OptionName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string ValueType { get; set; } = string.Empty;

        public string? DefaultValue { get; set; }

        public bool IsIntrusive { get; set; }

        public bool IsConfigured { get; set; }

        public string? OptionValue { get; set; }
    }
}