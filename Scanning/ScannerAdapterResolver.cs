using EVOLVE_VAPT.Scanning.Contracts;

namespace EVOLVE_VAPT.Scanning
{
    /// <summary>
    /// Resolves a scanner implementation from the
    /// ScannerTypeCode captured in the Scan Job.
    /// </summary>
    public class ScannerAdapterResolver
    {
        private readonly Dictionary<string, IScannerAdapter>
            _adapters;


        public ScannerAdapterResolver(
            IEnumerable<IScannerAdapter> adapters)
        {
            _adapters =
                adapters.ToDictionary(
                    x => x.ScannerTypeCode,
                    StringComparer.OrdinalIgnoreCase);
        }


        public IScannerAdapter? Resolve(
            string scannerTypeCode)
        {
            _adapters.TryGetValue(
                scannerTypeCode,
                out var adapter);


            return adapter;
        }
    }
}