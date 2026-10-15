using EVOLVE_VAPT.Models;
//using EVOLVE_VAPT.ViewModels.Assessment;
using EVOLVE_VAPT.ViewModels.Target;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;

namespace EVOLVE_VAPT.Services
{
    public class TargetService
    {
        private readonly EVOLVE_VAPTContext _context;
        private readonly AuditTrailService _auditTrailService;
        private readonly TargetValidationService _targetValidationService;

        public TargetService(
        EVOLVE_VAPTContext context,
        AuditTrailService auditTrailService,
        TargetValidationService targetValidationService)
        {
            _context = context;
            _auditTrailService = auditTrailService;
            _targetValidationService = targetValidationService;
        }


        /// <summary>
        /// Returns all active targets assigned to one assessment.
        ///
        /// Reading from vwGet_AssessmentTargetData allows the
        /// application to retrieve the target, target type,
        /// authorization status, and user-friendly authorization
        /// information in one read model.
        /// </summary>
        public async Task<List<AssessmentTargetListViewModel>> GetAssessmentTargetsAsync(int assessmentId)
        {
            return await _context.vwGet_AssessmentTargetData

                // Only return targets belonging to the
                // requested assessment and still marked active.
                .Where(x =>
                    x.AssessmentID == assessmentId &&
                    x.IsActive == true)

                .OrderBy(x => x.TargetName)

                .Select(x => new AssessmentTargetListViewModel
                {
                    AssessmentTargetID =
                        x.AssessmentTargetID,

                    AssessmentID =
                        x.AssessmentID,

                    TargetID =
                        x.TargetID,

                    TargetName =
                        x.TargetName,

                    TargetValue =
                        x.TargetValue,

                    TargetTypeName =
                        x.TargetTypeName,

                    AuthorizationStatusName =
                        x.AuthorizationStatusName,

                    AuthorizationReference =
                        x.AuthorizationReference,

                    AuthorizedByEmail =
                        x.AuthorizedByEmail,

                    AuthorizedDate =
                        x.AuthorizedDate,

                    AuthorizationValidFrom =
                        x.AuthorizationValidFrom,

                    AuthorizationValidUntil =
                        x.AuthorizationValidUntil,

                    IsActive =
                        x.IsActive
                })

                .ToListAsync();
        }

        /// <summary>
        /// Loads reference data required by the Add Target form.
        /// </summary>
        public async Task PopulateFormOptionsAsync(TargetFormViewModel model)
        {
            model.TargetTypeOptions =
                await _context.ref_TargetType

                    // Only active target types may be selected.
                    .Where(x => x.IsActive == true)

                    .OrderBy(x => x.SortOrder)

                    .Select(x => new SelectListItem
                    {
                        Value = x.TargetTypeID.ToString(),
                        Text = x.TargetTypeName
                    })

                    .ToListAsync();
        }

        /// <summary>
        /// Creates a master target and assigns it to an assessment.
        ///
        /// Security rule:
        /// Every newly added assessment target starts as
        /// PENDING approval. Creation alone never authorizes scanning.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> CreateTargetAsync(
                TargetFormViewModel model,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            var normalizedTargetValue =
                model.TargetValue.Trim();

            // Validate the value against the selected Target Type
            // before creating any database records.
            var validation =
                await _targetValidationService
                    .ValidateTargetValueAsync(
                        model.TargetTypeID,
                        normalizedTargetValue);


            if (!validation.IsValid)
            {
                return (
                    false,
                    validation.ErrorMessage
                );
            }


            // Confirm that the parent Assessment still exists
            // and has not been deactivated.
            var assessmentExists =
                await _context.mst_Assessment
                    .AnyAsync(x =>
                        x.AssessmentID == model.AssessmentID &&
                        x.IsActive == true);

            if (!assessmentExists)
            {
                return (
                    false,
                    "Assessment could not be found."
                );
            }


            // Resolve the Pending Approval reference record.
            // We use the stable code instead of assuming that
            // a particular numeric ID will always mean Pending.
            var pendingStatus =
                await _context.ref_TargetAuthorizationStatus
                    .FirstOrDefaultAsync(x =>
                        x.AuthorizationStatusCode == "PENDING" &&
                        x.IsActive == true);

            if (pendingStatus == null)
            {
                return (
                    false,
                    "Pending target authorization status is not configured."
                );
            }


            /*
             * Check whether this exact target is already active
             * in the current assessment.
             *
             * This prevents accidental duplicate assignments.
             */
            var duplicateExists =
                await _context.trn_AssessmentTarget

                    .Where(x =>
                        x.AssessmentID == model.AssessmentID &&
                        x.IsActive == true)

                    .AnyAsync(x =>
                        x.Target.TargetTypeID == model.TargetTypeID &&
                        x.Target.TargetValue == normalizedTargetValue);

            if (duplicateExists)
            {
                return (
                    false,
                    "This target is already assigned to the assessment."
                );
            }


            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                /*
                 * First create the reusable master target record.
                 */
                var target = new mst_Target
                {
                    TargetTypeID =
                        model.TargetTypeID,

                    TargetName =
                        model.TargetName.Trim(),

                    TargetValue =
                        normalizedTargetValue,

                    Description =
                        model.Description?.Trim(),

                    IsActive = true,

                    CreatedBy =
                        userId,

                    CreatedDate =
                        DateTime.Now
                };


                _context.mst_Target.Add(target);

                /*
                 * Save first so SQL Server generates TargetID.
                 * We need that value for trn_AssessmentTarget.
                 */
                await _context.SaveChangesAsync();


