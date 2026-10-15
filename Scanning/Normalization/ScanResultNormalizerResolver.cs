namespace EVOLVE_VAPT.Scanning.Normalization
{
    public class ScanResultNormalizerResolver
    {
        private readonly Dictionary<
            string,
            IScanResultNormalizer> _normalizers;

    
        public ScanResultNormalizerResolver(
            IEnumerable<IScanResultNormalizer>
                normalizers)
        {
            _normalizers =
                normalizers.ToDictionary(
                    x => x.ScannerTypeCode,
                    StringComparer.OrdinalIgnoreCase);
        }


        public IScanResultNormalizer? Resolve(
            string scannerTypeCode)
        {
            _normalizers.TryGetValue(
                scannerTypeCode,
                out var normalizer);


            return normalizer;
        }
    }
}