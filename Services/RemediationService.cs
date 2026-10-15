using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.ViewModels.Remediation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Services
{
    public class RemediationService
    {
        private readonly EVOLVE_VAPTContext _context;
        private readonly AuditTrailService _auditTrailService;
        private readonly UserManager<IdentityUser> _userManager;


        public RemediationService(
            EVOLVE_VAPTContext context,
            AuditTrailService auditTrailService,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _auditTrailService = auditTrailService;
            _userManager = userManager;
        }
        public async Task<List<RetestListViewModel>>
    GetRemediationsAsync()
        {
            return await _context
                .vwGet_FindingRemediationData
                .AsNoTracking()

                .Where(x =>
                    x.IsActive == true)

                .OrderByDescending(x =>
                    x.StartedDate)

                .Select(x =>
                    new RetestListViewModel
                    {
                        FindingRemediationID =
                            x.FindingRemediationID,

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

                        RemediationStatusCode =
                            x.RemediationStatusCode,

                        RemediationStatusName =
                            x.RemediationStatusName,

                        AssessmentCode =
                            x.AssessmentCode,

                        TargetValue =
                            x.TargetValue,

                        AssignedToEmail =
                            x.AssignedToEmail,

                        TargetDate =
                            x.TargetDate,

                        StartedDate =
                            x.StartedDate
                    })

                .ToListAsync();
        }
        public async Task PopulateUserOptionsAsync(
    RemediationFormViewModel model)
        {
            model.UserOptions =
                await _userManager.Users

                    .OrderBy(x =>
                        x.Email ?? x.UserName)

                    .Select(x =>
                        new SelectListItem
                        {
                            Value =
                                x.Id,

                            Text =
                                x.Email
                                ?? x.UserName
                                ?? x.Id
                        })

                    .ToListAsync();
        }
        public async Task<RemediationFormViewModel?>
    GetStartFormAsync(long findingId)
        {
            var finding =
                await _context.vwGet_FindingData
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.FindingID ==
                            findingId &&
                        x.IsActive == true);


            if (finding == null)
            {
                return null;
            }


            if (finding.FindingStatusCode !=
                "CONFIRMED")
            {
                return null;
            }


            /*
             * A finding may not have another active
             * remediation cycle already in progress.
             */
            var activeRemediationExists =
                await _context
                    .trn_FindingRemediation
                    .AnyAsync(x =>
                        x.FindingID ==
                            findingId &&
                        x.IsActive == true &&
                        !x.RemediationStatus.IsTerminal);


            if (activeRemediationExists)
            {
                return null;
            }


            var model =
                new RemediationFormViewModel
                {
                    FindingID =
                        finding.FindingID,

                    FindingTitle =
                        finding.Title,

                    SeverityName =
                        finding.SeverityName,

                    TargetValue =
                        finding.TargetValue
                };


            await PopulateUserOptionsAsync(model);


            return model;
        }
        public async Task<(bool Success, string? ErrorMessage, long? FindingRemediationID)> StartRemediationAsync(
        RemediationFormViewModel model,
        string userId,
        string userName,
        string? requestPath,
        string? ipAddress)
        {
            var finding =
                await _context.trn_Finding

                    .Include(x =>
                        x.FindingStatus)

                    .FirstOrDefaultAsync(x =>
                        x.FindingID ==
                            model.FindingID &&
                        x.IsActive == true);


            if (finding == null)
            {
                return (
                    false,
                    "Finding could not be found.",
                    null
                );
            }


            if (finding
                .FindingStatus
                .FindingStatusCode != "CONFIRMED")
            {
                return (
                    false,
                    "Only Confirmed findings can enter remediation.",
                    null
                );
            }


            var activeCycleExists =
                await _context
                    .trn_FindingRemediation
                    .AnyAsync(x =>
                        x.FindingID ==
                            finding.FindingID &&
                        x.IsActive == true &&
                        !x.RemediationStatus.IsTerminal);


            if (activeCycleExists)
            {
                return (
                    false,
                    "This finding already has an active remediation cycle.",
                    null
                );
            }


            if (!string.IsNullOrWhiteSpace(
                    model.AssignedToUserID))
            {
                var userExists =
                    await _userManager.Users.AnyAsync(x =>
                        x.Id ==
                            model.AssignedToUserID);


                if (!userExists)
                {
                    return (
                        false,
                        "The selected remediation assignee could not be found.",
                        null
                    );
                }
            }


            var remediationStatus =
                await _context.ref_RemediationStatus
                    .FirstOrDefaultAsync(x =>
                        x.RemediationStatusCode ==
                            "IN_PROGRESS" &&
                        x.IsActive == true);


            var findingRemediationStatus =
                await _context.ref_FindingStatus
                    .FirstOrDefaultAsync(x =>
                        x.FindingStatusCode ==
                            "REMEDIATION" &&
                        x.IsActive == true);


            if (remediationStatus == null ||
                findingRemediationStatus == null)
            {
                return (
                    false,
                    "Required remediation statuses are not configured.",
                    null
                );
            }


            var previousCycleNo =
                await _context
                    .trn_FindingRemediation

                    .Where(x =>
                        x.FindingID ==
                            finding.FindingID)

                    .Select(x =>
                        (int?)x.CycleNo)

                    .MaxAsync()
                    ?? 0;


            var now =
                DateTime.Now;


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            try
            {
                var remediation =
                    new trn_FindingRemediation
                    {
                        FindingID =
                            finding.FindingID,

                        CycleNo =
                            previousCycleNo + 1,

                        RemediationStatusID =
                            remediationStatus
                                .RemediationStatusID,

                        AssignedToUserID =
                            model.AssignedToUserID,

                        RemediationNotes =
                            model.RemediationNotes?.Trim(),

                        TargetDate =
                            model.TargetDate,

                        StartedDate =
                            now,

                        IsActive =
                            true,

                        CreatedBy =
                            userId,

                        CreatedDate =
                            now
                    };


                _context.trn_FindingRemediation
                    .Add(remediation);


                finding.FindingStatusID =
                    findingRemediationStatus
                        .FindingStatusID;

                finding.EditedBy =
                    userId;

                finding.EditedDate =
                    now;


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "START_REMEDIATION",
                    module: "Remediation",
                    tableName:
                        "trn_FindingRemediation",
                    recordId:
                        remediation
                            .FindingRemediationID
                            .ToString(),
                    description:
                        $"Started remediation cycle {remediation.CycleNo} for Finding {finding.FindingID}.",
                    oldValues: new
                    {
                        FindingStatus =
                            "CONFIRMED"
                    },
                    newValues: new
                    {
                        FindingStatus =
                            "REMEDIATION",

                        remediation.CycleNo,

                        remediation.AssignedToUserID,
                        remediation.TargetDate,
                        remediation.RemediationNotes
                    },
                    requestPath: requestPath,
                    ipAddress: ipAddress,
                    isSuccessful: true
                );


                await _context.SaveChangesAsync();

                await transaction.CommitAsync();


                return (
                    true,
                    null,
                    remediation.FindingRemediationID
                );
            }
            catch
            {
                await transaction.RollbackAsync();

                throw;
            }
        }
        public async Task<RemediationDetailsViewModel?>
    GetRemediationDetailsAsync(
        long remediationId)
        {
            return await _context
                .vwGet_FindingRemediationData
                .AsNoTracking()

                .Where(x =>
                    x.FindingRemediationID ==
                        remediationId &&
                    x.IsActive == true)

                .Select(x =>
                    new RemediationDetailsViewModel
                    {
                        FindingRemediationID =
                            x.FindingRemediationID,

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

                        FindingStatusCode =
                            x.FindingStatusCode,

                        FindingStatusName =
                            x.FindingStatusName,

                        RemediationStatusCode =
                            x.RemediationStatusCode,

                        RemediationStatusName =
                            x.RemediationStatusName,

                        AssessmentCode =
                            x.AssessmentCode,

                        AssessmentName =
                            x.AssessmentName,

                        TargetValue =
                            x.TargetValue,

                        JobCode =
                            x.JobCode,

                        AssignedToEmail =
                            x.AssignedToEmail,

                        RemediationNotes =
                            x.RemediationNotes,

                        TargetDate =
                            x.TargetDate,

                        StartedDate =
                            x.StartedDate,

                        SubmittedForRetestDate =
                            x.SubmittedForRetestDate,

                        SubmittedByEmail =
                            x.SubmittedByEmail,

                        SubmissionNotes =
                            x.SubmissionNotes,

                        CreatedDate =
                            x.CreatedDate,

                        CreatedByEmail =
                            x.CreatedByEmail
                    })

                .FirstOrDefaultAsync();
        }
        public async Task<RemediationFormViewModel?>
    GetRemediationForEditAsync(
        long remediationId)
        {
            var remediation =
                await _context
                    .vwGet_FindingRemediationData
                    .AsNoTracking()

                    .FirstOrDefaultAsync(x =>
                        x.FindingRemediationID ==
                            remediationId &&
                        x.IsActive == true &&
                        x.RemediationStatusCode ==
                            "IN_PROGRESS");


            if (remediation == null)
            {
                return null;
            }


            var model =
                new RemediationFormViewModel
                {
                    FindingRemediationID =
                        remediation
                            .FindingRemediationID,

                    FindingID =
                        remediation.FindingID,

                    FindingTitle =
                        remediation.FindingTitle,

                    SeverityName =
                        remediation.SeverityName,

                    TargetValue =
                        remediation.TargetValue,

                    AssignedToUserID =
                        remediation.AssignedToUserID,

                    TargetDate =
                        remediation.TargetDate,

                    RemediationNotes =
                        remediation.RemediationNotes
                };


            await PopulateUserOptionsAsync(model);


            return model;
        }
        public async Task<(bool Success, string? ErrorMessage)>
    UpdateRemediationAsync(
        RemediationFormViewModel model,
        string userId,
        string userName,
        string? requestPath,
        string? ipAddress)
        {
            if (!model.FindingRemediationID.HasValue)
            {
                return (
                    false,
                    "Remediation record could not be found."
                );
            }


            var remediation =
                await _context
                    .trn_FindingRemediation

                    .Include(x =>
                        x.RemediationStatus)

                    .FirstOrDefaultAsync(x =>
                        x.FindingRemediationID ==
                            model.FindingRemediationID.Value &&
                        x.IsActive == true);


            if (remediation == null)
            {
                return (
                    false,
                    "Remediation record could not be found."
                );
            }


            if (remediation
                .RemediationStatus
                .RemediationStatusCode !=
                "IN_PROGRESS")
            {
                return (
                    false,
                    "Only remediation currently In Progress can be edited."
                );
            }


            if (!string.IsNullOrWhiteSpace(
                    model.AssignedToUserID))
            {
                var userExists =
                    await _userManager.Users.AnyAsync(x =>
                        x.Id ==
                            model.AssignedToUserID);


                if (!userExists)
                {
                    return (
                        false,
                        "The selected remediation assignee could not be found."
                    );
                }
            }


            var oldValues =
                new
                {
                    remediation.AssignedToUserID,
                    remediation.TargetDate,
                    remediation.RemediationNotes
                };


            remediation.AssignedToUserID =
                model.AssignedToUserID;

            remediation.TargetDate =
                model.TargetDate;

            remediation.RemediationNotes =
                model.RemediationNotes?.Trim();

            remediation.EditedBy =
                userId;

            remediation.EditedDate =
                DateTime.Now;


            await _context.SaveChangesAsync();


            await _auditTrailService.LogAsync(
                userId: userId,
                userName: userName,
                actionType: "UPDATE",
                module: "Remediation",
                tableName:
                    "trn_FindingRemediation",
                recordId:
                    remediation
                        .FindingRemediationID
                        .ToString(),
                description:
                    $"Updated remediation cycle {remediation.CycleNo} for Finding {remediation.FindingID}.",
                oldValues: oldValues,
                newValues: new
                {
                    remediation.AssignedToUserID,
                    remediation.TargetDate,
                    remediation.RemediationNotes
                },
                requestPath: requestPath,
                ipAddress: ipAddress,
                isSuccessful: true
            );


            return (
                true,
                null
            );
        }
        public async Task<(bool Success, string? ErrorMessage)>
    SubmitForRetestAsync(
        long remediationId,
        string submissionNotes,
        string userId,
        string userName,
        string? requestPath,
        string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(
                    submissionNotes))
            {
                return (
                    false,
                    "Submission notes are required before retesting."
                );
            }


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
                    "Remediation record could not be found."
                );
            }


            if (remediation
                .RemediationStatus
                .RemediationStatusCode !=
                "IN_PROGRESS")
            {
                return (
                    false,
                    "Only remediation In Progress can be submitted for retesting."
                );
            }


            if (remediation
                .Finding
                .FindingStatus
                .FindingStatusCode !=
                "REMEDIATION")
            {
                return (
                    false,
                    "The Finding is not currently in Remediation."
                );
            }


            var submittedStatus =
                await _context.ref_RemediationStatus
                    .FirstOrDefaultAsync(x =>
                        x.RemediationStatusCode ==
                            "SUBMITTED_RETEST" &&
                        x.IsActive == true);


            var retestFindingStatus =
                await _context.ref_FindingStatus
                    .FirstOrDefaultAsync(x =>
                        x.FindingStatusCode ==
                            "RETEST" &&
                        x.IsActive == true);


            if (submittedStatus == null ||
                retestFindingStatus == null)
            {
                return (
                    false,
                    "Required Retest statuses are not configured."
                );
            }


            var now =
                DateTime.Now;


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            try
            {
                remediation.RemediationStatusID =
                    submittedStatus
                        .RemediationStatusID;

                remediation.SubmittedForRetestDate =
                    now;

                remediation.SubmittedByUserID =
                    userId;

                remediation.SubmissionNotes =
                    submissionNotes.Trim();

                remediation.EditedBy =
                    userId;

                remediation.EditedDate =
                    now;


                remediation.Finding.FindingStatusID =
                    retestFindingStatus
                        .FindingStatusID;

                remediation.Finding.EditedBy =
                    userId;

                remediation.Finding.EditedDate =
                    now;


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType:
                        "SUBMIT_RETEST",
                    module:
                        "Remediation",
                    tableName:
                        "trn_FindingRemediation",
                    recordId:
                        remediation
                            .FindingRemediationID
                            .ToString(),
                    description:
                        $"Submitted Finding {remediation.FindingID} remediation cycle {remediation.CycleNo} for retesting.",
                    oldValues: new
                    {
                        RemediationStatus =
                            "IN_PROGRESS",

                        FindingStatus =
                            "REMEDIATION"
                    },
                    newValues: new
                    {
                        RemediationStatus =
                            "SUBMITTED_RETEST",

                        FindingStatus =
                            "RETEST",

                        remediation.SubmittedForRetestDate,
                        remediation.SubmittedByUserID,
                        remediation.SubmissionNotes
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