                /*
                 * Assign the newly created master target
                 * to the current assessment.
                 *
                 * The target remains PENDING until explicitly approved.
                 */
                var assessmentTarget =
                    new trn_AssessmentTarget
                    {
                        AssessmentID =
                            model.AssessmentID,

                        TargetID =
                            target.TargetID,

                        TargetAuthorizationStatusID =
                            pendingStatus.TargetAuthorizationStatusID,

                        AuthorizationReference =
                            model.AuthorizationReference?.Trim(),

                        AuthorizationNotes =
                            model.AuthorizationNotes?.Trim(),

                        AuthorizationValidFrom =
                            model.AuthorizationValidFrom,

                        AuthorizationValidUntil =
                            model.AuthorizationValidUntil,

                        IsActive = true,

                        CreatedBy =
                            userId,

                        CreatedDate =
                            DateTime.Now
                    };


                _context.trn_AssessmentTarget
                    .Add(assessmentTarget);

                await _context.SaveChangesAsync();


                /*
                 * Record both the master target and its
                 * assessment assignment in AuditTrail.
                 */
                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "CREATE",
                    module: "Targets & Scope",
                    tableName: "trn_AssessmentTarget",
                    recordId:
                        assessmentTarget.AssessmentTargetID.ToString(),
                    description:
                        $"Added target {target.TargetValue} to assessment ID {model.AssessmentID}.",
                    newValues: new
                    {
                        assessmentTarget.AssessmentTargetID,
                        assessmentTarget.AssessmentID,

                        target.TargetID,
                        target.TargetName,
                        target.TargetValue,
                        target.TargetTypeID,

                        AuthorizationStatus =
                            pendingStatus.AuthorizationStatusCode,

                        assessmentTarget.AuthorizationReference,
                        assessmentTarget.AuthorizationValidFrom,
                        assessmentTarget.AuthorizationValidUntil
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
                // Neither the master target nor its assessment
                // assignment should remain if creation fails.
                await transaction.RollbackAsync();

                throw;
            }
        }

        /// <summary>
        /// Returns one active Assessment Target using the
        /// database view that already combines target,
        /// assessment, type, authorization and user information.
        /// </summary>
        public async Task<TargetDetailsViewModel?> GetTargetDetailsAsync(int assessmentTargetId)
        {
            var target = await _context.vwGet_AssessmentTargetData.Where(x =>
         x.AssessmentTargetID == assessmentTargetId &&
         x.IsActive == true).Select(x => new TargetDetailsViewModel
             {
                 // Keep all your existing mappings here.
                 AssessmentTargetID = x.AssessmentTargetID,
                 AssessmentID = x.AssessmentID,

                 AssessmentCode = x.AssessmentCode,
                 AssessmentName = x.AssessmentName,

                 TargetID = x.TargetID,
                 TargetName = x.TargetName,
                 TargetValue = x.TargetValue,

                 TargetTypeCode = x.TargetTypeCode,
                 TargetTypeName = x.TargetTypeName,

                 AuthorizationStatusCode =
                     x.AuthorizationStatusCode,

                 AuthorizationStatusName =
                     x.AuthorizationStatusName,

                 AuthorizationReference =
                     x.AuthorizationReference,

                 AuthorizationNotes =
                     x.AuthorizationNotes,

                 AuthorizedByEmail =
                     x.AuthorizedByEmail,

                 AuthorizedDate =
                     x.AuthorizedDate,

                 AuthorizationValidFrom =
                     x.AuthorizationValidFrom,

                 AuthorizationValidUntil =
                     x.AuthorizationValidUntil,

                 IsActive =
                     x.IsActive,

                 CreatedByEmail =
                     x.CreatedByEmail,

                 CreatedDate =
                     x.CreatedDate,

                 EditedByEmail =
                     x.EditedByEmail,

                 EditedDate =
                     x.EditedDate
             }).FirstOrDefaultAsync();


            if (target == null)
            {
                return null;
            }


            // Load the target's current boundary rules separately.
            target.ScopeRules =
                await GetScopeRulesAsync(assessmentTargetId);


            return target;
        }

        /// <summary>
        /// Explicitly approves a pending Assessment Target.
        ///
        /// Approval is a separate operation from target creation.
        /// Scanner eligibility will later depend on this status.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> ApproveTargetAsync(
                int assessmentTargetId,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            var assessmentTarget =
                await _context.trn_AssessmentTarget
                    .Include(x => x.Target)
                    .Include(x => x.TargetAuthorizationStatus)
                    .FirstOrDefaultAsync(x =>
                        x.AssessmentTargetID == assessmentTargetId &&
                        x.IsActive == true);

            if (assessmentTarget == null)
            {
                return (
                    false,
                    "Assessment target could not be found."
                );
            }


            // Approval currently requires the target to still
            // be in the Pending state.
            if (assessmentTarget
                .TargetAuthorizationStatus
                .AuthorizationStatusCode != "PENDING")
            {
                return (
                    false,
                    "Only pending targets can be approved."
                );
            }


            var approvedStatus =
                await _context.ref_TargetAuthorizationStatus
                    .FirstOrDefaultAsync(x =>
                        x.AuthorizationStatusCode == "APPROVED" &&
                        x.IsActive == true);

            if (approvedStatus == null)
            {
                return (
                    false,
                    "Approved authorization status is not configured."
                );
            }


            var oldValues = new
            {
                AuthorizationStatus =
                    assessmentTarget
                        .TargetAuthorizationStatus
                        .AuthorizationStatusCode,

                assessmentTarget.AuthorizedByUserID,
                assessmentTarget.AuthorizedDate
            };


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                // Explicit approval is recorded against the
                // authenticated user performing the action.
                assessmentTarget.TargetAuthorizationStatusID =
                    approvedStatus.TargetAuthorizationStatusID;

                assessmentTarget.AuthorizedByUserID =
                    userId;

                assessmentTarget.AuthorizedDate =
                    DateTime.Now;

                assessmentTarget.EditedBy =
                    userId;

                assessmentTarget.EditedDate =
                    DateTime.Now;


