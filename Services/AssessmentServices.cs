using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.ViewModels.Assessment;
using EVOLVE_VAPT.ViewModels.Target;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Services
{
    public class AssessmentService
    {
        
        private readonly EVOLVE_VAPTContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AuditTrailService _auditTrailService;
        public AssessmentService(
            EVOLVE_VAPTContext context,
            UserManager<IdentityUser> userManager,
            AuditTrailService auditTrailService)
        {
            _context = context;
            _userManager = userManager;
            _auditTrailService = auditTrailService;
        }


        public async Task<List<AssessmentListViewModel>> GetAssessmentsAsync()
        {
            return await _context.vwGet_AssessmentData
                .Where(x => x.IsActive == true)
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new AssessmentListViewModel
                {
                    AssessmentID = x.AssessmentID,

                    AssessmentCode =
                        x.AssessmentCode,

                    AssessmentName =
                        x.AssessmentName,

                    AssessmentStatusName =
                        x.AssessmentStatusName,

                    Environment =
                        x.Environment,

                    PlannedStartDate =
                        x.PlannedStartDate,

                    PlannedEndDate =
                        x.PlannedEndDate,

                    OwnerEmail =
                        x.OwnerEmail,

                    ReviewerEmail =
                        x.ReviewerEmail,

                    IsActive =
                        x.IsActive
                })
                .ToListAsync();
        }

        public async Task PopulateFormOptionsAsync(AssessmentFormViewModel model)
        {
            model.StatusOptions =
                await _context.ref_AssessmentStatus
                    .Where(x => x.IsActive == true)
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new SelectListItem
                    {
                        Value = x.AssessmentStatusID.ToString(),
                        Text = x.AssessmentStatusName
                    })
                    .ToListAsync();


            model.UserOptions =
                await _userManager.Users
                    .OrderBy(x => x.Email)
                    .Select(x => new SelectListItem
                    {
                        Value = x.Id,
                        Text = x.Email ?? x.UserName ?? x.Id
                    })
                    .ToListAsync();
        }


        public async Task<(bool Success, string? ErrorMessage)> CreateAssessmentAsync( AssessmentFormViewModel model, string userId, string userName, string? requestPath, string? ipAddress)
        {
            var codeExists =
                await _context.mst_Assessment
                    .AnyAsync(x =>
                        x.AssessmentCode == model.AssessmentCode);

            if (codeExists)
            {
                return (
                    false,
                    "The Assessment Code already exists."
                );
            }


            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var assessment = new mst_Assessment
                {
                    AssessmentCode =
                        model.AssessmentCode.Trim(),

                    AssessmentName =
                        model.AssessmentName.Trim(),

                    Description =
                        model.Description?.Trim(),

                    Methodology =
                        model.Methodology?.Trim(),

                    Environment =
                        model.Environment?.Trim(),

                    AssessmentStatusID =
                        model.AssessmentStatusID,

                    PlannedStartDate =
                        model.PlannedStartDate,

                    PlannedEndDate =
                        model.PlannedEndDate,

                    OwnerUserID =
                        model.OwnerUserID,

                    ReviewerUserID =
                        model.ReviewerUserID,

                    IsActive = true,

                    CreatedBy = userId,
                    CreatedDate = DateTime.Now
                };


                _context.mst_Assessment.Add(assessment);

                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "CREATE",
                    module: "Assessment",
                    tableName: "mst_Assessment",
                    recordId: assessment.AssessmentID.ToString(),
                    description:
                        $"Created assessment {assessment.AssessmentCode}.",
                    newValues: new
                    {
                        assessment.AssessmentCode,
                        assessment.AssessmentName,
                        assessment.AssessmentStatusID,
                        assessment.Environment,
                        assessment.PlannedStartDate,
                        assessment.PlannedEndDate,
                        assessment.OwnerUserID,
                        assessment.ReviewerUserID
                    },
                    requestPath: requestPath,
                    ipAddress: ipAddress,
                    isSuccessful: true
                );


                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return (true, null);
            }
            catch
            {
                await transaction.RollbackAsync();

                throw;
            }
        }

        public async Task<AssessmentDetailsViewModel?> GetAssessmentDetailsAsync(int assessmentId)
        {
            return await _context.vwGet_AssessmentData
                .Where(x =>
                    x.AssessmentID == assessmentId &&
                    x.IsActive == true)
                .Select(x => new AssessmentDetailsViewModel
                {
                    AssessmentID =
                        x.AssessmentID,

                    AssessmentCode =
                        x.AssessmentCode,

                    AssessmentName =
                        x.AssessmentName,

                    Description =
                        x.Description,

                    Methodology =
                        x.Methodology,

                    Environment =
                        x.Environment,

                    AssessmentStatusName =
                        x.AssessmentStatusName,

                    PlannedStartDate =
                        x.PlannedStartDate,

                    PlannedEndDate =
                        x.PlannedEndDate,

                    ActualStartDate =
                        x.ActualStartDate,

                    ActualEndDate =
                        x.ActualEndDate,

                    OwnerEmail =
                        x.OwnerEmail,

                    ReviewerEmail =
                        x.ReviewerEmail,

                    IsActive =
                        x.IsActive,

                    CreatedBy =
                        x.CreatedBy,

                    CreatedByEmail =
                        x.CreatedByEmail,

                    CreatedDate =
                        x.CreatedDate,

                    EditedBy =
                        x.EditedBy,

                    EditedByEmail =
                        x.EditedByEmail,

                    EditedDate =
                        x.EditedDate


                })
                .FirstOrDefaultAsync();
        }

        public async Task<AssessmentFormViewModel?> GetAssessmentForEditAsync(int assessmentId)
        {
            var assessment =
                await _context.mst_Assessment
                    .FirstOrDefaultAsync(x =>
                        x.AssessmentID == assessmentId &&
                        x.IsActive == true);

            if (assessment == null)
            {
                return null;
            }

            var model = new AssessmentFormViewModel
            {
                AssessmentID =
                    assessment.AssessmentID,

                AssessmentCode =
                    assessment.AssessmentCode,

                AssessmentName =
                    assessment.AssessmentName,

                Description =
                    assessment.Description,

                Methodology =
                    assessment.Methodology,

                Environment =
                    assessment.Environment,

                AssessmentStatusID =
                    assessment.AssessmentStatusID,

                PlannedStartDate =
                    assessment.PlannedStartDate,

                PlannedEndDate =
                    assessment.PlannedEndDate,

                ActualStartDate =
                    assessment.ActualStartDate,

                ActualEndDate =
                    assessment.ActualEndDate,

                OwnerUserID =
                    assessment.OwnerUserID,

                ReviewerUserID =
                    assessment.ReviewerUserID
            };

            await PopulateFormOptionsAsync(model);

            return model;
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateAssessmentAsync(
        AssessmentFormViewModel model,
        string userId,
        string userName,
        string? requestPath,
        string? ipAddress)
        {
            var assessment =
                await _context.mst_Assessment
                    .FirstOrDefaultAsync(x =>
                        x.AssessmentID == model.AssessmentID &&
                        x.IsActive == true);

            if (assessment == null)
            {
                return (
                    false,
                    "Assessment could not be found."
                );
            }


            var assessmentCode =
                model.AssessmentCode.Trim();


            var codeExists =
                await _context.mst_Assessment
                    .AnyAsync(x =>
                        x.AssessmentCode == assessmentCode &&
                        x.AssessmentID != model.AssessmentID);

            if (codeExists)
            {
                return (
                    false,
                    "The Assessment Code already exists."
                );
            }


            /*
             * Capture the values BEFORE modifying
             * the tracked entity.
             */
            var oldValues = new
            {
                assessment.AssessmentCode,
                assessment.AssessmentName,
                assessment.Description,
                assessment.Methodology,
                assessment.Environment,
                assessment.AssessmentStatusID,
                assessment.PlannedStartDate,
                assessment.PlannedEndDate,
                assessment.ActualStartDate,
                assessment.ActualEndDate,
                assessment.OwnerUserID,
                assessment.ReviewerUserID
            };


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                assessment.AssessmentCode =
                    assessmentCode;

                assessment.AssessmentName =
                    model.AssessmentName.Trim();

                assessment.Description =
                    model.Description?.Trim();

                assessment.Methodology =
                    model.Methodology?.Trim();

                assessment.Environment =
                    model.Environment?.Trim();

                assessment.AssessmentStatusID =
                    model.AssessmentStatusID;

                assessment.PlannedStartDate =
                    model.PlannedStartDate;

                assessment.PlannedEndDate =
                    model.PlannedEndDate;

                assessment.ActualStartDate =
                    model.ActualStartDate;

                assessment.ActualEndDate =
                    model.ActualEndDate;

                assessment.OwnerUserID =
                    model.OwnerUserID;

                assessment.ReviewerUserID =
                    model.ReviewerUserID;

                assessment.EditedBy =
                    userId;

                assessment.EditedDate =
                    DateTime.Now;


                var newValues = new
                {
                    assessment.AssessmentCode,
                    assessment.AssessmentName,
                    assessment.Description,
                    assessment.Methodology,
                    assessment.Environment,
                    assessment.AssessmentStatusID,
                    assessment.PlannedStartDate,
                    assessment.PlannedEndDate,
                    assessment.ActualStartDate,
                    assessment.ActualEndDate,
                    assessment.OwnerUserID,
                    assessment.ReviewerUserID
                };


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "UPDATE",
                    module: "Assessment",
                    tableName: "mst_Assessment",
                    recordId:
                        assessment.AssessmentID.ToString(),
                    description:
                        $"Updated assessment {assessment.AssessmentCode}.",
                    oldValues: oldValues,
                    newValues: newValues,
                    requestPath: requestPath,
                    ipAddress: ipAddress,
                    isSuccessful: true
                );


                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return (true, null);
            }
            catch
            {
                await transaction.RollbackAsync();

                throw;
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> DeactivateAssessmentAsync(
        int assessmentId,
        string userId,
        string userName,
        string? requestPath,
        string? ipAddress)
        {
            var assessment =
                await _context.mst_Assessment
                    .FirstOrDefaultAsync(x =>
                        x.AssessmentID == assessmentId &&
                        x.IsActive == true);

            if (assessment == null)
            {
                return (
                    false,
                    "Assessment could not be found."
                );
            }

            var oldValues = new
            {
                assessment.AssessmentID,
                assessment.AssessmentCode,
                assessment.AssessmentName,
                assessment.IsActive
            };

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                assessment.IsActive = false;
                assessment.EditedBy = userId;
                assessment.EditedDate = DateTime.Now;

                var newValues = new
                {
                    assessment.AssessmentID,
                    assessment.AssessmentCode,
                    assessment.AssessmentName,
                    assessment.IsActive
                };

                await _context.SaveChangesAsync();

                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "DEACTIVATE",
                    module: "Assessment",
                    tableName: "mst_Assessment",
                    recordId: assessment.AssessmentID.ToString(),
                    description:
                        $"Deactivated assessment {assessment.AssessmentCode}.",
                    oldValues: oldValues,
                    newValues: newValues,
                    requestPath: requestPath,
                    ipAddress: ipAddress,
                    isSuccessful: true
                );

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return (true, null);
            }
            catch
            {
                await transaction.RollbackAsync();

                throw;
            }
        }



    }
}