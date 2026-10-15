using System.Text.Json;
using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.Scanning.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Services
{
    /// <summary>
    /// Handles worker-side Scan Job lifecycle operations.
    ///
    /// MVC controllers never execute scanners directly.
    /// </summary>
    public class ScanJobExecutionService
    {
        private readonly EVOLVE_VAPTContext _context;
        private readonly TargetService _targetService;
        private readonly RetestStatusSynchronizationService
    _retestStatusSynchronizationService;




        public ScanJobExecutionService(
     EVOLVE_VAPTContext context,
     TargetService targetService,
     RetestStatusSynchronizationService
         retestStatusSynchronizationService)
        {
            _context =
                context;

            _targetService =
                targetService;

            _retestStatusSynchronizationService =
                retestStatusSynchronizationService;
        }


        /// <summary>
        /// Atomically claims the oldest Queued job.
        ///
        /// UPDLOCK + READPAST prevents two workers from
        /// claiming the same row when multiple workers
        /// are introduced later.
        /// </summary>
        public async Task<ScannerExecutionContext?> ClaimNextQueuedJobAsync(string workerInstanceId, CancellationToken cancellationToken)
        {
            var queuedStatus =
                await _context.ref_ScanJobStatus
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.ScanJobStatusCode == "QUEUED" &&
                            x.IsActive == true,
                        cancellationToken);


            var runningStatus =
                await _context.ref_ScanJobStatus
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.ScanJobStatusCode == "RUNNING" &&
                            x.IsActive == true,
                        cancellationToken);


            if (queuedStatus == null ||
                runningStatus == null)
            {
                throw new InvalidOperationException(
                    "Queued or Running Scan Job status is not configured.");
            }


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        cancellationToken);


            /*
             * UPDLOCK reserves the selected job for this worker.
             *
             * READPAST causes another worker to skip a row that
             * has already been locked by this worker.
             */
            var job =
                await _context.trn_ScanJob
                    .FromSqlInterpolated($"""
                        SELECT TOP (1) *
                        FROM dbo.trn_ScanJob
                            WITH (UPDLOCK, READPAST, ROWLOCK)
                        WHERE ScanJobStatusID =
                            {queuedStatus.ScanJobStatusID}
                        ORDER BY RequestedDate, ScanJobID
                        """)
                    .FirstOrDefaultAsync(
                        cancellationToken);


            if (job == null)
            {
                await transaction.CommitAsync(
                    cancellationToken);

                return null;
            }
            var jobPurpose = await _context.ref_ScanJobPurpose
        .AsNoTracking()
        .FirstOrDefaultAsync(
            x =>
                x.ScanJobPurposeID ==
                    job.ScanJobPurposeID &&
                x.IsActive == true,
            cancellationToken);

            if (jobPurpose == null)
            {
                throw new InvalidOperationException(
                    "Scan Job purpose is not configured.");
            }


            var now = DateTime.Now;


            // Claim the job before releasing the database lock.
            job.ScanJobStatusID =
                runningStatus.ScanJobStatusID;

            job.StartedDate =
                now;

            // Identify the worker that owns this execution.
            job.WorkerInstanceID =
                workerInstanceId;


            // Initial heartbeat is recorded as part of the atomic claim.
            job.LastHeartbeatDate =
                now;


            job.ExecutionAttemptCount += 1;

            await _context.SaveChangesAsync(
                cancellationToken);

            await _retestStatusSynchronizationService
    .SyncForScanJobAsync(
        job.ScanJobID,
        cancellationToken);


            var context =
                new ScannerExecutionContext
                {
                    ScanJobID =
                        job.ScanJobID,

                    JobCode =
                        job.JobCode,

                    AssessmentTargetID =
                        job.AssessmentTargetID,

                    ScannerTypeCode =
                        job.ScannerTypeCodeSnapshot,

                    TargetTypeCode =
                        job.TargetTypeCodeSnapshot,

                    TargetValue =
                        job.TargetValueSnapshot,

                    ScopeSnapshotJson =
                        job.ScopeSnapshot,

                    ProfileOptionsSnapshotJson =
                        job.ProfileOptionsSnapshot,

                    IsIntrusive =
                        job.IsIntrusiveSnapshot,

                    TimeoutMinutes =
                        job.TimeoutMinutesSnapshot,

                    MaxConcurrency =
                        job.MaxConcurrencySnapshot,
                    
                    ScanJobPurposeCode =
                        jobPurpose.ScanJobPurposeCode,
                };



            await transaction.CommitAsync(
                cancellationToken);


            return context;
        }


        /// <summary>
        /// Performs the execution-time authorization check.
        ///
        /// The job is already claimed, but no scanner has
        /// executed yet.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)>
            ValidateExecutionAuthorizationAsync(
                ScannerExecutionContext job,
                CancellationToken cancellationToken)
        {
            /*
             * Authorization may have expired or been revoked
             * after the job entered the queue.
             */
            var currentlyAuthorized =
                await _targetService
                    .IsTargetCurrentlyAuthorizedAsync(
                        job.AssessmentTargetID);


            if (!currentlyAuthorized)
            {
                return (
                    false,
                    "Target authorization is no longer valid."
                );
            }


            var currentTarget =
                await _context.vwGet_AssessmentTargetData
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.AssessmentTargetID ==
                                job.AssessmentTargetID &&
                            x.IsActive == true,
                        cancellationToken);


            if (currentTarget == null)
            {
                return (
                    false,
                    "Assessment Target is no longer active."
                );
            }


            var assessmentActive =
                await _context.mst_Assessment
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.AssessmentID ==
                                currentTarget.AssessmentID &&
                            x.IsActive == true,
                        cancellationToken);


            if (!assessmentActive)
            {
                return (
                    false,
                    "The parent Assessment is no longer active."
                );
            }


            /*
             * A queued job must not silently scan a different
             * target after the target was edited and re-approved.
             */
            if (!string.Equals(
                    currentTarget.TargetTypeCode,
                    job.TargetTypeCode,
                    StringComparison.OrdinalIgnoreCase)
                ||
                !string.Equals(
                    currentTarget.TargetValue,
                    job.TargetValue,
                    StringComparison.Ordinal))
            {
                return (
                    false,
                    "The target changed after this Scan Job was created."
                );
            }


            List<ScannerScopeRuleSnapshot>
                snapshotScope;


            try
            {
                snapshotScope =
                    JsonSerializer.Deserialize<
                        List<ScannerScopeRuleSnapshot>>(
                            job.ScopeSnapshotJson)
                    ?? new();
            }
            catch (JsonException)
            {
                return (
                    false,
                    "The Scan Job contains an invalid scope snapshot."
                );
            }


            if (!snapshotScope.Any(x =>
                    string.Equals(
                        x.RuleTypeCode,
                        "INCLUDE",
                        StringComparison.OrdinalIgnoreCase)))
            {
                return (
                    false,
                    "The Scan Job does not contain an Include scope rule."
                );
            }


            var currentScope =
                await _context
                    .vwGet_AssessmentScopeRuleData
                    .AsNoTracking()
                    .Where(x =>
                        x.AssessmentTargetID ==
                            job.AssessmentTargetID &&
                        x.IsActive == true)
                    .Select(x => new
                    {
                        x.AssessmentScopeRuleID,

                        RuleTypeCode =
                            x.ScopeRuleTypeCode,

                        x.RuleValue
                    })
                    .ToListAsync(
                        cancellationToken);


            /*
             * Compare exact rule identity + type + value.
             *
             * If the approved scope changed after job creation,
             * the old job is blocked even if the target was later
             * approved again.
             */
            var snapshotKeys =
                snapshotScope
                    .Select(x =>
                        $"{x.AssessmentScopeRuleID}|{x.RuleTypeCode}|{x.RuleValue}")
                    .OrderBy(x => x)
                    .ToList();


            var currentKeys =
                currentScope
                    .Select(x =>
                        $"{x.AssessmentScopeRuleID}|{x.RuleTypeCode}|{x.RuleValue}")
                    .OrderBy(x => x)
                    .ToList();


            if (!snapshotKeys.SequenceEqual(
                    currentKeys,
                    StringComparer.Ordinal))
            {
                return (
                    false,
                    "The approved scope changed after this Scan Job was created."
                );
            }


            return (
                true,
                null
            );
        }


        public async Task BlockJobAsync(
            long scanJobId,
            string reason,
            CancellationToken cancellationToken)
        {
            var blockedStatus =
                await GetStatusAsync(
                    "BLOCKED",
                    cancellationToken);


            var job =
                await GetRunningJobAsync(
                    scanJobId,
                    cancellationToken);


            if (job == null)
            {
                return;
            }


            job.ScanJobStatusID =
                blockedStatus.ScanJobStatusID;

            job.CompletedDate =
                DateTime.Now;

            job.WorkerInstanceID = null;

            job.FailureReason =
                Limit(reason, 2000);


            await _context.SaveChangesAsync(
                cancellationToken);
            
            await _retestStatusSynchronizationService
    .SyncForScanJobAsync(
        job.ScanJobID,
        cancellationToken);
        }


        public async Task FinishJobAsync(
            long scanJobId,
            ScannerExecutionResult result,
            CancellationToken cancellationToken)
        {
            var finalStatus =
                await GetStatusAsync(
                    result.Success
                        ? "COMPLETED"
                        : "FAILED",
                    cancellationToken);


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        cancellationToken);


            var job =
                await GetRunningJobAsync(
                    scanJobId,
                    cancellationToken);


            if (job == null)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                return;
            }


            var existingResult =
                await _context.trn_ScanResult
                    .AnyAsync(
                        x =>
                            x.ScanJobID ==
                                scanJobId,
                        cancellationToken);


            if (!existingResult)
            {
                _context.trn_ScanResult.Add(
                    new trn_ScanResult
                    {
                        ScanJobID =
                            scanJobId,

                        ScannerTypeCode =
                            job.ScannerTypeCodeSnapshot,

                        ScannerVersion =
                            result.ScannerVersion,

                        ExitCode =
                            result.ExitCode,

                        IsSuccessful =
                            result.Success,

                        OutputFormat =
                            result.OutputFormat,

                        RawOutput =
                            result.RawOutput,

                        ErrorMessage =
                            Limit(
                                result.ErrorMessage,
                                2000),

                        CreatedDate =
                            DateTime.Now
                    });
            }


            job.ScanJobStatusID =
                finalStatus.ScanJobStatusID;

            job.CompletedDate =
                DateTime.Now;

            job.WorkerInstanceID =
                null;

            job.FailureReason =
                result.Success
                    ? null
                    : Limit(
                        result.ErrorMessage,
                        2000);


            await _context.SaveChangesAsync(
                cancellationToken);

            await _retestStatusSynchronizationService
    .SyncForScanJobAsync(
        job.ScanJobID,
        cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
        }


        private async Task<ref_ScanJobStatus>
            GetStatusAsync(
                string statusCode,
                CancellationToken cancellationToken)
        {
            var status =
                await _context.ref_ScanJobStatus
                    .FirstOrDefaultAsync(
                        x =>
                            x.ScanJobStatusCode ==
                                statusCode &&
                            x.IsActive == true,
                        cancellationToken);


            return status
                ?? throw new InvalidOperationException(
                    $"Scan Job status '{statusCode}' is not configured.");
        }


        private async Task<trn_ScanJob?>
            GetRunningJobAsync(
                long scanJobId,
                CancellationToken cancellationToken)
        {
            return await _context.trn_ScanJob
                .Include(x => x.ScanJobStatus)
                .FirstOrDefaultAsync(
                    x =>
                        x.ScanJobID ==
                            scanJobId &&
                        x.ScanJobStatus
                            .ScanJobStatusCode ==
                            "RUNNING",
                    cancellationToken);
        }


        private static string? Limit(
            string? value,
            int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }


            return value.Length <= maximumLength
                ? value
                : value[..maximumLength];
        }

        public async Task<bool> UpdateHeartbeatAsync(long scanJobId,string workerInstanceId,CancellationToken cancellationToken)
        {
            var job =
                await _context.trn_ScanJob
                    .Include(x =>
                        x.ScanJobStatus)
                    .FirstOrDefaultAsync(
                        x =>
                            x.ScanJobID ==
                                scanJobId,
                        cancellationToken);


            if (job == null)
            {
                return false;
            }


            /*
             * A worker may only refresh a job that it currently
             * owns and that is still running.
             */
            if (job.ScanJobStatus.ScanJobStatusCode !=
                    "RUNNING" ||
                !string.Equals(
                    job.WorkerInstanceID,
                    workerInstanceId,
                    StringComparison.Ordinal))
            {
                return false;
            }


            job.LastHeartbeatDate =
                DateTime.Now;


            await _context.SaveChangesAsync(
                cancellationToken);


            return true;
        }


        public async Task<int> RecoverStaleRunningJobsAsync(TimeSpan staleAfter, CancellationToken cancellationToken)
        {
            var runningStatus =
                await GetStatusAsync(
                    "RUNNING",
                    cancellationToken);


            var failedStatus =
                await GetStatusAsync(
                    "FAILED",
                    cancellationToken);


            var cutoff =
                DateTime.Now.Subtract(
                    staleAfter);


            var staleJobs =
                await _context.trn_ScanJob

                    .Where(x =>
                        x.ScanJobStatusID ==
                            runningStatus.ScanJobStatusID
                        &&
                        (
                            !x.LastHeartbeatDate.HasValue
                            ||
                            x.LastHeartbeatDate <
                                cutoff
                        ))

                    .ToListAsync(
                        cancellationToken);


            if (staleJobs.Count == 0)
            {
                return 0;
            }


            var now =
                DateTime.Now;


            foreach (var job in staleJobs)
            {
                job.ScanJobStatusID =
                    failedStatus.ScanJobStatusID;

                job.CompletedDate =
                    now;

                job.FailureReason =
                    "Execution was marked as failed because the worker heartbeat expired.";

                job.WorkerInstanceID =
                    null;

                job.EditedDate =
                    now;
            }


            await _context.SaveChangesAsync(
                cancellationToken);


            /*
             * Retest jobs must also reflect the failed
             * Scan Job state.
             */
            foreach (var job in staleJobs)
            {
                await _retestStatusSynchronizationService
                    .SyncForScanJobAsync(
                        job.ScanJobID,
                        cancellationToken);
            }


            return staleJobs.Count;
        }
    }
}