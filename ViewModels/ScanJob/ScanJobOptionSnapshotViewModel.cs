namespace EVOLVE_VAPT.ViewModels.ScanJob
{
    public class ScanJobOptionSnapshotViewModel
    {
        public int ScannerOptionID { get; set; }

        public string OptionCode { get; set; } = string.Empty;

        public string OptionName { get; set; } = string.Empty;

        public string ValueType { get; set; } = string.Empty;

        public string? OptionValue { get; set; }

        public bool IsIntrusive { get; set; }
    }
}