                var newValues = new
                {
                    AuthorizationStatus = "APPROVED",

                    assessmentTarget.AuthorizedByUserID,
                    assessmentTarget.AuthorizedDate
                };


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "APPROVE",
                    module: "Targets & Scope",
                    tableName: "trn_AssessmentTarget",
                    recordId:
                        assessmentTarget.AssessmentTargetID.ToString(),
                    description:
                        $"Approved target {assessmentTarget.Target.TargetValue}.",
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

        /// <summary>
        /// Rejects a pending Assessment Target.
        ///
        /// The rejection reason is retained in AuditTrail
        /// so the decision remains traceable.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> RejectTargetAsync(
                int assessmentTargetId,
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


            var assessmentTarget =
                await _context.trn_AssessmentTarget
                    .Include(x => x.Target)
                    .Include(x => x.TargetAuthorizationStatus)
                    .FirstOrDefaultAsync(x =>
                        x.AssessmentTargetID == assessmentTargetId &&
                        x.IsActive == true);

            if (assessmentTarget == null)
            {
                return (
                    false,
                    "Assessment target could not be found."
                );
            }


            if (assessmentTarget
                .TargetAuthorizationStatus
                .AuthorizationStatusCode != "PENDING")
            {
                return (
                    false,
                    "Only pending targets can be rejected."
                );
            }


            var rejectedStatus =
                await _context.ref_TargetAuthorizationStatus
                    .FirstOrDefaultAsync(x =>
                        x.AuthorizationStatusCode == "REJECTED" &&
                        x.IsActive == true);

            if (rejectedStatus == null)
            {
                return (
                    false,
                    "Rejected authorization status is not configured."
                );
            }


            var oldValues = new
            {
                AuthorizationStatus =
                    assessmentTarget
                        .TargetAuthorizationStatus
                        .AuthorizationStatusCode
            };


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                assessmentTarget.TargetAuthorizationStatusID =
                    rejectedStatus.TargetAuthorizationStatusID;

                assessmentTarget.EditedBy =
                    userId;

                assessmentTarget.EditedDate =
                    DateTime.Now;


