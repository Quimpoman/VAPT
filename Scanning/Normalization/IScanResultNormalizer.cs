namespace EVOLVE_VAPT.Scanning.Normalization
{
    public interface IScanResultNormalizer
    {
        string ScannerTypeCode { get; }


        IReadOnlyList<NormalizedFinding> Normalize(
            string rawOutput);
    }
}