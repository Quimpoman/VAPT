using EVOLVE_VAPT.Scanning.Configuration;
using EVOLVE_VAPT.Scanning.Contracts;
using EVOLVE_VAPT.Services;
using Microsoft.Extensions.Options;

namespace EVOLVE_VAPT.Scanning
{
    /// <summary>
    /// Background execution engine for queued Scan Jobs.
    ///
    /// The worker performs orchestration only.
    /// Scanner-specific behavior remains inside adapters.
    /// </summary>
    public class ScanJobExecutionWorker :
        BackgroundService
    {
        private readonly IServiceScopeFactory
            _scopeFactory;

        private readonly ILogger<
            ScanJobExecutionWorker> _logger;


        private static readonly TimeSpan
            EmptyQueueDelay =
                TimeSpan.FromSeconds(3);

        private readonly string _workerInstanceId;

        private readonly ScanExecutionOptions _options;

        private readonly SemaphoreSlim _executionGate;


        public ScanJobExecutionWorker(
     IServiceScopeFactory scopeFactory,
     ILogger<ScanJobExecutionWorker> logger,
     IOptions<ScanExecutionOptions> options)
        {
            _scopeFactory =
                scopeFactory;

            _logger =
                logger;

            _options =
                options.Value;


            _workerInstanceId =
                $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

            _executionGate =
    new SemaphoreSlim(
        _options.MaxConcurrentJobs,
        _options.MaxConcurrentJobs);

        }


        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
            "Scan execution worker {WorkerInstanceId} started. " +
            "Maximum concurrent jobs: {MaxConcurrentJobs}. " +
            "Heartbeat interval: {HeartbeatSeconds}s. " +
            "Stale threshold: {StaleAfterMinutes}m.",
            _workerInstanceId,
            _options.MaxConcurrentJobs,
            _options.HeartbeatSeconds,
            _options.StaleAfterMinutes);

            /*
             * Reconcile unfinished Retests with their Scan Jobs
             * whenever the worker/application starts.
             */
            using (var scope =
                _scopeFactory.CreateScope())
            {
                var retestSyncService =
                    scope.ServiceProvider
                        .GetRequiredService<
                            RetestStatusSynchronizationService>();


                await retestSyncService
                    .SyncAllActiveRetestsAsync(
                        stoppingToken);
            }