                await _context.SaveChangesAsync();


                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "REJECT",
                    module: "Targets & Scope",
                    tableName: "trn_AssessmentTarget",
                    recordId:
                        assessmentTarget.AssessmentTargetID.ToString(),
                    description:
                        $"Rejected target {assessmentTarget.Target.TargetValue}. Reason: {reason.Trim()}",
                    oldValues: oldValues,
                    newValues: new
                    {
                        AuthorizationStatus = "REJECTED",
                        Reason = reason.Trim()
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

        /// <summary>
        /// Returns all active scope rules defined for one
        /// Assessment Target.
        /// </summary>
        public async Task<List<ScopeRuleListViewModel>>
            GetScopeRulesAsync(int assessmentTargetId)
        {
            return await _context.vwGet_AssessmentScopeRuleData

                // Deactivated rules remain in the database
                // but are not part of the current scope.
                .Where(x =>
                    x.AssessmentTargetID == assessmentTargetId &&
                    x.IsActive == true)

                .OrderBy(x => x.ScopeRuleTypeCode)

                .ThenBy(x => x.RuleValue)

                .Select(x => new ScopeRuleListViewModel
                {
                    AssessmentScopeRuleID =
                        x.AssessmentScopeRuleID,

                    AssessmentTargetID =
                        x.AssessmentTargetID,

                    ScopeRuleTypeCode =
                        x.ScopeRuleTypeCode,

                    ScopeRuleTypeName =
                        x.ScopeRuleTypeName,

                    RuleValue =
                        x.RuleValue,

                    Description =
                        x.Description,

                    CreatedByEmail =
                        x.CreatedByEmail,

                    CreatedDate =
                        x.CreatedDate
                })

                .ToListAsync();
        }

        /// <summary>
        /// Loads the available Include/Exclude values
        /// from ref_ScopeRuleType.
        /// </summary>
        public async Task PopulateScopeRuleOptionsAsync(
            ScopeRuleFormViewModel model)
        {
            model.RuleTypeOptions =
                await _context.ref_ScopeRuleType

                    .Where(x => x.IsActive == true)

                    .OrderBy(x => x.SortOrder)

                    .Select(x => new SelectListItem
                    {
                        Value = x.ScopeRuleTypeID.ToString(),
                        Text = x.ScopeRuleTypeName
                    })

                    .ToListAsync();
        }

        /// <summary>
        /// Adds an Include/Exclude rule to an Assessment Target.
        ///
        /// Security rule:
        /// If the target was already APPROVED, changing its scope
        /// invalidates that approval and returns it to PENDING.
        /// </summary>
        public async Task<(
            bool Success,
            string? ErrorMessage,
            bool ApprovalReset)>
            CreateScopeRuleAsync(
                ScopeRuleFormViewModel model,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            var ruleValue = model.RuleValue.Trim();


            // Load the Assessment Target together with its
            // current authorization status and master target.
            var assessmentTarget =
                await _context.trn_AssessmentTarget

                    .Include(x => x.Target)

                    .Include(x => x.TargetAuthorizationStatus)

                    .FirstOrDefaultAsync(x =>
                        x.AssessmentTargetID ==
                            model.AssessmentTargetID &&
                        x.IsActive == true);


            if (assessmentTarget == null)
            {
                return (
                    false,
                    "Assessment target could not be found.",
                    false
                );
            }


            var currentStatusCode =
                assessmentTarget
                    .TargetAuthorizationStatus
                    .AuthorizationStatusCode;


            // Rejected and revoked targets should not have
            // their testing boundaries modified.
            if (currentStatusCode == "REJECTED" ||
                currentStatusCode == "REVOKED")
            {
                return (
                    false,
                    "Scope rules cannot be added to a rejected or revoked target.",
                    false
                );
            }


            // Confirm that the selected rule type still exists.
            var ruleType =
                await _context.ref_ScopeRuleType
                    .FirstOrDefaultAsync(x =>
                        x.ScopeRuleTypeID ==
                            model.ScopeRuleTypeID &&
                        x.IsActive == true);


            if (ruleType == null)
            {
                return (
                    false,
                    "Scope Rule Type could not be found.",
                    false
                );
            }


            /*
             * Prevent the exact same active rule from being
             * added twice to the same Assessment Target.
             */
            var duplicateExists =
                await _context.trn_AssessmentScopeRule
                    .AnyAsync(x =>
                        x.AssessmentTargetID ==
                            model.AssessmentTargetID &&
                        x.ScopeRuleTypeID ==
                            model.ScopeRuleTypeID &&
                        x.RuleValue == ruleValue &&
                        x.IsActive == true);


            if (duplicateExists)
            {
                return (
                    false,
                    "This scope rule already exists.",
                    false
                );
            }


            /*
             * If an already-approved target changes scope,
             * we need the Pending status so the new scope
             * can go through approval again.
             */
            ref_TargetAuthorizationStatus? pendingStatus = null;

            if (currentStatusCode == "APPROVED")
            {
                pendingStatus =
                    await _context.ref_TargetAuthorizationStatus
                        .FirstOrDefaultAsync(x =>
                            x.AuthorizationStatusCode ==
                                "PENDING" &&
                            x.IsActive == true);

                if (pendingStatus == null)
                {
                    return (
                        false,
                        "Pending authorization status is not configured.",
                        false
                    );
                }
            }


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            try
            {
                // Create the new Include/Exclude rule.
                var scopeRule =
                    new trn_AssessmentScopeRule
                    {
                        AssessmentTargetID =
                            model.AssessmentTargetID,

                        ScopeRuleTypeID =
                            model.ScopeRuleTypeID,

                        RuleValue =
                            ruleValue,

                        Description =
                            model.Description?.Trim(),

                        IsActive = true,

                        CreatedBy =
                            userId,

                        CreatedDate =
                            DateTime.Now
                    };


                _context.trn_AssessmentScopeRule
                    .Add(scopeRule);


                /*
                 * Save first so SQL Server generates
                 * AssessmentScopeRuleID.
                 */
                await _context.SaveChangesAsync();


                var approvalReset = false;


                /*
                 * IMPORTANT:
                 * Approval applied to the OLD scope.
                 *
                 * If the target was approved, the addition of a
                 * new rule means the current scope has changed.
                 * Return it to Pending Approval.
                 */
                if (currentStatusCode == "APPROVED")
                {
                    assessmentTarget
                        .TargetAuthorizationStatusID =
                            pendingStatus!
                                .TargetAuthorizationStatusID;


                    // The old approval is no longer applicable
                    // to the newly modified testing scope.
                    assessmentTarget.AuthorizedByUserID =
                        null;

                    assessmentTarget.AuthorizedDate =
                        null;


                    assessmentTarget.EditedBy =
                        userId;

                    assessmentTarget.EditedDate =
                        DateTime.Now;


                    approvalReset = true;
                }


                await _context.SaveChangesAsync();


                // Audit the actual creation of the scope rule.
                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "CREATE",
                    module: "Targets & Scope",
                    tableName: "trn_AssessmentScopeRule",
                    recordId:
                        scopeRule.AssessmentScopeRuleID.ToString(),
                    description:
                        $"Added {ruleType.ScopeRuleTypeCode} scope rule '{scopeRule.RuleValue}' to target {assessmentTarget.Target.TargetValue}.",
                    newValues: new
                    {
                        scopeRule.AssessmentScopeRuleID,
                        scopeRule.AssessmentTargetID,

                        RuleType =
                            ruleType.ScopeRuleTypeCode,

                        scopeRule.RuleValue,
                        scopeRule.Description
                    },
                    requestPath: requestPath,
                    ipAddress: ipAddress,
                    isSuccessful: true
                );


                /*
                 * Create a separate audit record if approval
                 * had to be invalidated because scope changed.
                 */
                if (approvalReset)
                {
                    await _auditTrailService.AddAsync(
                        userId: userId,
                        userName: userName,
                        actionType: "RESET_APPROVAL",
                        module: "Targets & Scope",
                        tableName: "trn_AssessmentTarget",
                        recordId:
                            assessmentTarget
                                .AssessmentTargetID
                                .ToString(),
                        description:
                            $"Target {assessmentTarget.Target.TargetValue} returned to Pending Approval because its scope changed.",
                        oldValues: new
                        {
                            AuthorizationStatus =
                                "APPROVED"
                        },
                        newValues: new
                        {
                            AuthorizationStatus =
                                "PENDING"
                        },
                        requestPath: requestPath,
                        ipAddress: ipAddress,
                        isSuccessful: true
                    );
                }


                await _context.SaveChangesAsync();

                await transaction.CommitAsync();


                return (
                    true,
                    null,
                    approvalReset
                );
            }
            catch
            {
                // Scope creation, approval reset, and auditing
                // must succeed or fail as one transaction.
                await transaction.RollbackAsync();

                throw;
            }
        }


