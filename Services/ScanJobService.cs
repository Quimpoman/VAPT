using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.ViewModels.ScanJob;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Contracts;
using System.Text.Json;
using EVOLVE_VAPT.Services.Results;
using Microsoft.EntityFrameworkCore.Storage;

namespace EVOLVE_VAPT.Services
{
    public class ScanJobService
    {
        private readonly EVOLVE_VAPTContext _context;
        private readonly AuditTrailService _auditTrailService;
        private readonly TargetService _targetService;
        private readonly RetestStatusSynchronizationService
    _retestStatusSynchronizationService;


        public ScanJobService(
            EVOLVE_VAPTContext context,
            AuditTrailService auditTrailService,
            TargetService targetService,
            RetestStatusSynchronizationService
    retestStatusSynchronizationService)
        {
            _context = context;
            _auditTrailService = auditTrailService;
            _targetService = targetService;
            _retestStatusSynchronizationService =
    retestStatusSynchronizationService;
        }
        /// <summary>
        /// Returns Scan Jobs newest first.
        /// </summary>
        public async Task<List<ScanJobListViewModel>> GetScanJobsAsync()
        {
            return await _context.vwGet_ScanJobData
                .AsNoTracking()

                .OrderByDescending(x =>
                    x.RequestedDate)

                .Select(x =>
                    new ScanJobListViewModel
                    {
                        ScanJobID =
                            x.ScanJobID,

                        JobCode =
                            x.JobCode,

                        AssessmentCode =
                            x.AssessmentCode,

                        AssessmentName =
                            x.AssessmentName,

                        TargetName =
                            x.TargetName,

                        TargetValue =
                            x.TargetValueSnapshot,

                        ScanProfileName =
                            x.ScanProfileName,

                        ScannerTypeCode =
                            x.ScannerTypeCodeSnapshot,

                        ScanJobStatusCode =
                            x.ScanJobStatusCode,

                        ScanJobStatusName =
                            x.ScanJobStatusName,

                        RequiresExplicitApproval =
                            x.RequiresExplicitApproval,

                        RequestedByEmail =
                            x.RequestedByEmail,

                        RequestedDate =
                            x.RequestedDate
                    })

                .ToListAsync();
        }

        /// <summary>
        /// Returns the most recent Scan Jobs associated
        /// with one Assessment.
        /// </summary>
        public async Task<List<ScanJobListViewModel>>
            GetAssessmentScanJobsAsync(
                int assessmentId,
                int take = 5)
        {
            /*
             * Assessment Details is only a summary.
             * Prevent an unexpectedly large request from turning
             * this into another full Scan Job listing.
             */
            take =
                Math.Clamp(
                    take,
                    1,
                    20);


            return await _context.vwGet_ScanJobData
                .AsNoTracking()

                .Where(x =>
                    x.AssessmentID ==
                    assessmentId)

                .OrderByDescending(x =>
                    x.RequestedDate)

                .ThenByDescending(x =>
                    x.ScanJobID)

                .Take(take)

                .Select(x =>
                    new ScanJobListViewModel
                    {
                        ScanJobID =
                            x.ScanJobID,

                        JobCode =
                            x.JobCode,

                        AssessmentCode =
                            x.AssessmentCode,

                        AssessmentName =
                            x.AssessmentName,

                        TargetName =
                            x.TargetName,

                        TargetValue =
                            x.TargetValueSnapshot,

                        ScanProfileName =
                            x.ScanProfileName,

                        ScannerTypeCode =
                            x.ScannerTypeCodeSnapshot,

                        ScanJobStatusCode =
                            x.ScanJobStatusCode,

                        ScanJobStatusName =
                            x.ScanJobStatusName,

                        RequiresExplicitApproval =
                            x.RequiresExplicitApproval,

                        RequestedByEmail =
                            x.RequestedByEmail,

                        RequestedDate =
                            x.RequestedDate
                    })

                .ToListAsync();
        }

