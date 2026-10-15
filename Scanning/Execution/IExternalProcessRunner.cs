namespace EVOLVE_VAPT.Scanning.Execution
{
    public interface IExternalProcessRunner
    {
        Task<ExternalProcessResult> RunAsync(
            ExternalProcessRequest request,
            CancellationToken cancellationToken);
    }
}