        /// <summary>
        /// Deactivates an existing scope rule.
        ///
        /// Security rule:
        /// Removing an Include/Exclude rule changes the testing boundary.
        /// If the target is currently APPROVED, its approval must be
        /// invalidated and returned to PENDING.
        /// </summary>
        public async Task<(
           bool Success,
            string? ErrorMessage,
            bool ApprovalReset,
            int? AssessmentTargetID)> DeactivateScopeRuleAsync(
                int assessmentScopeRuleId,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            // Retrieve the active scope rule that the user wants to deactivate.
            var scopeRule =
                await _context.trn_AssessmentScopeRule
                    .FirstOrDefaultAsync(x =>
                        x.AssessmentScopeRuleID == assessmentScopeRuleId &&
                        x.IsActive == true);

            if (scopeRule == null)
            {
                return (
                    false,
                    "Scope rule could not be found.",
                    false,
                    null
                );
            }


            // Retrieve the parent Assessment Target because changing its
            // rules may also invalidate its current approval.
            var assessmentTarget =
                await _context.trn_AssessmentTarget
                    .Include(x => x.Target)
                    .Include(x => x.TargetAuthorizationStatus)
                    .FirstOrDefaultAsync(x =>
                        x.AssessmentTargetID ==
                            scopeRule.AssessmentTargetID &&
                        x.IsActive == true);

            if (assessmentTarget == null)
            {
                return (
                         false,
                         "Scope rule could not be found.",
                         false,
                         null
                     );
            }


            var currentStatusCode =
                assessmentTarget
                    .TargetAuthorizationStatus
                    .AuthorizationStatusCode;


            // Rejected and revoked targets are treated as locked
            // from further scope modification.
            if (currentStatusCode == "REJECTED" ||
                currentStatusCode == "REVOKED")
            {
                return (
                    false,
                    "Scope rules cannot be modified for a rejected or revoked target.",
                    false,
                    null
                );
            }


            // Resolve the rule type for a more meaningful audit entry.
            var ruleType =
                await _context.ref_ScopeRuleType
                    .FirstOrDefaultAsync(x =>
                        x.ScopeRuleTypeID ==
                            scopeRule.ScopeRuleTypeID);

            if (ruleType == null)
            {
                return (
                    false,
                    "Scope Rule Type could not be found.",
                    false,
                    scopeRule.AssessmentTargetID
                );
            }


            // If an Approved target changes scope, we need the
            // Pending status so that the new boundary can be reviewed again.
            ref_TargetAuthorizationStatus? pendingStatus = null;

            if (currentStatusCode == "APPROVED")
            {
                pendingStatus =
                    await _context.ref_TargetAuthorizationStatus
                        .FirstOrDefaultAsync(x =>
                            x.AuthorizationStatusCode == "PENDING" &&
                            x.IsActive == true);

                if (pendingStatus == null)
                {
                    return (
                        false,
                        "Pending authorization status is not configured.",
                        false,
                        null
                    );
                }
            }


            var oldValues = new
            {
                scopeRule.AssessmentScopeRuleID,
                RuleType = ruleType.ScopeRuleTypeCode,
                scopeRule.RuleValue,
                scopeRule.Description,
                scopeRule.IsActive
            };


            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // Soft-delete the rule so its historical record remains.
                scopeRule.IsActive = false;
                scopeRule.EditedBy = userId;
                scopeRule.EditedDate = DateTime.Now;


                var approvalReset = false;


                // Approval belonged to the previous scope.
                // Once the scope changes, force another review.
                if (currentStatusCode == "APPROVED")
                {
                    assessmentTarget.TargetAuthorizationStatusID =
                        pendingStatus!.TargetAuthorizationStatusID;

                    // Clear the previous approval information because
                    // it no longer applies to the new scope.
                    assessmentTarget.AuthorizedByUserID = null;
                    assessmentTarget.AuthorizedDate = null;

                    assessmentTarget.EditedBy = userId;
                    assessmentTarget.EditedDate = DateTime.Now;

                    approvalReset = true;
                }


                await _context.SaveChangesAsync();


                // Record the removal of the scope rule.
                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "DEACTIVATE",
                    module: "Targets & Scope",
                    tableName: "trn_AssessmentScopeRule",
                    recordId:
                        scopeRule.AssessmentScopeRuleID.ToString(),
                    description:
                        $"Deactivated {ruleType.ScopeRuleTypeCode} scope rule '{scopeRule.RuleValue}' for target {assessmentTarget.Target.TargetValue}.",
                    oldValues: oldValues,
                    newValues: new
                    {
                        scopeRule.AssessmentScopeRuleID,
                        RuleType = ruleType.ScopeRuleTypeCode,
                        scopeRule.RuleValue,
                        IsActive = false
                    },
                    requestPath: requestPath,
                    ipAddress: ipAddress,
                    isSuccessful: true
                );


                // Record approval invalidation separately so the
                // security decision is obvious in AuditTrail.
                if (approvalReset)
                {
                    await _auditTrailService.AddAsync(
                        userId: userId,
                        userName: userName,
                        actionType: "RESET_APPROVAL",
                        module: "Targets & Scope",
                        tableName: "trn_AssessmentTarget",
                        recordId:
                            assessmentTarget
                                .AssessmentTargetID
                                .ToString(),
                        description:
                            $"Target {assessmentTarget.Target.TargetValue} returned to Pending Approval because its scope changed.",
                        oldValues: new
                        {
                            AuthorizationStatus = "APPROVED"
                        },
                        newValues: new
                        {
                            AuthorizationStatus = "PENDING"
                        },
                        requestPath: requestPath,
                        ipAddress: ipAddress,
                        isSuccessful: true
                    );
                }


                await _context.SaveChangesAsync();

                await transaction.CommitAsync();


