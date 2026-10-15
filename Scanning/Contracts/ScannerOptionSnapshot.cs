namespace EVOLVE_VAPT.Scanning.Contracts
{
    /// <summary>
    /// Represents one effective scanner option captured
    /// inside ProfileOptionsSnapshot.
    /// </summary>
    public class ScannerOptionSnapshot
    {
        public int ScannerOptionID { get; set; }

        public string OptionCode { get; set; } = string.Empty;

        public string OptionName { get; set; } = string.Empty;

        public string ValueType { get; set; } = string.Empty;

        public string? OptionValue { get; set; }

        public bool IsIntrusive { get; set; }
    }
}