            //marks stale running jobs as failed if they have not been updated in the last 2 minutes
            using (var scope = _scopeFactory.CreateScope())
            {
                var executionService =
                    scope.ServiceProvider
                        .GetRequiredService<
                            ScanJobExecutionService>();


                var recovered =
                    await executionService
                        .RecoverStaleRunningJobsAsync(
                            TimeSpan.FromMinutes(
                            _options.StaleAfterMinutes),
                            stoppingToken);


                if (recovered > 0)
                {
                    _logger.LogWarning(
                        "Recovered {Count} stale running Scan Job(s).",
                        recovered);
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ScannerExecutionContext? job;


                    /*
                     * DbContext and TargetService are scoped,
                     * while BackgroundService is singleton.
                     *
                     * Therefore the worker must create a DI
                     * scope instead of injecting DbContext directly.
                     */
                    using (var scope =
                        _scopeFactory.CreateScope())
                    {
                        var executionService =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    ScanJobExecutionService>();


                        job = await executionService
                            .ClaimNextQueuedJobAsync(
                                _workerInstanceId,
                                stoppingToken);
                    }


                    if (job == null)
                    {
                        await Task.Delay(
                            EmptyQueueDelay,
                            stoppingToken);

                        continue;
                    }


                    _logger.LogInformation(
                        "Claimed Scan Job {JobCode}.",
                        job.JobCode);


                    /*
                     * Third authorization checkpoint:
                     * request → approval → execution.
                     */
                    using (var scope =
                        _scopeFactory.CreateScope())
                    {
                        var executionService =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    ScanJobExecutionService>();


                        var validation =
                            await executionService
                                .ValidateExecutionAuthorizationAsync(
                                    job,
                                    stoppingToken);


                        if (!validation.Success)
                        {
                            await executionService
                                .BlockJobAsync(
                                    job.ScanJobID,
                                    validation.ErrorMessage
                                        ?? "Execution authorization failed.",
                                    stoppingToken);


                            _logger.LogWarning(
                                "Scan Job {JobCode} was blocked: {Reason}",
                                job.JobCode,
                                validation.ErrorMessage);


                            continue;
                        }
                    }


                    IScannerAdapter? adapter;


                    using (var scope =
                        _scopeFactory.CreateScope())
                    {
                        var resolver =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    ScannerAdapterResolver>();


                        // Keep the execution service in the same DI scope
                        // for the entire scanner execution lifecycle.
                        var executionService =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    ScanJobExecutionService>();

                        var normalizationService =
                            scope.ServiceProvider
                                .GetRequiredService<
                                    FindingNormalizationService>();
                    


                        adapter =
                            resolver.Resolve(
                                job.ScannerTypeCode);


                        if (adapter == null)
                        {
                            await executionService
                                .BlockJobAsync(
                                    job.ScanJobID,
                                    $"No scanner adapter is registered for '{job.ScannerTypeCode}'.",
                                    stoppingToken);


                            _logger.LogWarning(
                                "No adapter found for scanner type {ScannerType}.",
                                job.ScannerTypeCode);


                            continue;
                        }


                        using var timeoutSource =
                            CancellationTokenSource
                                .CreateLinkedTokenSource(
                                    stoppingToken);


                        timeoutSource.CancelAfter(
                            TimeSpan.FromMinutes(
                                job.TimeoutMinutes));


                        ScannerExecutionResult result;

                        using var heartbeatSource =
                            CancellationTokenSource
                                .CreateLinkedTokenSource(
                                    stoppingToken);


                        var heartbeatTask =
                            RunHeartbeatAsync(
                                job.ScanJobID,
                                heartbeatSource.Token);


                        try
                        {
                            result =
                                await adapter.ExecuteAsync(
                                    job,
                                    timeoutSource.Token);
                        }
                        catch (OperationCanceledException)
                            when (!stoppingToken
                                .IsCancellationRequested)
                        {
                            result =
                                ScannerExecutionResult.Failed(
                                    $"Scan execution exceeded the configured timeout of {job.TimeoutMinutes} minute(s).");
                        }
                        catch (OperationCanceledException)
                            when (stoppingToken
                                .IsCancellationRequested)
                        {
                            /*
                             * Try to leave a terminal record during
                             * graceful application shutdown.
                             */
                            result =
                                ScannerExecutionResult.Failed(
                                    "Scan execution was interrupted because the worker stopped.");
                        }
                        catch (Exception ex)
                        {
                            result =
                                ScannerExecutionResult.Failed(
                                    $"Unhandled scanner execution error: {ex.Message}");
                        }
                        finally
                        {
                            /*
                             * The heartbeat belongs to this scanner execution.
                             * Stop it as soon as the scanner has reached a
                             * terminal execution state.
                             */
                            heartbeatSource.Cancel();


                            try
                            {
                                await heartbeatTask;
                            }
                            catch (OperationCanceledException)
                                when (heartbeatSource
                                    .IsCancellationRequested)
                            {
                                /*
                                 * Expected when the worker intentionally
                                 * stops the heartbeat after scanner execution.
                                 */
                            }
                            catch (Exception ex)
                            {
                                /*
                                 * A heartbeat shutdown problem must not
                                 * overwrite the actual scanner result.
                                 */
                                _logger.LogWarning(
                                    ex,
                                    "Heartbeat task ended unexpectedly for Scan Job {JobCode}.",
                                    job.JobCode);
                            }
                        }


                        await executionService
                            .FinishJobAsync(
                                job.ScanJobID,
                                result,
                                CancellationToken.None);

                        /*
                         * Scanner success and finding normalization are
                         * separate processing stages.
                         *
                         * Raw output remains preserved even if normalization fails.
                         */
                        /*
                         * Assessment scans create Findings.
                         *
                         * Retest scans preserve their raw results for analyst
                         * comparison against an existing Finding instead.
                         */
                        if (result.Success && job.ScanJobPurposeCode == "ASSESSMENT")

                        {
                            var normalization =
                                await normalizationService
                                    .NormalizeScanResultAsync(
                                        job.ScanJobID,
                                        CancellationToken.None);


                            if (!normalization.Success)
                            {
                                _logger.LogWarning(
                                    "Finding normalization failed for Scan Job {JobCode}: {Reason}",
                                    job.JobCode,
                                    normalization.ErrorMessage);
                            }
                            else
                            {
                                _logger.LogInformation(
                                    "Normalized {FindingCount} finding(s) for Scan Job {JobCode}.",
                                    normalization.FindingCount,
                                    job.JobCode);
                            }
                        }


                        _logger.LogInformation(
                            "Scan Job {JobCode} finished. Success: {Success}",
                            job.JobCode,
                            result.Success);
                    }
                }
                catch (OperationCanceledException)
                    when (stoppingToken
                        .IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    /*
                     * One broken job must not permanently kill
                     * the background worker.
                     */
                    _logger.LogError(
                        ex,
                        "Unexpected error in Scan Job execution worker.");


                    try
                    {
                        await Task.Delay(
                            EmptyQueueDelay,
                            stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }


            _logger.LogInformation(
                "Scan Job execution worker stopped.");
        }
        private async Task RunHeartbeatAsync(
    long scanJobId,
    CancellationToken cancellationToken)
        {
            var interval =
                TimeSpan.FromSeconds(_options.HeartbeatSeconds);


            while (!cancellationToken
                .IsCancellationRequested)
            {
                await Task.Delay(
                    interval,
                    cancellationToken);


                /*
                 * Each heartbeat gets its own DI scope because
                 * DbContext is not thread-safe.
                 */
                using var scope =
                    _scopeFactory.CreateScope();


                var executionService =
                    scope.ServiceProvider
                        .GetRequiredService<
                            ScanJobExecutionService>();


                var updated =
                    await executionService
                        .UpdateHeartbeatAsync(
                            scanJobId,
                            _workerInstanceId,
                            cancellationToken);


                if (!updated)
                {
                    return;
                }
            }
        }
    }
}