                return (
                    true,
                    null,
                    approvalReset,
                    scopeRule.AssessmentTargetID

                );
            }
            catch
            {
                // Rule deactivation, approval reset and audit records
                // must all succeed or all fail together.
                await transaction.RollbackAsync();

                throw;
            }
        }

        /// <summary>
        /// Loads an existing active Assessment Target
        /// and prepares it for the Edit form.
        /// </summary>
        public async Task<TargetFormViewModel?>
            GetTargetForEditAsync(int assessmentTargetId)
        {
            var assessmentTarget =
                await _context.trn_AssessmentTarget

                    // Target contains TargetName, TargetValue,
                    // TargetTypeID and Description.
                    .Include(x => x.Target)

                    .FirstOrDefaultAsync(x =>
                        x.AssessmentTargetID == assessmentTargetId &&
                        x.IsActive == true);


            if (assessmentTarget == null)
            {
                return null;
            }


            var model = new TargetFormViewModel
            {
                AssessmentTargetID =
                    assessmentTarget.AssessmentTargetID,

                AssessmentID =
                    assessmentTarget.AssessmentID,

                TargetTypeID =
                    assessmentTarget.Target.TargetTypeID,

                TargetName =
                    assessmentTarget.Target.TargetName,

                TargetValue =
                    assessmentTarget.Target.TargetValue,

                Description =
                    assessmentTarget.Target.Description,

                AuthorizationReference =
                    assessmentTarget.AuthorizationReference,

                AuthorizationNotes =
                    assessmentTarget.AuthorizationNotes,

                AuthorizationValidFrom =
                    assessmentTarget.AuthorizationValidFrom,

                AuthorizationValidUntil =
                    assessmentTarget.AuthorizationValidUntil
            };


            // Populate Target Type dropdown values before
            // returning the form to the View.
            await PopulateFormOptionsAsync(model);


            return model;
        }

        /// <summary>
        /// Updates a Target and its Assessment-specific
        /// authorization information.
        ///
        /// Security-sensitive changes invalidate an existing
        /// approval because the previous approval no longer
        /// represents the current target configuration.
        /// </summary>
        public async Task<(
            bool Success,
            string? ErrorMessage,
            bool ApprovalReset,
            int? AssessmentTargetID)>
            UpdateTargetAsync(
                TargetFormViewModel model,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            var assessmentTarget =
                await _context.trn_AssessmentTarget

                    .Include(x => x.Target)

                    .Include(x => x.TargetAuthorizationStatus)

                    .FirstOrDefaultAsync(x =>
                        x.AssessmentTargetID ==
                            model.AssessmentTargetID &&
                        x.IsActive == true);


            if (assessmentTarget == null)
            {
                return (
                    false,
                    "Assessment target could not be found.",
                    false,
                    null
                );
            }


            // A revoked authorization is intentionally locked.
            // Reinstatement should eventually be a separate,
            // explicitly audited workflow.
            var currentStatusCode =
                assessmentTarget
                    .TargetAuthorizationStatus
                    .AuthorizationStatusCode;


            if (currentStatusCode == "REVOKED")
            {
                return (
                    false,
                    "A revoked target cannot be edited.",
                    false,
                    assessmentTarget.AssessmentTargetID
                );
            }


            var targetValue =
                model.TargetValue.Trim();

            // Target Type and Target Value must still form
            // a valid combination after editing.
            var validation =
                await _targetValidationService
                    .ValidateTargetValueAsync(
                        model.TargetTypeID,
                        targetValue);


            if (!validation.IsValid)
            {
                return (
                    false,
                    validation.ErrorMessage,
                    false,
                    assessmentTarget.AssessmentTargetID
                );
            }

            var targetName =
                model.TargetName.Trim();

            var description =
                model.Description?.Trim();

            var authorizationReference =
                model.AuthorizationReference?.Trim();

            var authorizationNotes =
                model.AuthorizationNotes?.Trim();


            /*
             * Prevent another active target with the same
             * type/value combination from existing in this
             * same Assessment.
             */
            var duplicateExists =
                await _context.trn_AssessmentTarget

                    .Where(x =>
                        x.AssessmentID ==
                            assessmentTarget.AssessmentID &&

                        x.AssessmentTargetID !=
                            assessmentTarget.AssessmentTargetID &&

                        x.IsActive == true)

                    .AnyAsync(x =>
                        x.Target.TargetTypeID ==
                            model.TargetTypeID &&

                        x.Target.TargetValue ==
                            targetValue);


            if (duplicateExists)
            {
                return (
                    false,
                    "This target is already assigned to the assessment.",
                    false,
                    assessmentTarget.AssessmentTargetID
                );
            }


            /*
             * Save the original values BEFORE modifying the
             * EF-tracked entities. These will be written to
             * AuditTrail as the previous state.
             */
            var oldTargetValues = new
            {
                assessmentTarget.Target.TargetTypeID,
                assessmentTarget.Target.TargetName,
                assessmentTarget.Target.TargetValue,
                assessmentTarget.Target.Description
            };


            var oldAuthorizationValues = new
            {
                assessmentTarget.AuthorizationReference,
                assessmentTarget.AuthorizationNotes,
                assessmentTarget.AuthorizationValidFrom,
                assessmentTarget.AuthorizationValidUntil
            };


            /*
             * These fields affect what system may be tested
             * or under which authorization conditions.
             *
             * Changing them invalidates an existing approval.
             */
            var targetIdentityChanged =
                assessmentTarget.Target.TargetTypeID !=
                    model.TargetTypeID ||

                !string.Equals(
                    assessmentTarget.Target.TargetValue,
                    targetValue,
                    StringComparison.OrdinalIgnoreCase);


            var authorizationChanged =
                !string.Equals(
                    assessmentTarget.AuthorizationReference,
                    authorizationReference,
                    StringComparison.Ordinal) ||

                assessmentTarget.AuthorizationValidFrom !=
                    model.AuthorizationValidFrom ||

                assessmentTarget.AuthorizationValidUntil !=
                    model.AuthorizationValidUntil;


            var approvalSensitiveChange =
                targetIdentityChanged ||
                authorizationChanged;


            // Detect ordinary descriptive changes separately.
            var targetDataChanged =
                targetIdentityChanged ||

                !string.Equals(
                    assessmentTarget.Target.TargetName,
                    targetName,
                    StringComparison.Ordinal) ||

                !string.Equals(
                    assessmentTarget.Target.Description,
                    description,
                    StringComparison.Ordinal);


            var assignmentDataChanged =
                authorizationChanged ||

                !string.Equals(
                    assessmentTarget.AuthorizationNotes,
                    authorizationNotes,
                    StringComparison.Ordinal);


            /*
             * A rejected target that is edited is considered
             * corrected/resubmitted and therefore returns to
             * Pending Approval.
             */
            var rejectedResubmission =
                currentStatusCode == "REJECTED" &&
                (targetDataChanged || assignmentDataChanged);


            var statusMustReturnToPending =
                (currentStatusCode == "APPROVED" &&
                 approvalSensitiveChange)
                ||
                rejectedResubmission;


            ref_TargetAuthorizationStatus? pendingStatus = null;


            if (statusMustReturnToPending)
            {
                pendingStatus =
                    await _context.ref_TargetAuthorizationStatus
                        .FirstOrDefaultAsync(x =>
                            x.AuthorizationStatusCode ==
                                "PENDING" &&
                            x.IsActive == true);


                if (pendingStatus == null)
                {
                    return (
                        false,
                        "Pending authorization status is not configured.",
                        false,
                        assessmentTarget.AssessmentTargetID
                    );
                }
            }


            // Nothing changed. Avoid creating unnecessary
            // UPDATE and AuditTrail records.
            if (!targetDataChanged &&
                !assignmentDataChanged)
            {
                return (
                    false,
                    "No changes were detected.",
                    false,
                    assessmentTarget.AssessmentTargetID
                );
            }


            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            try
            {
                /*
                 * Update the reusable master target information.
                 */
                assessmentTarget.Target.TargetTypeID =
                    model.TargetTypeID;

                assessmentTarget.Target.TargetName =
                    targetName;

                assessmentTarget.Target.TargetValue =
                    targetValue;

                assessmentTarget.Target.Description =
                    description;

                assessmentTarget.Target.EditedBy =
                    userId;

                assessmentTarget.Target.EditedDate =
                    DateTime.Now;


                /*
                 * Update Assessment-specific authorization
                 * information.
                 */
                assessmentTarget.AuthorizationReference =
                    authorizationReference;

                assessmentTarget.AuthorizationNotes =
                    authorizationNotes;

                assessmentTarget.AuthorizationValidFrom =
                    model.AuthorizationValidFrom;

                assessmentTarget.AuthorizationValidUntil =
                    model.AuthorizationValidUntil;

                assessmentTarget.EditedBy =
                    userId;

                assessmentTarget.EditedDate =
                    DateTime.Now;


                var approvalReset = false;


                if (statusMustReturnToPending)
                {
                    assessmentTarget
                        .TargetAuthorizationStatusID =
                            pendingStatus!
                                .TargetAuthorizationStatusID;


                    // Previous approval no longer applies.
                    assessmentTarget.AuthorizedByUserID =
                        null;

                    assessmentTarget.AuthorizedDate =
                        null;


                    approvalReset = true;
                }


                await _context.SaveChangesAsync();


                /*
                 * Audit changes made to mst_Target.
                 */
                if (targetDataChanged)
                {
                    await _auditTrailService.AddAsync(
                        userId: userId,
                        userName: userName,
                        actionType: "UPDATE",
                        module: "Targets & Scope",
                        tableName: "mst_Target",
                        recordId:
                            assessmentTarget.Target.TargetID
                                .ToString(),
                        description:
                            $"Updated target {assessmentTarget.Target.TargetValue}.",
                        oldValues:
                            oldTargetValues,
                        newValues: new
                        {
                            assessmentTarget.Target.TargetTypeID,
                            assessmentTarget.Target.TargetName,
                            assessmentTarget.Target.TargetValue,
                            assessmentTarget.Target.Description
                        },
                        requestPath:
                            requestPath,
                        ipAddress:
                            ipAddress,
                        isSuccessful:
                            true
                    );
                }


                /*
                 * Audit Assessment-specific authorization changes.
                 */
                if (assignmentDataChanged)
                {
                    await _auditTrailService.AddAsync(
                        userId: userId,
                        userName: userName,
                        actionType: "UPDATE",
                        module: "Targets & Scope",
                        tableName: "trn_AssessmentTarget",
                        recordId:
                            assessmentTarget
                                .AssessmentTargetID
                                .ToString(),
                        description:
                            $"Updated authorization information for target {assessmentTarget.Target.TargetValue}.",
                        oldValues:
                            oldAuthorizationValues,
                        newValues: new
                        {
                            assessmentTarget.AuthorizationReference,
                            assessmentTarget.AuthorizationNotes,
                            assessmentTarget.AuthorizationValidFrom,
                            assessmentTarget.AuthorizationValidUntil
                        },
                        requestPath:
                            requestPath,
                        ipAddress:
                            ipAddress,
                        isSuccessful:
                            true
                    );
                }


                /*
                 * Keep approval invalidation separate in AuditTrail
                 * because this is an important security event.
                 */
                if (approvalReset)
                {
                    await _auditTrailService.AddAsync(
                        userId: userId,
                        userName: userName,
                        actionType:
                            rejectedResubmission
                                ? "RESUBMIT"
                                : "RESET_APPROVAL",
                        module:
                            "Targets & Scope",
                        tableName:
                            "trn_AssessmentTarget",
                        recordId:
                            assessmentTarget
                                .AssessmentTargetID
                                .ToString(),
                        description:
                            rejectedResubmission
                                ? $"Target {assessmentTarget.Target.TargetValue} was edited and resubmitted for approval."
                                : $"Target {assessmentTarget.Target.TargetValue} returned to Pending Approval because approval-sensitive information changed.",
                        oldValues: new
                        {
                            AuthorizationStatus =
                                currentStatusCode
                        },
                        newValues: new
                        {
                            AuthorizationStatus =
                                "PENDING"
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

                await transaction.CommitAsync();


                return (
                    true,
                    null,
                    approvalReset,
                    assessmentTarget.AssessmentTargetID
                );
            }
            catch
            {
                // Target changes, authorization changes and
                // audit entries must behave as one transaction.
                await transaction.RollbackAsync();

                throw;
            }
        }

        /// <summary>
        /// Deactivates a target from its Assessment.
        ///
        /// The record is retained for historical/audit purposes.
        /// Any active scope rules are also deactivated because the
        /// Assessment Target is no longer part of the active scope.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage, int? AssessmentID)>
            DeactivateTargetAsync(
                int assessmentTargetId,
                string userId,
                string userName,
                string? requestPath,
                string? ipAddress)
        {
            var assessmentTarget =
                await _context.trn_AssessmentTarget

                    // Target is required for a meaningful audit description.
                    .Include(x => x.Target)

                    // Load the current authorization status before changing it.
                    .Include(x => x.TargetAuthorizationStatus)

                    .FirstOrDefaultAsync(x =>
                        x.AssessmentTargetID == assessmentTargetId &&
                        x.IsActive == true);


            if (assessmentTarget == null)
            {
                return (
                    false,
                    "Assessment target could not be found.",
                    null
                );
            }


            // Retrieve the REVOKED status.
            // We use the stable status code rather than assuming a numeric ID.
            var revokedStatus =
                await _context.ref_TargetAuthorizationStatus
                    .FirstOrDefaultAsync(x =>
                        x.AuthorizationStatusCode == "REVOKED" &&
                        x.IsActive == true);


            if (revokedStatus == null)
            {
                return (
                    false,
                    "Revoked authorization status is not configured.",
                    assessmentTarget.AssessmentID
                );
            }


            // Preserve the original values before changing the EF-tracked entity.
            var oldValues = new
            {
                assessmentTarget.AssessmentTargetID,
                assessmentTarget.AssessmentID,
                assessmentTarget.TargetID,

                AuthorizationStatus =
                    assessmentTarget
                        .TargetAuthorizationStatus
                        .AuthorizationStatusCode,

                assessmentTarget.IsActive
            };


            await using var transaction =
                await _context.Database.BeginTransactionAsync();


            try
            {
                /*
                 * Soft-deactivate the Assessment/Target relationship.
                 *
                 * We do not physically DELETE the record because it
                 * forms part of the VAPT engagement history.
                 */
                assessmentTarget.IsActive = false;

                assessmentTarget.TargetAuthorizationStatusID =
                    revokedStatus.TargetAuthorizationStatusID;

                assessmentTarget.EditedBy =
                    userId;

                assessmentTarget.EditedDate =
                    DateTime.Now;


                /*
                 * Scope rules under an inactive Assessment Target
                 * should no longer be considered active boundaries.
                 */
                var activeScopeRules =
                    await _context.trn_AssessmentScopeRule
                        .Where(x =>
                            x.AssessmentTargetID == assessmentTargetId &&
                            x.IsActive == true)
                        .ToListAsync();


                foreach (var scopeRule in activeScopeRules)
                {
                    scopeRule.IsActive = false;
                    scopeRule.EditedBy = userId;
                    scopeRule.EditedDate = DateTime.Now;
                }


                await _context.SaveChangesAsync();


                // Record the target deactivation as the main user action.
                await _auditTrailService.AddAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "DEACTIVATE",
                    module: "Targets & Scope",
                    tableName: "trn_AssessmentTarget",
                    recordId:
                        assessmentTarget.AssessmentTargetID.ToString(),
                    description:
                        $"Deactivated target {assessmentTarget.Target.TargetValue} from assessment ID {assessmentTarget.AssessmentID}.",
                    oldValues: oldValues,
                    newValues: new
                    {
                        assessmentTarget.AssessmentTargetID,

                        AuthorizationStatus =
                            "REVOKED",

                        IsActive =
                            false,

                        DeactivatedScopeRuleCount =
                            activeScopeRules.Count
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
                    assessmentTarget.AssessmentID
                );
            }
            catch
            {
                // Target deactivation, scope-rule changes and
                // AuditTrail must succeed or fail together.
                await transaction.RollbackAsync();

                throw;
            }
        }

        /// <summary>
        /// Determines whether an Assessment Target is currently
        /// eligible from an authorization perspective.
        ///
        /// This does not start a scan. It provides the security
        /// check that future ScanJob creation will call.
        /// </summary>
        public async Task<bool>
            IsTargetCurrentlyAuthorizedAsync(
                int assessmentTargetId)
        {
            var target =
                await _context.trn_AssessmentTarget

                    .Include(x =>
                        x.TargetAuthorizationStatus)

                    .AsNoTracking()

                    .FirstOrDefaultAsync(x =>
                        x.AssessmentTargetID ==
                            assessmentTargetId &&
                        x.IsActive == true);


            if (target == null)
            {
                return false;
            }


            // Only explicitly Approved targets may ever pass.
            if (target
                .TargetAuthorizationStatus
                .AuthorizationStatusCode != "APPROVED")
            {
                return false;
            }


            var now = DateTime.Now;


            // Authorization becomes valid at the beginning
            // of the selected Valid From date.
            if (target.AuthorizationValidFrom.HasValue &&
                now <
                    target.AuthorizationValidFrom.Value.Date)
            {
                return false;
            }


            // Authorization remains valid through the entire
            // selected Valid Until date.
            if (target.AuthorizationValidUntil.HasValue)
            {
                var validUntilExclusive =
                    target.AuthorizationValidUntil.Value.Date
                        .AddDays(1);


                if (now >= validUntilExclusive)
                {
                    return false;
                }
            }


            return true;


        }

    }
}