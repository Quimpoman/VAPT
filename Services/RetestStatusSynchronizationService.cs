using EVOLVE_VAPT.Models;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Services
{
    /// <summary>
    /// Keeps a Finding Retest workflow synchronized with
    /// the Scan Job that performs the actual verification.
    /// </summary>
    public class RetestStatusSynchronizationService
    {
        private readonly EVOLVE_VAPTContext _context;



        public RetestStatusSynchronizationService(
            EVOLVE_VAPTContext context)
        {
            _context = context;
        }


        public async Task SyncForScanJobAsync(
            long scanJobId,
            CancellationToken cancellationToken = default)
        {
            var retest =
                await _context.trn_FindingRetest

                    .Include(x =>
                        x.RetestStatus)

                    .FirstOrDefaultAsync(
                        x =>
                            x.RetestScanJobID ==
                                scanJobId &&
                            x.IsActive == true,
                        cancellationToken);


            /*
             * Most Scan Jobs are normal Assessment scans,
             * so having no linked Retest is expected.
             */
            if (retest == null)
            {
                return;
            }


            // Analyst-completed or cancelled Retests
            // should no longer be changed automatically.
            if (retest.RetestStatus.IsTerminal)
            {
                return;
            }


            var scanJob =
                await _context.trn_ScanJob

                    .Include(x =>
                        x.ScanJobStatus)

                    .AsNoTracking()

                    .FirstOrDefaultAsync(
                        x =>
                            x.ScanJobID ==
                                scanJobId,
                        cancellationToken);


            if (scanJob == null)
            {
                return;
            }


            var targetRetestStatusCode =
                scanJob
                    .ScanJobStatus
                    .ScanJobStatusCode
                switch
                {
                    "PENDING_APPROVAL" =>
                        "PENDING_SCAN",

                    "QUEUED" =>
                        "PENDING_SCAN",

                    "RUNNING" =>
                        "SCANNING",

                    "COMPLETED" =>
                        "AWAITING_REVIEW",

                    /*
                     * The analyst still needs to review why
                     * verification could not complete.
                     */
                    "FAILED" =>
                        "AWAITING_REVIEW",

                    "BLOCKED" =>
                        "AWAITING_REVIEW",

                    "REJECTED" =>
                        "CANCELLED",

                    "CANCELLED" =>
                        "CANCELLED",

                    _ =>
                        null
                };


            if (targetRetestStatusCode == null)
            {
                return;
            }


            // Avoid unnecessary UPDATE statements.
            if (string.Equals(
                    retest
                        .RetestStatus
                        .RetestStatusCode,
                    targetRetestStatusCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }


            var targetStatus =
                await _context.ref_RetestStatus
                    .FirstOrDefaultAsync(
                        x =>
                            x.RetestStatusCode ==
                                targetRetestStatusCode &&
                            x.IsActive == true,
                        cancellationToken);


            if (targetStatus == null)
            {
                throw new InvalidOperationException(
                    $"Retest status '{targetRetestStatusCode}' is not configured.");
            }


            retest.RetestStatusID =
                targetStatus.RetestStatusID;

            retest.EditedDate =
                DateTime.Now;


            await _context.SaveChangesAsync(
                cancellationToken);
        }


        /// <summary>
        /// Reconciles unfinished Retests when the application
        /// starts. This also helps recover from interrupted
        /// application/worker sessions.
        /// </summary>
        public async Task SyncAllActiveRetestsAsync(
            CancellationToken cancellationToken = default)
        {
            var scanJobIds =
                await _context.trn_FindingRetest

                    .Where(x =>
                        x.IsActive == true &&
                        !x.RetestStatus.IsTerminal &&
                        x.RetestScanJobID.HasValue)

                    .Select(x =>
                        x.RetestScanJobID!.Value)

                    .ToListAsync(
                        cancellationToken);


            foreach (var scanJobId in scanJobIds)
            {
                await SyncForScanJobAsync(
                    scanJobId,
                    cancellationToken);
            }
        }
    }
}