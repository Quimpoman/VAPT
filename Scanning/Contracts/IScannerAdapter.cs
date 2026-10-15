namespace EVOLVE_VAPT.Scanning.Contracts
{
    /// <summary>
    /// Common contract implemented by every scanner integration.
    ///
    /// The worker communicates with this abstraction rather than
    /// knowing anything about Nmap, Nuclei, ZAP, etc.
    /// </summary>
    public interface IScannerAdapter
    {
        string ScannerTypeCode { get; }

        Task<ScannerExecutionResult> ExecuteAsync(
            ScannerExecutionContext context,
            CancellationToken cancellationToken);
    }
}