using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.ViewModels.ScanJob;
using Microsoft.EntityFrameworkCore;
using EVOLVE_VAPT.ViewModels.Retest;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EVOLVE_VAPT.Services
{
    public class RetestService
    {
        private readonly EVOLVE_VAPTContext _context;
        private readonly ScanJobService _scanJobService;
        private readonly AuditTrailService _auditTrailService;



        public RetestService(
            EVOLVE_VAPTContext context,
            ScanJobService scanJobService,
            AuditTrailService auditTrailService)
        {
            _context =
                context;

            _scanJobService =
                scanJobService;

            _auditTrailService =
                auditTrailService;
        }


        public async Task<(
            bool Success,
            string? ErrorMessage,
            long? FindingRetestID,
            long? ScanJobID)>
            CreateRetestAsync(
                long remediationId,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            var remediation =
                await _context
                    .trn_FindingRemediation

                    .Include(x =>
                        x.RemediationStatus)

                    .Include(x =>
                        x.Finding)

                        .ThenInclude(x =>
                            x.FindingStatus)

                    .FirstOrDefaultAsync(x =>
                        x.FindingRemediationID ==
                            remediationId &&
                        x.IsActive == true);


            if (remediation == null)
            {
                return (
                    false,
                    "Remediation record could not be found.",
                    null,
                    null
                );
            }


            if (remediation
                .RemediationStatus
                .RemediationStatusCode !=
                "SUBMITTED_RETEST")
            {
                return (
                    false,
                    "Only remediation submitted for retesting can create a Retest.",
                    null,
                    null
                );
            }


            if (remediation
                .Finding
                .FindingStatus
                .FindingStatusCode !=
                "RETEST")
            {
                return (
                    false,
                    "The Finding is not currently awaiting retesting.",
                    null,
                    null
                );
            }


            var activeRetestExists =
                await _context
                    .trn_FindingRetest

                    .Include(x =>
                        x.RetestStatus)

                    .AnyAsync(x =>
                        x.FindingID ==
                            remediation.FindingID &&
                        x.IsActive == true &&
                        !x.RetestStatus.IsTerminal);


            if (activeRetestExists)
            {
                return (
                    false,
                    "This Finding already has an active Retest.",
                    null,
                    null
                );
            }


            /*
             * Reuse the original Scan Profile and Target.
             *
             * The new Scan Job still captures CURRENT approved
             * scope and profile settings and passes all normal
             * authorization checks.
             */
            var originalScanJob =
                await _context.trn_ScanJob
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ScanJobID ==
                            remediation
                                .Finding
                                .ScanJobID);


            if (originalScanJob == null)
            {
                return (
                    false,
                    "The original Scan Job could not be found.",
                    null,
                    null
                );
            }


            var pendingRetestStatus =
                await _context.ref_RetestStatus
                    .FirstOrDefaultAsync(x =>
                        x.RetestStatusCode ==
                            "PENDING_SCAN" &&
                        x.IsActive == true);


            if (pendingRetestStatus == null)
            {
                return (
                    false,
                    "Pending Scan Retest status is not configured.",
                    null,
                    null
                );
            }


            var previousCycle =
                await _context.trn_FindingRetest

                    .Where(x =>
                        x.FindingID ==
                            remediation.FindingID)

                    .Select(x =>
                        (int?)x.CycleNo)

                    .MaxAsync()
                    ?? 0;


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            try
            {
                var scanJobModel =
                    new ScanJobCreateViewModel
                    {
                        AssessmentTargetID =
                            originalScanJob
                                .AssessmentTargetID,

                        ScanProfileID =
                            originalScanJob
                                .ScanProfileID
                    };


                /*
                 * ScanJobService performs all of the same
                 * authorization, scope and profile validation
                 * used by ordinary scans.
                 */
                var scanResult =
                    await _scanJobService
                        .CreateScanJobAsync(
                            scanJobModel,
                            userId,
                            userName,
                            requestPath,
                            ipAddress,
                            scanJobPurposeCode:
                                "RETEST");


                if (!scanResult.Success ||
                    !scanResult.ScanJobID.HasValue)
                {
                    await transaction
                        .RollbackAsync();


                    return (
                        false,
                        scanResult.ErrorMessage
                            ?? "Unable to create the Retest Scan Job.",
                        null,
                        null
                    );
                }


                var now =
                    DateTime.Now;


                var retest =
                    new trn_FindingRetest
                    {
                        FindingID =
                            remediation.FindingID,

                        FindingRemediationID =
                            remediation
                                .FindingRemediationID,

                        CycleNo =
                            previousCycle + 1,

                        RetestStatusID =
                            pendingRetestStatus
                                .RetestStatusID,

                        RetestOutcomeID =
                            null,

                        RetestScanJobID =
                            scanResult.ScanJobID.Value,

                        RequestedByUserID =
                            userId,

                        RequestedDate =
                            now,

                        IsActive =
                            true,

                        CreatedBy =
                            userId,

                        CreatedDate =
                            now
                    };


                _context.trn_FindingRetest
                    .Add(retest);


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType:
                        "CREATE_RETEST",
                    module:
                        "Retesting",
                    tableName:
                        "trn_FindingRetest",
                    recordId:
                        retest
                            .FindingRetestID
                            .ToString(),
                    description:
                        $"Created Retest cycle {retest.CycleNo} for Finding {retest.FindingID}.",
                    oldValues: null,
                    newValues: new
                    {
                        retest.FindingRetestID,
                        retest.FindingID,
                        retest.FindingRemediationID,
                        retest.CycleNo,
                        RetestStatus =
                            "PENDING_SCAN",
                        retest.RetestScanJobID
                    },
                    requestPath:
                        requestPath,
                    ipAddress:
                        ipAddress,
                    isSuccessful:
                        true
                );


                await _context.SaveChangesAsync();

                await transaction.CommitAsync();


                return (
                    true,
                    null,
                    retest.FindingRetestID,
                    scanResult.ScanJobID
                );
            }
            catch
            {
                await transaction.RollbackAsync();

                throw;
            }
        }

        public async Task<List<RetestListViewModel>> GetRetestsAsync()
        {
            return await _context.vwGet_FindingRetestData
                .AsNoTracking()

                .Where(x =>
                    x.IsActive == true)

                .OrderByDescending(x =>
                    x.RequestedDate)

                .Select(x =>
                    new RetestListViewModel
                    {
                        FindingRetestID =
                            x.FindingRetestID,

                        FindingID =
                            x.FindingID,

                        CycleNo =
                            x.CycleNo,

                        FindingTitle =
                            x.FindingTitle,

                        SeverityCode =
                            x.SeverityCode,

                        SeverityName =
                            x.SeverityName,

                        AssessmentCode =
                            x.AssessmentCode,

                        TargetValue =
                            x.TargetValue,

                        RetestStatusCode =
                            x.RetestStatusCode,

                        RetestStatusName =
                            x.RetestStatusName,

                        RetestOutcomeName =
                            x.RetestOutcomeName,

                        RetestJobCode =
                            x.RetestJobCode,

                        ScanJobStatusCode =
                            x.ScanJobStatusCode,

                        ScanJobStatusName =
                            x.ScanJobStatusName,

                        RequestedByEmail =
                            x.RequestedByEmail,

                        RequestedDate =
                            x.RequestedDate
                    })

                .ToListAsync();
        }

        public async Task<RetestDetailsViewModel?> GetRetestDetailsAsync(
        long findingRetestId)
        {
            var retest =
                await _context.vwGet_FindingRetestData
                    .AsNoTracking()

                    .FirstOrDefaultAsync(x =>
                        x.FindingRetestID ==
                            findingRetestId &&
                        x.IsActive == true);


            if (retest == null)
            {
                return null;
            }


            var finding =
                await _context.vwGet_FindingData
                    .AsNoTracking()

                    .FirstOrDefaultAsync(x =>
                        x.FindingID ==
                            retest.FindingID);


            if (finding == null)
            {
                return null;
            }


            var remediation =
                await _context.vwGet_FindingRemediationData
                    .AsNoTracking()

                    .FirstOrDefaultAsync(x =>
                        x.FindingRemediationID ==
                            retest.FindingRemediationID);


            if (remediation == null)
            {
                return null;
            }


            trn_ScanResult? scanResult =
                null;


            if (retest.RetestScanJobID.HasValue)
            {
                scanResult =
                    await _context.trn_ScanResult
                        .AsNoTracking()

                        .FirstOrDefaultAsync(x =>
                            x.ScanJobID ==
                                retest.RetestScanJobID.Value);
            }


            return new RetestDetailsViewModel
            {
                FindingRetestID =
                    retest.FindingRetestID,

                FindingID =
                    retest.FindingID,

                FindingRemediationID =
                    retest.FindingRemediationID,

                CycleNo =
                    retest.CycleNo,


                FindingTitle =
                    finding.Title,

                SourceFindingIdentifier =
                    finding.SourceFindingIdentifier,

                SeverityCode =
                    finding.SeverityCode,

                SeverityName =
                    finding.SeverityName,

                TargetValue =
                    finding.TargetValue,

                FindingDescription =
                    finding.Description,

                OriginalEvidence =
                    finding.Evidence,

                Recommendation =
                    finding.Recommendation,

                Reference =
                    finding.Reference,


                AssessmentCode =
                    retest.AssessmentCode,

                AssessmentName =
                    retest.AssessmentName,


                OriginalJobCode =
                    retest.OriginalJobCode,

                RetestJobCode =
                    retest.RetestJobCode,


                RetestStatusCode =
                    retest.RetestStatusCode,

                RetestStatusName =
                    retest.RetestStatusName,

                RetestOutcomeCode =
                    retest.RetestOutcomeCode,

                RetestOutcomeName =
                    retest.RetestOutcomeName,


                ScanJobStatusCode =
                    retest.ScanJobStatusCode,

                ScanJobStatusName =
                    retest.ScanJobStatusName,


                RemediationNotes =
                    remediation.RemediationNotes,

                RemediationTargetDate =
                    remediation.TargetDate,

                SubmissionNotes =
                    remediation.SubmissionNotes,


                ScannerTypeCode =
                    scanResult?.ScannerTypeCode,

                ScannerVersion =
                    scanResult?.ScannerVersion,

                ScanSuccessful =
                    scanResult?.IsSuccessful,

                OutputFormat =
                    scanResult?.OutputFormat,

                RawOutput =
                    scanResult?.RawOutput,

                ScanErrorMessage =
                    scanResult?.ErrorMessage,


                RequestedByEmail =
                    retest.RequestedByEmail,

                RequestedDate =
                    retest.RequestedDate,

                ReviewedByEmail =
                    retest.ReviewedByEmail,

                ReviewedDate =
                    retest.ReviewedDate,

                ReviewNotes =
                    retest.ReviewNotes
            };
        }

        public async Task PopulateOutcomeOptionsAsync(RetestReviewViewModel model)
        {
            model.OutcomeOptions =
                await _context.ref_RetestOutcome

                    .Where(x =>
                        x.IsActive == true)

                    .OrderBy(x =>
                        x.SortOrder)

                    .Select(x =>
                        new SelectListItem
                        {
                            Value =
                                x.RetestOutcomeID
                                    .ToString(),

                            Text =
                                x.RetestOutcomeName
                        })

                    .ToListAsync();
        }

        public async Task<(bool Success, string? ErrorMessage)>
    ReviewRetestAsync(
        RetestReviewViewModel model,
        string userId,
        string userName,
        string? requestPath,
        string? ipAddress)
        {
            if (!model.RetestOutcomeID.HasValue)
            {
                return (
                    false,
                    "A Retest outcome is required."
                );
            }


            if (string.IsNullOrWhiteSpace(
                    model.ReviewNotes))
            {
                return (
                    false,
                    "Review notes are required."
                );
            }


            var retest =
                await _context.trn_FindingRetest

                    .Include(x =>
                        x.RetestStatus)

                    .Include(x =>
                        x.Finding)

                        .ThenInclude(x =>
                            x.FindingStatus)

                    .FirstOrDefaultAsync(x =>
                        x.FindingRetestID ==
                            model.FindingRetestID &&
                        x.IsActive == true);


            if (retest == null)
            {
                return (
                    false,
                    "Retest could not be found."
                );
            }


            if (retest
                .RetestStatus
                .RetestStatusCode !=
                "AWAITING_REVIEW")
            {
                return (
                    false,
                    "Only Retests awaiting review can receive an outcome."
                );
            }


            var outcome =
                await _context.ref_RetestOutcome
                    .FirstOrDefaultAsync(x =>
                        x.RetestOutcomeID ==
                            model.RetestOutcomeID.Value &&
                        x.IsActive == true);


            if (outcome == null)
            {
                return (
                    false,
                    "The selected Retest outcome is invalid."
                );
            }


            if (!retest.RetestScanJobID.HasValue)
            {
                return (
                    false,
                    "The Retest does not have a linked Scan Job."
                );
            }


            var scanJob =
                await _context.trn_ScanJob

                    .Include(x =>
                        x.ScanJobStatus)

                    .AsNoTracking()

                    .FirstOrDefaultAsync(x =>
                        x.ScanJobID ==
                            retest.RetestScanJobID.Value);


            if (scanJob == null)
            {
                return (
                    false,
                    "The Retest Scan Job could not be found."
                );
            }


            var scanStatus =
                scanJob
                    .ScanJobStatus
                    .ScanJobStatusCode;


            /*
             * A positive security conclusion requires a successfully
             * completed verification scan.
             *
             * Failed/Blocked scans may only result in
             * Unable to Verify.
             */
            if (scanStatus != "COMPLETED" &&
                outcome.RetestOutcomeCode !=
                    "UNABLE_TO_VERIFY")
            {
                return (
                    false,
                    "Only Unable to Verify may be selected when the Retest Scan did not complete successfully."
                );
            }


            if (scanStatus != "COMPLETED" &&
                scanStatus != "FAILED" &&
                scanStatus != "BLOCKED")
            {
                return (
                    false,
                    "The linked Retest Scan has not reached a reviewable state."
                );
            }


            var completedRetestStatus =
                await _context.ref_RetestStatus
                    .FirstOrDefaultAsync(x =>
                        x.RetestStatusCode ==
                            "COMPLETED" &&
                        x.IsActive == true);


            if (completedRetestStatus == null)
            {
                return (
                    false,
                    "Completed Retest status is not configured."
                );
            }
            string targetFindingStatusCode;


            switch (outcome.RetestOutcomeCode)
            {
                case "FIXED":

                    targetFindingStatusCode =
                        "CLOSED";

                    break;


                case "STILL_VULNERABLE":

                    targetFindingStatusCode =
                        "CONFIRMED";

                    break;


                case "PARTIALLY_FIXED":

                    targetFindingStatusCode =
                        "CONFIRMED";

                    break;


                case "UNABLE_TO_VERIFY":

                    targetFindingStatusCode =
                        "RETEST";

                    break;


                default:

                    return (
                        false,
                        "The selected Retest outcome is not supported."
                    );
            }


            var targetFindingStatus =
                await _context.ref_FindingStatus
                    .FirstOrDefaultAsync(x =>
                        x.FindingStatusCode ==
                            targetFindingStatusCode &&
                        x.IsActive == true);


            if (targetFindingStatus == null)
            {
                return (
                    false,
                    $"Finding status '{targetFindingStatusCode}' is not configured."
                );
            }


            var now =
                DateTime.Now;


            var previousFindingStatus =
                retest
                    .Finding
                    .FindingStatus
                    .FindingStatusCode;


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            try
            {
                retest.RetestStatusID =
                    completedRetestStatus
                        .RetestStatusID;

                retest.RetestOutcomeID =
                    outcome.RetestOutcomeID;

                retest.ReviewedByUserID =
                    userId;

                retest.ReviewedDate =
                    now;

                retest.ReviewNotes =
                    model.ReviewNotes.Trim();

                retest.EditedBy =
                    userId;

                retest.EditedDate =
                    now;


                retest.Finding.FindingStatusID =
                    targetFindingStatus
                        .FindingStatusID;

                retest.Finding.EditedBy =
                    userId;

                retest.Finding.EditedDate =
                    now;


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType:
                        "RETEST_REVIEW",
                    module:
                        "Retesting",
                    tableName:
                        "trn_FindingRetest",
                    recordId:
                        retest
                            .FindingRetestID
                            .ToString(),
                    description:
                        $"Completed Retest cycle {retest.CycleNo} for Finding {retest.FindingID} with outcome {outcome.RetestOutcomeCode}.",
                    oldValues: new
                    {
                        RetestStatus =
                            "AWAITING_REVIEW",

                        FindingStatus =
                            previousFindingStatus
                    },
                    newValues: new
                    {
                        RetestStatus =
                            "COMPLETED",

                        RetestOutcome =
                            outcome.RetestOutcomeCode,

                        FindingStatus =
                            targetFindingStatusCode,

                        retest.ReviewedByUserID,
                        retest.ReviewedDate,
                        retest.ReviewNotes
                    },
                    requestPath: requestPath,
                    ipAddress: ipAddress,
                    isSuccessful: true
                );


                await _context.SaveChangesAsync();

                await transaction.CommitAsync();


                return (
                    true,
                    null
                );
            }
            catch
            {
                await transaction.RollbackAsync();

                throw;
            }
        }
    }

}