        /// <summary>
        /// Loads currently usable Targets and active Scan Profiles
        /// for Scan Job creation.
        ///
        /// The service will still revalidate everything during POST.
        /// </summary>
        public async Task PopulateCreateOptionsAsync(
            ScanJobCreateViewModel model)
        {
            var today = DateTime.Today;


            /*
             * Only show targets that appear eligible right now.
             *
             * This improves the UI, but it is NOT our security control.
             * CreateScanJobAsync performs the authoritative validation.
             */
            var eligibleTargets =
                await _context.vwGet_AssessmentTargetData

                    .AsNoTracking()

                    .Where(x =>
                        x.IsActive == true &&

                        x.AuthorizationStatusCode ==
                            "APPROVED" &&

                        (!x.AuthorizationValidFrom.HasValue ||
                         x.AuthorizationValidFrom.Value.Date <=
                            today) &&

                        (!x.AuthorizationValidUntil.HasValue ||
                         x.AuthorizationValidUntil.Value.Date >=
                            today))

                    .OrderBy(x =>
                        x.AssessmentCode)

                    .ThenBy(x =>
                        x.TargetName)

                    .Select(x =>
                        new
                        {
                            x.AssessmentTargetID,
                            x.AssessmentCode,
                            x.TargetName,
                            x.TargetValue
                        })

                    .ToListAsync();


            model.TargetOptions =
                eligibleTargets
                    .Select(x =>
                        new SelectListItem
                        {
                            Value =
                                x.AssessmentTargetID.ToString(),

                            Text =
                                $"{x.AssessmentCode} - {x.TargetName} ({x.TargetValue})"
                        })
                    .ToList();


            model.ScanProfileOptions =
                await _context.mst_ScanProfile

                    .AsNoTracking()

                    .Where(x =>
                        x.IsActive == true)

                    .OrderBy(x =>
                        x.ProfileName)

                    .Select(x =>
                        new SelectListItem
                        {
                            Value =
                                x.ScanProfileID.ToString(),

                            Text =
                                x.ProfileName
                        })

                    .ToListAsync();
        }
        /// <summary>
        /// Creates a governed Scan Job request.
        ///
        /// No scanner process is executed here.
        /// The MVC request only validates, snapshots and queues
        /// the job for future asynchronous worker processing.
        /// </summary>
        public async Task<ScanJobCreationResult>
            CreateScanJobAsync(
                ScanJobCreateViewModel model,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress,
                string scanJobPurposeCode = "ASSESSMENT")
        {


            var purpose = await _context.ref_ScanJobPurpose
        .FirstOrDefaultAsync(x =>
            x.ScanJobPurposeCode ==
                scanJobPurposeCode &&
            x.IsActive == true);


            if (purpose == null)
            {
                return ScanJobCreationResult.Failed($"Scan Job purpose '{scanJobPurposeCode}' is not configured.");
            }


            /*
             * Load the Assessment Target itself.
             * This first proves that the posted ID represents
             * an active target assignment.
             */
            var assessmentTarget =
                await _context.trn_AssessmentTarget

                    .Include(x => x.Target)

                    .Include(x =>
                        x.TargetAuthorizationStatus)

                    .FirstOrDefaultAsync(x =>
                        x.AssessmentTargetID ==
                            model.AssessmentTargetID &&
                        x.IsActive == true);


            if (assessmentTarget == null)
            {
                
                return ScanJobCreationResult.Failed("Assessment Target could not be found or is inactive.");

            }




            /*
             * The parent Assessment must also still be active.
             */
            var assessmentIsActive =
                await _context.mst_Assessment
                    .AnyAsync(x =>
                        x.AssessmentID ==
                            assessmentTarget.AssessmentID &&
                        x.IsActive == true);


            if (!assessmentIsActive)
            {
               
                return ScanJobCreationResult.Failed("The parent Assessment is inactive.");
            }


            /*
             * This method checks:
             *
             * - active target assignment
             * - APPROVED authorization state
             * - Valid From date
             * - Valid Until date
             *
             * Never rely only on the dropdown filtering.
             */
            var currentlyAuthorized =
                await _targetService
                    .IsTargetCurrentlyAuthorizedAsync(
                        assessmentTarget
                            .AssessmentTargetID);


            if (!currentlyAuthorized)
            {
              
                return ScanJobCreationResult.Failed("The selected target is not currently authorized for scanning.");
            }


            /*
             * Retrieve the Target Type from its reference table
             * for the immutable job snapshot.
             */
            var targetType =
                await _context.ref_TargetType
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.TargetTypeID ==
                            assessmentTarget
                                .Target
                                .TargetTypeID &&
                        x.IsActive == true);


