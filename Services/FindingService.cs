using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.ViewModels.Finding;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Services
{
    public class FindingService
    {
        private readonly EVOLVE_VAPTContext _context;
        private readonly AuditTrailService _auditTrailService;


        public FindingService(
            EVOLVE_VAPTContext context,
            AuditTrailService auditTrailService)
        {
            _context = context;
            _auditTrailService = auditTrailService;
        }


        public async Task<List<FindingListViewModel>>
            GetFindingsAsync()
        {
            return await _context.vwGet_FindingData
                .AsNoTracking()

                .Where(x =>
                    x.IsActive == true)

                /*
                 * Highest-severity and newest findings
                 * should naturally rise to the top.
                 */
                .OrderByDescending(x =>
                    x.SeveritySortOrder)

                .ThenByDescending(x =>
                    x.CreatedDate)

                .Select(x =>
                    new FindingListViewModel
                    {
                        FindingID =
                            x.FindingID,

                        Title =
                            x.Title,

                        SeverityCode =
                            x.SeverityCode,

                        SeverityName =
                            x.SeverityName,

                        FindingStatusCode =
                            x.FindingStatusCode,

                        FindingStatusName =
                            x.FindingStatusName,

                        TargetValue =
                            x.TargetValue,

                        AssessmentCode =
                            x.AssessmentCode,

                        JobCode =
                            x.JobCode,

                        ScannerTypeCode =
                            x.ScannerTypeCode,

                        CreatedDate =
                            x.CreatedDate
                    })

                .ToListAsync();
        }


        public async Task<RemediationFormViewModel?>
            GetFindingDetailsAsync(long findingId)
        {
            return await _context.vwGet_FindingData
                .AsNoTracking()

                .Where(x =>
                    x.FindingID == findingId &&
                    x.IsActive == true)

                .Select(x =>
                    new RemediationFormViewModel
                    {
                        FindingID =
                            x.FindingID,

                        ScanResultID =
                            x.ScanResultID,

                        ScanJobID =
                            x.ScanJobID,

                        AssessmentTargetID =
                            x.AssessmentTargetID,

                        SourceFindingIdentifier =
                            x.SourceFindingIdentifier,

                        Title =
                            x.Title,

                        SeverityCode =
                            x.SeverityCode,

                        SeverityName =
                            x.SeverityName,

                        FindingStatusCode =
                            x.FindingStatusCode,

                        FindingStatusName =
                            x.FindingStatusName,

                        TargetValue =
                            x.TargetValue,

                        AssessmentCode =
                            x.AssessmentCode,

                        AssessmentName =
                            x.AssessmentName,

                        TargetName =
                            x.TargetName,

                        JobCode =
                            x.JobCode,

                        Description =
                            x.Description,

                        Evidence =
                            x.Evidence,

                        Recommendation =
                            x.Recommendation,

                        Reference =
                            x.Reference,

                        ScannerTypeCode =
                            x.ScannerTypeCode,

                        ScannerVersion =
                            x.ScannerVersion,

                        ReviewedByEmail =
                            x.ReviewedByEmail,

                        ReviewedDate =
                            x.ReviewedDate,

                        ValidationNotes =
                            x.ValidationNotes,

                        CreatedDate =
                            x.CreatedDate
                    })

                .FirstOrDefaultAsync();
        }
        public async Task<(bool Success, string? ErrorMessage)>
    StartReviewAsync(
        long findingId,
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
                        x.FindingID == findingId &&
                        x.IsActive == true);


            if (finding == null)
            {
                return (
                    false,
                    "Finding could not be found."
                );
            }


            if (finding
                .FindingStatus
                .FindingStatusCode != "NEW")
            {
                return (
                    false,
                    "Only New findings can be placed Under Review."
                );
            }


            var underReviewStatus =
                await GetStatusAsync(
                    "UNDER_REVIEW");


            if (underReviewStatus == null)
            {
                return (
                    false,
                    "Under Review Finding status is not configured."
                );
            }


            var now =
                DateTime.Now;


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            try
            {
                finding.FindingStatusID =
                    underReviewStatus.FindingStatusID;

                finding.EditedBy =
                    userId;

                finding.EditedDate =
                    now;


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "START_REVIEW",
                    module: "Findings",
                    tableName: "trn_Finding",
                    recordId:
                        finding.FindingID.ToString(),
                    description:
                        $"Started analyst review for Finding {finding.FindingID}.",
                    oldValues: new
                    {
                        Status = "NEW"
                    },
                    newValues: new
                    {
                        Status = "UNDER_REVIEW"
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
        public async Task<(bool Success, string? ErrorMessage)>
    ConfirmFindingAsync(
        long findingId,
        string? notes,
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
                        x.FindingID == findingId &&
                        x.IsActive == true);


            if (finding == null)
            {
                return (
                    false,
                    "Finding could not be found."
                );
            }


            if (finding
                .FindingStatus
                .FindingStatusCode != "UNDER_REVIEW")
            {
                return (
                    false,
                    "Only findings Under Review can be confirmed."
                );
            }


            var confirmedStatus =
                await GetStatusAsync(
                    "CONFIRMED");


            if (confirmedStatus == null)
            {
                return (
                    false,
                    "Confirmed Finding status is not configured."
                );
            }


            var now =
                DateTime.Now;


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            try
            {
                finding.FindingStatusID =
                    confirmedStatus.FindingStatusID;

                finding.ReviewedByUserID =
                    userId;

                finding.ReviewedDate =
                    now;

                finding.ValidationNotes =
                    notes?.Trim();

                finding.EditedBy =
                    userId;

                finding.EditedDate =
                    now;


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "CONFIRM",
                    module: "Findings",
                    tableName: "trn_Finding",
                    recordId:
                        finding.FindingID.ToString(),
                    description:
                        $"Confirmed Finding {finding.FindingID}.",
                    oldValues: new
                    {
                        Status = "UNDER_REVIEW"
                    },
                    newValues: new
                    {
                        Status = "CONFIRMED",

                        finding.ReviewedByUserID,
                        finding.ReviewedDate,

                        finding.ValidationNotes
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
        public async Task<(bool Success, string? ErrorMessage)>
    MarkFalsePositiveAsync(
        long findingId,
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
                    "A false-positive reason is required."
                );
            }


            var finding =
                await _context.trn_Finding
                    .Include(x =>
                        x.FindingStatus)
                    .FirstOrDefaultAsync(x =>
                        x.FindingID == findingId &&
                        x.IsActive == true);


            if (finding == null)
            {
                return (
                    false,
                    "Finding could not be found."
                );
            }


            if (finding
                .FindingStatus
                .FindingStatusCode != "UNDER_REVIEW")
            {
                return (
                    false,
                    "Only findings Under Review can be marked as False Positive."
                );
            }


            var falsePositiveStatus =
                await GetStatusAsync(
                    "FALSE_POSITIVE");


            if (falsePositiveStatus == null)
            {
                return (
                    false,
                    "False Positive Finding status is not configured."
                );
            }


            var now =
                DateTime.Now;


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            try
            {
                finding.FindingStatusID =
                    falsePositiveStatus.FindingStatusID;

                finding.ReviewedByUserID =
                    userId;

                finding.ReviewedDate =
                    now;

                finding.ValidationNotes =
                    reason.Trim();

                finding.EditedBy =
                    userId;

                finding.EditedDate =
                    now;


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "FALSE_POSITIVE",
                    module: "Findings",
                    tableName: "trn_Finding",
                    recordId:
                        finding.FindingID.ToString(),
                    description:
                        $"Marked Finding {finding.FindingID} as False Positive.",
                    oldValues: new
                    {
                        Status = "UNDER_REVIEW"
                    },
                    newValues: new
                    {
                        Status = "FALSE_POSITIVE",

                        Reason =
                            reason.Trim(),

                        finding.ReviewedByUserID,
                        finding.ReviewedDate
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
        private async Task<ref_FindingStatus?>
    GetStatusAsync(string statusCode)
        {
            return await _context.ref_FindingStatus
                .FirstOrDefaultAsync(x =>
                    x.FindingStatusCode ==
                        statusCode &&
                    x.IsActive == true);
        }
    }
}