            if (targetType == null)
            {
               
                return ScanJobCreationResult.Failed("The Target Type is invalid or inactive.");
            }


            /*
             * Load every active scope boundary so the Scan Job
             * receives an immutable snapshot of the current scope.
             */
            var scopeRules =
                await (
                    from rule
                        in _context.trn_AssessmentScopeRule

                    join ruleType
                        in _context.ref_ScopeRuleType

                        on rule.ScopeRuleTypeID
                        equals ruleType.ScopeRuleTypeID

                    where
                        rule.AssessmentTargetID ==
                            assessmentTarget.AssessmentTargetID

                        && rule.IsActive == true

                        && ruleType.IsActive == true

                    orderby
                        ruleType.ScopeRuleTypeCode,
                        rule.RuleValue

                    select new
                    {
                        rule.AssessmentScopeRuleID,

                        RuleTypeCode =
                            ruleType.ScopeRuleTypeCode,

                        rule.RuleValue,
                        rule.Description
                    }
                )
                .AsNoTracking()
                .ToListAsync();


            /*
             * We deliberately require an explicit INCLUDE rule.
             *
             * A target without any defined positive scope boundary
             * cannot become a Scan Job.
             */
            var hasIncludeRule =
                scopeRules.Any(x =>
                    x.RuleTypeCode == "INCLUDE");


            if (!hasIncludeRule)
            {
                
                return ScanJobCreationResult.Failed("The target must have at least one active INCLUDE scope rule before a Scan Job can be created.");
            }


            /*
             * Scan Profile must still exist and be active.
             */
            var scanProfile =
                await _context.mst_ScanProfile
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ScanProfileID ==
                            model.ScanProfileID &&
                        x.IsActive == true);


            if (scanProfile == null)
            {
                
                return ScanJobCreationResult.Failed("The selected Scan Profile could not be found or is inactive.");
            }


            var scannerType =
                await _context.ref_ScannerType
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ScannerTypeID ==
                            scanProfile.ScannerTypeID &&
                        x.IsActive == true);


            if (scannerType == null)
            {
               
                return ScanJobCreationResult.Failed("The Scan Profile's scanner is invalid or inactive.");
            }


            /*
             * Retrieve every active option supported by this scanner.
             *
             * We'll combine configured values with reference defaults
             * so the Scan Job snapshot represents the EFFECTIVE
             * configuration at creation time.
             */
            var availableOptions =
                await _context.ref_ScannerOption
                    .AsNoTracking()
                    .Where(x =>
                        x.ScannerTypeID ==
                            scannerType.ScannerTypeID &&
                        x.IsActive == true)
                    .OrderBy(x =>
                        x.OptionCode)
                    .ToListAsync();


            var configuredOptions =
                await _context.trn_ScanProfileOption
                    .AsNoTracking()
                    .Where(x =>
                        x.ScanProfileID ==
                            scanProfile.ScanProfileID &&
                        x.IsActive == true)
                    .ToListAsync();


            /*
             * Defensive consistency check:
             * an active option assignment must belong to the same
             * scanner as the Scan Profile.
             */
            var configuredOptionIds =
                configuredOptions
                    .Select(x =>
                        x.ScannerOptionID)
                    .ToList();


            if (configuredOptionIds.Count > 0)
            {
                var invalidConfiguredOptionExists =
                    await _context.ref_ScannerOption
                        .AsNoTracking()
                        .AnyAsync(x =>
                            configuredOptionIds.Contains(
                                x.ScannerOptionID) &&

                            x.ScannerTypeID !=
                                scanProfile.ScannerTypeID);


                if (invalidConfiguredOptionExists)
                {
                    
                    return ScanJobCreationResult.Failed("The Scan Profile contains an option that does not belong to its selected scanner.");
                }
            }


            var configuredByOptionId =
                configuredOptions
                    .ToDictionary(
                        x => x.ScannerOptionID);


            /*
             * Build the effective profile configuration.
             *
             * Configured value wins.
             * Otherwise use the scanner option's default.
             */
            var effectiveOptions =
                availableOptions
                    .Select(option =>
                    {
                        configuredByOptionId.TryGetValue(
                            option.ScannerOptionID,
                            out var configured);


                        return new
                        {
                            option.ScannerOptionID,
                            option.OptionCode,
                            option.OptionName,
                            option.ValueType,

                            OptionValue =
                                configured?.OptionValue
                                ?? option.DefaultValue,

                            option.IsIntrusive
                        };
                    })
                    .ToList();


            /*
             * Even though ScanProfileService already protects this,
             * job creation performs its own fail-closed check.
             */
            var intrusiveConfiguredOption =
                effectiveOptions.Any(x =>
                    x.IsIntrusive &&
                    configuredByOptionId.ContainsKey(
                        x.ScannerOptionID));


            if (intrusiveConfiguredOption &&
                (!scanProfile.IsIntrusive ||
                 !scanProfile.RequiresExplicitApproval))
            {
               
                return ScanJobCreationResult.Failed("The Scan Profile contains intrusive options but is not configured for explicit approval.");
            }


            /*
             * Resolve the initial workflow status.
             *
             * Standard profile:
             *      QUEUED
             *
             * Approval-required profile:
             *      PENDING_APPROVAL
             */
            var initialStatusCode =
                scanProfile.RequiresExplicitApproval
                    ? "PENDING_APPROVAL"
                    : "QUEUED";


            var initialStatus =
                await _context.ref_ScanJobStatus
                    .FirstOrDefaultAsync(x =>
                        x.ScanJobStatusCode ==
                            initialStatusCode &&
                        x.IsActive == true);


            if (initialStatus == null)
            {
               ;
                return ScanJobCreationResult.Failed($"Scan Job status '{initialStatusCode}' is not configured.");
            }


            /*
             * Serialize immutable snapshots.
             *
             * Future Target/Profile edits will therefore not silently
             * change what this job was requested to scan.
             */
            var scopeSnapshot =
                JsonSerializer.Serialize(
                    scopeRules.Select(x =>
                        new
                        {
                            x.AssessmentScopeRuleID,
                            x.RuleTypeCode,
                            x.RuleValue,
                            x.Description
                        }));


            var profileOptionsSnapshot =
                JsonSerializer.Serialize(
                    effectiveOptions);


            var now =
                DateTime.Now;


            var ownsTransaction = _context.Database.CurrentTransaction == null;

            IDbContextTransaction? transaction = null;


            if (ownsTransaction)
            {
                transaction =
                    await _context.Database
                        .BeginTransactionAsync();
            }


            try
            {
                /*
                 * JobCode is NOT NULL, but our permanent code depends
                 * on the SQL-generated ScanJobID.
                 *
                 * Therefore we insert a unique temporary code first,
                 * obtain the identity, then replace it immediately
                 * inside the same transaction.
                 */
                var temporaryJobCode =
                    $"TEMP-{Guid.NewGuid():N}";


                var scanJob =
                    new trn_ScanJob
                    {
                        JobCode =
                            temporaryJobCode,

                        AssessmentTargetID =
                            assessmentTarget.AssessmentTargetID,

                        ScanProfileID =
                            scanProfile.ScanProfileID,

                        ScanJobStatusID =
                            initialStatus.ScanJobStatusID,

                        TargetTypeCodeSnapshot =
                            targetType.TargetTypeCode,

                        TargetValueSnapshot =
                            assessmentTarget.Target.TargetValue,

                        ScanProfileCodeSnapshot =
                            scanProfile.ProfileCode,

                        ScannerTypeCodeSnapshot =
                            scannerType.ScannerTypeCode,

                        ScopeSnapshot =
                            scopeSnapshot,

                        ProfileOptionsSnapshot =
                            profileOptionsSnapshot,

                        RequiresExplicitApproval =
                            scanProfile.RequiresExplicitApproval,

                        RequestedByUserID =
                            userId,

                        RequestedDate =
                            now,

                        /*
                         * A standard profile is immediately eligible
                         * for worker pickup.
                         *
                         * Approval-required jobs remain unqueued.
                         */
                        QueuedDate =
                            initialStatusCode == "QUEUED"
                                ? now
                                : null,

                        CreatedBy =
                            userId,

                        CreatedDate =
                            now,
                        // Execution limits are snapshotted so later profile edits
                        // cannot silently change an already-created Scan Job.
                        IsIntrusiveSnapshot =
                            scanProfile.IsIntrusive,

                        TimeoutMinutesSnapshot =
                            scanProfile.TimeoutMinutes,

                        MaxConcurrencySnapshot =
                            scanProfile.MaxConcurrency,

                        ScanJobPurposeID =
                        purpose.ScanJobPurposeID,

                    };


                _context.trn_ScanJob.Add(
                    scanJob);


                // Generate ScanJobID.
                await _context.SaveChangesAsync();


                /*
                 * Create a human-readable permanent JobCode using
                 * the database identity, avoiding COUNT()+1 races.
                 *
                 * Example:
                 * JOB-20261005-000027
                 */
                scanJob.JobCode =
                    $"JOB-{now:yyyyMMdd}-{scanJob.ScanJobID:D6}";


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId:
                        userId,

                    userName:
                        userName,

                    actionType:
                        "CREATE",

                    module:
                        "Scan Jobs",

                    tableName:
                        "trn_ScanJob",

                    recordId:
                        scanJob.ScanJobID.ToString(),

                    description:
                        $"Created Scan Job '{scanJob.JobCode}' for target '{assessmentTarget.Target.TargetValue}'.",

                    oldValues:
                        null,

                    newValues:
                        new
                        {
                            scanJob.ScanJobID,
                            scanJob.JobCode,

                            scanJob.AssessmentTargetID,

                            scanJob.ScanProfileID,

                            Status =
                                initialStatusCode,

                            Target =
                                scanJob.TargetValueSnapshot,

                            Profile =
                                scanJob.ScanProfileCodeSnapshot,

                            Scanner =
                                scanJob.ScannerTypeCodeSnapshot,

                            scanJob.RequiresExplicitApproval,

                            ScopeRuleCount =
                                scopeRules.Count,

                            ScanOptionCount =
                                effectiveOptions.Count,

                            IsIntrusiveSnapshot =
                            scanProfile.IsIntrusive,

                            TimeoutMinutesSnapshot =
                            scanProfile.TimeoutMinutes,

                            MaxConcurrencySnapshot =
                            scanProfile.MaxConcurrency
                        },

                    requestPath:
                        requestPath,

                    ipAddress:
                        ipAddress,

                    isSuccessful:
                        true
                );
               

                /*
                 * Keep queueing as its own audit event.
                 * A standard profile enters the queue immediately.
                 */
                if (initialStatusCode == "QUEUED")
                {
                    await _auditTrailService.AddAsync(
                        userId:
                            userId,

                        userName:
                            userName,

                        actionType:
                            "QUEUE",

                        module:
                            "Scan Jobs",

                        tableName:
                            "trn_ScanJob",

                        recordId:
                            scanJob.ScanJobID.ToString(),

                        description:
                            $"Scan Job '{scanJob.JobCode}' entered the worker queue.",

                        oldValues:
                            null,

                        newValues:
                            new
                            {
                                Status =
                                    "QUEUED",

                                scanJob.QueuedDate
                            },

                        requestPath:
                            requestPath,

                        ipAddress:
                            ipAddress,

                        isSuccessful:
                            true
                    );
                }


                await _context.SaveChangesAsync();

                if (ownsTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                }


                return ScanJobCreationResult.Created(
     scanJob.ScanJobID);
            }
            catch
            {
                if (ownsTransaction &&
                    transaction != null)
                {
                    await transaction.RollbackAsync();
                }

                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        /// <summary>
        /// Loads a Scan Job together with the immutable
        /// scope and profile-option snapshots captured at creation.
        /// </summary>
        public async Task<ScanJobDetailsViewModel?>
            GetScanJobDetailsAsync(long scanJobId)
        {
            var job =
                await _context.vwGet_ScanJobData
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ScanJobID == scanJobId);


            if (job == null)
            {
                return null;
            }


            var sourceJob =
                await _context.trn_ScanJob
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ScanJobID == scanJobId);


            if (sourceJob == null)
            {
                return null;
            }


            /*
             * Snapshots were created by EVOLVE_VAPT itself.
             * If malformed JSON somehow exists, return empty
             * collections instead of breaking the entire page.
             */
            List<ScanJobScopeSnapshotViewModel> scopeRules;

            List<ScanJobOptionSnapshotViewModel> profileOptions;


            try
            {
                scopeRules =
                    JsonSerializer.Deserialize<
                        List<ScanJobScopeSnapshotViewModel>>(
                            sourceJob.ScopeSnapshot)
                    ?? new();
            }
            catch (JsonException)
            {
                scopeRules = new();
            }


            try
            {
                profileOptions =
                    string.IsNullOrWhiteSpace(
                        sourceJob.ProfileOptionsSnapshot)

                        ? new()

                        : JsonSerializer.Deserialize<
                            List<ScanJobOptionSnapshotViewModel>>(
                                sourceJob.ProfileOptionsSnapshot)
                          ?? new();
            }
            catch (JsonException)
            {
                profileOptions = new();
            }


            return new ScanJobDetailsViewModel
            {
                ScanJobID =
                    job.ScanJobID,

                JobCode =
                    job.JobCode,

                AssessmentTargetID =
                    job.AssessmentTargetID,

                AssessmentCode =
                    job.AssessmentCode,

                AssessmentName =
                    job.AssessmentName,

                TargetName =
                    job.TargetName,

                TargetTypeCode =
                    job.TargetTypeCodeSnapshot,

                TargetValue =
                    job.TargetValueSnapshot,

                ScanProfileID =
                    job.ScanProfileID,

                ScanProfileCode =
                    job.ScanProfileCodeSnapshot,

                ScanProfileName =
                    job.ScanProfileName,

                ScannerTypeCode =
                    job.ScannerTypeCodeSnapshot,

                ScanJobStatusCode =
                    job.ScanJobStatusCode,

                ScanJobStatusName =
                    job.ScanJobStatusName,

                IsTerminal =
                    job.IsTerminal,

                RequiresExplicitApproval =
                    job.RequiresExplicitApproval,

                RequestedByEmail =
                    job.RequestedByEmail,

                RequestedDate =
                    job.RequestedDate,

                ApprovedByEmail =
                    job.ApprovedByEmail,

                ApprovedDate =
                    job.ApprovedDate,

                RejectedByEmail =
                    job.RejectedByEmail,

                RejectedDate =
                    job.RejectedDate,

                CancelledByEmail =
                    job.CancelledByEmail,

                CancelledDate =
                    job.CancelledDate,

                CancellationReason =
                    job.CancellationReason,

                ApprovalNotes =
                    job.ApprovalNotes,

                QueuedDate =
                    job.QueuedDate,

                StartedDate =
                    job.StartedDate,

                CompletedDate =
                    job.CompletedDate,

                FailureReason =
                    job.FailureReason,

                ScopeRules =
                    scopeRules,

                ProfileOptions =
                    profileOptions
            };
        }

        /// <summary>
        /// Resolves an active Scan Job status using its
        /// stable business code rather than numeric IDs.
        /// </summary>
        private async Task<ref_ScanJobStatus?>
            GetScanJobStatusAsync(string statusCode)
        {
            return await _context.ref_ScanJobStatus
                .FirstOrDefaultAsync(x =>
                    x.ScanJobStatusCode == statusCode &&
                    x.IsActive == true);
        }

        /// <summary>
        /// Approves an approval-required Scan Job and
        /// moves it into the worker queue.
        ///
        /// Authorization is checked again because conditions
        /// may have changed since the job was requested.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)>
            ApproveScanJobAsync(
                long scanJobId,
                string userId,
                string userName,
                string? approvalNotes,
                string? requestPath,
                string? ipAddress)
        {
            var job =
                await _context.trn_ScanJob
                    .Include(x => x.ScanJobStatus)
                    .FirstOrDefaultAsync(x =>
                        x.ScanJobID == scanJobId);


            if (job == null)
            {
                return (
                    false,
                    "Scan Job could not be found."
                );
            }


            if (job.ScanJobStatus.ScanJobStatusCode !=
                "PENDING_APPROVAL")
            {
                return (
                    false,
                    "Only Scan Jobs pending approval can be approved."
                );
            }


            /*
             * Approval is not allowed to bypass a target whose
             * authorization has expired or changed since request.
             */
            var currentlyAuthorized =
                await _targetService
                    .IsTargetCurrentlyAuthorizedAsync(
                        job.AssessmentTargetID);


            if (!currentlyAuthorized)
            {
                return (
                    false,
                    "The target is no longer currently authorized for scanning."
                );
            }


            var queuedStatus =
                await GetScanJobStatusAsync("QUEUED");


            if (queuedStatus == null)
            {
                return (
                    false,
                    "Queued Scan Job status is not configured."
                );
            }


            var now = DateTime.Now;


            await using var transaction =
                await _context.Database.BeginTransactionAsync();


            try
            {
                job.ScanJobStatusID =
                    queuedStatus.ScanJobStatusID;

                job.ApprovedByUserID =
                    userId;

                job.ApprovedDate =
                    now;

                job.ApprovalNotes =
                    approvalNotes?.Trim();

                job.QueuedDate =
                    now;

                job.EditedBy =
                    userId;

                job.EditedDate =
                    now;


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "APPROVE",
                    module: "Scan Jobs",
                    tableName: "trn_ScanJob",
                    recordId: job.ScanJobID.ToString(),
                    description:
                        $"Approved Scan Job '{job.JobCode}'.",
                    oldValues: new
                    {
                        Status = "PENDING_APPROVAL"
                    },
                    newValues: new
                    {
                        Status = "QUEUED",
                        job.ApprovedByUserID,
                        job.ApprovedDate,
                        job.ApprovalNotes
                    },
                    requestPath: requestPath,
                    ipAddress: ipAddress,
                    isSuccessful: true
                );


                // Queueing is separately visible in the audit history.
                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "QUEUE",
                    module: "Scan Jobs",
                    tableName: "trn_ScanJob",
                    recordId: job.ScanJobID.ToString(),
                    description:
                        $"Scan Job '{job.JobCode}' entered the worker queue after approval.",
                    oldValues: null,
                    newValues: new
                    {
                        Status = "QUEUED",
                        job.QueuedDate
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

        /// <summary>
        /// Rejects a Scan Job that is waiting for explicit approval.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)>
            RejectScanJobAsync(
                long scanJobId,
                string reason,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return (
                    false,
                    "A rejection reason is required."
                );
            }


            var job =
                await _context.trn_ScanJob
                    .Include(x => x.ScanJobStatus)
                    .FirstOrDefaultAsync(x =>
                        x.ScanJobID == scanJobId);


            if (job == null)
            {
                return (
                    false,
                    "Scan Job could not be found."
                );
            }


            if (job.ScanJobStatus.ScanJobStatusCode !=
                "PENDING_APPROVAL")
            {
                return (
                    false,
                    "Only Scan Jobs pending approval can be rejected."
                );
            }


            var rejectedStatus =
                await GetScanJobStatusAsync("REJECTED");


            if (rejectedStatus == null)
            {
                return (
                    false,
                    "Rejected Scan Job status is not configured."
                );
            }


            var now = DateTime.Now;


            await using var transaction =
                await _context.Database.BeginTransactionAsync();


            try
            {
                job.ScanJobStatusID =
                    rejectedStatus.ScanJobStatusID;

                job.RejectedByUserID =
                    userId;

                job.RejectedDate =
                    now;

                job.ApprovalNotes =
                    reason.Trim();

                job.EditedBy =
                    userId;

                job.EditedDate =
                    now;


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "REJECT",
                    module: "Scan Jobs",
                    tableName: "trn_ScanJob",
                    recordId: job.ScanJobID.ToString(),
                    description:
                        $"Rejected Scan Job '{job.JobCode}'.",
                    oldValues: new
                    {
                        Status = "PENDING_APPROVAL"
                    },
                    newValues: new
                    {
                        Status = "REJECTED",
                        Reason = reason.Trim(),
                        job.RejectedByUserID,
                        job.RejectedDate
                    },
                    requestPath: requestPath,
                    ipAddress: ipAddress,
                    isSuccessful: true
                );


                await _context.SaveChangesAsync();

                await _retestStatusSynchronizationService
    .SyncForScanJobAsync(
        job.ScanJobID);

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

        /// <summary>
        /// Cancels a Scan Job before worker execution begins.
        ///
        /// RUNNING jobs are intentionally excluded for now.
        /// Worker-aware cancellation will be implemented later.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)>
            CancelScanJobAsync(
                long scanJobId,
                string reason,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return (
                    false,
                    "A cancellation reason is required."
                );
            }


            var job =
                await _context.trn_ScanJob
                    .Include(x => x.ScanJobStatus)
                    .FirstOrDefaultAsync(x =>
                        x.ScanJobID == scanJobId);


            if (job == null)
            {
                return (
                    false,
                    "Scan Job could not be found."
                );
            }


            var currentStatus =
                job.ScanJobStatus.ScanJobStatusCode;


            /*
             * For now a job may only be cancelled before execution.
             * RUNNING cancellation requires communication with the
             * background worker and will be added later.
             */
            if (currentStatus != "PENDING_APPROVAL" &&
                currentStatus != "QUEUED")
            {
                return (
                    false,
                    "Only Pending Approval or Queued Scan Jobs can currently be cancelled."
                );
            }


            var cancelledStatus =
                await GetScanJobStatusAsync("CANCELLED");


            if (cancelledStatus == null)
            {
                return (
                    false,
                    "Cancelled Scan Job status is not configured."
                );
            }


            var now = DateTime.Now;


            await using var transaction =
                await _context.Database.BeginTransactionAsync();


            try
            {
                job.ScanJobStatusID =
                    cancelledStatus.ScanJobStatusID;

                job.CancelledByUserID =
                    userId;

                job.CancelledDate =
                    now;

                job.CancellationReason =
                    reason.Trim();

                job.EditedBy =
                    userId;

                job.EditedDate =
                    now;


                await _context.SaveChangesAsync();
                await _retestStatusSynchronizationService
    .SyncForScanJobAsync(
        job.ScanJobID);


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "CANCEL",
                    module: "Scan Jobs",
                    tableName: "trn_ScanJob",
                    recordId: job.ScanJobID.ToString(),
                    description:
                        $"Cancelled Scan Job '{job.JobCode}'.",
                    oldValues: new
                    {
                        Status = currentStatus
                    },
                    newValues: new
                    {
                        Status = "CANCELLED",
                        Reason = reason.Trim(),
                        job.CancelledByUserID,
                        job.CancelledDate
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