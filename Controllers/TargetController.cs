using EVOLVE_VAPT.Services;
using EVOLVE_VAPT.ViewModels.Target;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Controllers
{
    /*
     * Targets & Scope can directly affect what the VAPT
     * platform is eventually allowed to scan.
     *
     * For the current MVP, restrict management access
     * to users who can perform assessments.
     */

    //Assessment
    //   ↓
    //Which targets belong to it?
    //   ↓
    //Are they actually authorized?
    //   ↓
    //What are the permitted/excluded boundaries?
    //   ↓
    //Only then can scanning eventually occur.
    
    // All actions require an authenticated user.
    // Individual actions below apply more specific policies.

    [Authorize]
    public class TargetController : Controller
    {
        private readonly TargetService _targetService;
        private readonly AssessmentService _assessmentService;
        private readonly UserManager<IdentityUser> _userManager;

        public TargetController(TargetService targetService,
     AssessmentService assessmentService,
     UserManager<IdentityUser> userManager)
        {
            _targetService = targetService;
            _assessmentService = assessmentService;
            _userManager = userManager;
        }

        [Authorize(Policy = "CanViewTargets")]
        public async Task<IActionResult> Index(
            int assessmentId)
        {
            /*
             * Confirm the parent assessment exists before
             * displaying its Targets & Scope workspace.
             */
            var assessment =
                await _assessmentService
                    .GetAssessmentDetailsAsync(assessmentId);

            if (assessment == null)
            {
                return NotFound();
            }


            var targets =
                await _targetService
                    .GetAssessmentTargetsAsync(
                        assessmentId);


            /*
             * Pass small pieces of assessment context to
             * the page without adding them to every target row.
             */
            ViewData["AssessmentID"] =
                assessment.AssessmentID;

            ViewData["AssessmentCode"] =
                assessment.AssessmentCode;

            ViewData["AssessmentName"] =
                assessment.AssessmentName;


            return View(targets);
        }

        /// <summary>
        /// Displays the Add Target form for one Assessment.
        /// </summary>
        [Authorize(Policy = "CanManageTargets")]
        [HttpGet]
        public async Task<IActionResult> Create(
            int assessmentId)
        {
            // Prevent users from adding targets to an
            // assessment that does not exist or is inactive.
            var assessment =
                await _assessmentService
                    .GetAssessmentDetailsAsync(assessmentId);

            if (assessment == null)
            {
                return NotFound();
            }


            var model =
                new TargetFormViewModel
                {
                    AssessmentID =
                        assessmentId
                };


            await _targetService
                .PopulateFormOptionsAsync(model);


            ViewData["AssessmentCode"] =
                assessment.AssessmentCode;

            ViewData["AssessmentName"] =
                assessment.AssessmentName;


            return View(model);
        }

        /// <summary>
        /// Creates a new target assignment.
        ///
        /// The TargetService enforces Pending Approval;
        /// this action does not authorize scanning.
        /// </summary>

        [Authorize(Policy = "CanManageTargets")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            TargetFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await _targetService
                    .PopulateFormOptionsAsync(model);

                return View(model);
            }


            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _targetService.CreateTargetAsync(
                    model,
                    user.Id,
                    user.Email
                        ?? user.UserName
                        ?? user.Id,
                    HttpContext.Request.Path,
                    HttpContext.Connection
                        .RemoteIpAddress
                        ?.ToString()
                );


            if (!result.Success)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage!);

                await _targetService
                    .PopulateFormOptionsAsync(model);

                return View(model);
            }


            TempData["SuccessMessage"] =
                "Target added successfully and is pending approval.";


            return RedirectToAction(
                nameof(Index),
                new
                {
                    assessmentId =
                        model.AssessmentID
                });
        }


        /// <summary>
        /// Displays the complete details and authorization state
        /// of one target assigned to an assessment.
        /// </summary>
        [Authorize(Policy = "CanViewTargets")]
        public async Task<IActionResult> Details(int id)
        {
            var target =
                await _targetService
                    .GetTargetDetailsAsync(id);

            if (target == null)
            {
                return NotFound();
            }

            return View(target);
        }

        /// <summary>
        /// Explicit approval action.
        /// Only CanApproveTargets users may execute this.
        /// </summary>
        [Authorize(Policy = "CanApproveTargets")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _targetService.ApproveTargetAsync(
                    id,
                    user.Id,
                    user.Email ?? user.UserName ?? user.Id,
                    HttpContext.Request.Path,
                    HttpContext.Connection.RemoteIpAddress?.ToString()
                );


            if (!result.Success)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage;

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }


            TempData["SuccessMessage"] =
                "Target approved successfully.";


            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        /// <summary>
        /// Explicit target rejection action.
        /// The supplied reason becomes part of the audit record.
        /// </summary>
        [Authorize(Policy = "CanApproveTargets")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(
            int id,
            string reason)
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _targetService.RejectTargetAsync(
                    id,
                    reason,
                    user.Id,
                    user.Email ?? user.UserName ?? user.Id,
                    HttpContext.Request.Path,
                    HttpContext.Connection.RemoteIpAddress?.ToString()
                );


            if (!result.Success)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage;
            }
            else
            {
                TempData["SuccessMessage"] =
                    "Target rejected.";
            }


            return RedirectToAction(
                nameof(Details),
                new { id });
        }


        /// <summary>
        /// Displays the form used to add a boundary rule
        /// to one Assessment Target.
        /// </summary>
        [Authorize(Policy = "CanManageTargets")]
        [HttpGet]
        public async Task<IActionResult> CreateScopeRule(
            int assessmentTargetId)
        {
            var target =
                await _targetService
                    .GetTargetDetailsAsync(assessmentTargetId);

            if (target == null)
            {
                return NotFound();
            }


            /*
             * Rejected or revoked targets cannot have their
             * scope changed until they return to an editable state.
             */
            if (target.AuthorizationStatusCode == "REJECTED" ||
                target.AuthorizationStatusCode == "REVOKED")
            {
                TempData["ErrorMessage"] =
                    "Scope rules cannot be added to a rejected or revoked target.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = assessmentTargetId });
            }


            var model = new ScopeRuleFormViewModel
            {
                AssessmentTargetID =
                    assessmentTargetId
            };


            await _targetService
                .PopulateScopeRuleOptionsAsync(model);


            ViewData["TargetName"] =
                target.TargetName;

            ViewData["TargetValue"] =
                target.TargetValue;

            ViewData["AuthorizationStatus"] =
                target.AuthorizationStatusCode;


            return View(model);
        }


        /// <summary>
        /// Saves a new Include/Exclude rule.
        ///
        /// TargetService is responsible for automatically
        /// invalidating approval when an approved scope changes.
        /// </summary>
        [Authorize(Policy = "CanManageTargets")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateScopeRule(
            ScopeRuleFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Dropdown values must be populated again whenever
                // the form is redisplayed after validation failure.
                await _targetService
                    .PopulateScopeRuleOptionsAsync(model);

                return View(model);
            }


            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _targetService
                    .CreateScopeRuleAsync(
                        model,
                        user.Id,
                        user.Email
                            ?? user.UserName
                            ?? user.Id,
                        HttpContext.Request.Path,
                        HttpContext.Connection
                            .RemoteIpAddress
                            ?.ToString()
                    );


            if (!result.Success)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage!);

                await _targetService
                    .PopulateScopeRuleOptionsAsync(model);

                return View(model);
            }


            /*
             * Give the user a clearer message when changing
             * the scope caused an existing approval to reset.
             */
            TempData["SuccessMessage"] =
                result.ApprovalReset
                    ? "Scope rule added successfully. The target has returned to Pending Approval because its approved scope changed."
                    : "Scope rule added successfully.";


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        model.AssessmentTargetID
                });
        }
        

        /// <summary>
        /// Soft-deactivates an Include/Exclude rule.
        ///
        /// This is POST-only because the action changes database state.
        /// </summary>
        /// <summary>
        /// Soft-deactivates an Include/Exclude scope rule.
        /// </summary>
        [Authorize(Policy = "CanManageTargets")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateScopeRule(
            int id)
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _targetService
                    .DeactivateScopeRuleAsync(
                        id,
                        user.Id,
                        user.Email
                            ?? user.UserName
                            ?? user.Id,
                        HttpContext.Request.Path,
                        HttpContext.Connection
                            .RemoteIpAddress
                            ?.ToString()
                    );


            if (!result.Success ||
                !result.AssessmentTargetID.HasValue)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage
                    ?? "Unable to deactivate scope rule.";

                return RedirectToAction(
                    nameof(Index));
            }


            TempData["SuccessMessage"] =
                result.ApprovalReset
                    ? "Scope rule deactivated. The target returned to Pending Approval because its scope changed."
                    : "Scope rule deactivated successfully.";


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        result.AssessmentTargetID.Value
                });
        }


        /// <summary>
        /// Displays the editable information for one
        /// active Assessment Target.
        /// </summary>
        [Authorize(Policy = "CanManageTargets")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model =
                await _targetService
                    .GetTargetForEditAsync(id);


            if (model == null)
            {
                return NotFound();
            }


            return View(model);
        }

        /// <summary>
        /// Saves Target changes.
        ///
        /// TargetService determines whether the modification
        /// invalidates an existing approval.
        /// </summary>
        [Authorize(Policy = "CanManageTargets")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            TargetFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await _targetService
                    .PopulateFormOptionsAsync(model);

                return View(model);
            }


            var user =
                await _userManager.GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _targetService
                    .UpdateTargetAsync(
                        model,
                        user.Id,
                        user.Email
                            ?? user.UserName
                            ?? user.Id,
                        HttpContext.Request.Path,
                        HttpContext.Connection
                            .RemoteIpAddress
                            ?.ToString()
                    );


            if (!result.Success)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage!);

                await _targetService
                    .PopulateFormOptionsAsync(model);

                return View(model);
            }


            TempData["SuccessMessage"] =
                result.ApprovalReset
                    ? "Target updated successfully. The target returned to Pending Approval because approval-sensitive information changed."
                    : "Target updated successfully.";


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        result.AssessmentTargetID!.Value
                });
        }


        /// <summary>
        /// Removes an Assessment Target from the active scope
        /// without physically deleting its historical record.
        /// </summary>
        [Authorize(Policy = "CanManageTargets")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var user =
                await _userManager.GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _targetService.DeactivateTargetAsync(
                    id,
                    user.Id,
                    user.Email
                        ?? user.UserName
                        ?? user.Id,
                    HttpContext.Request.Path,
                    HttpContext.Connection
                        .RemoteIpAddress
                        ?.ToString()
                );


            if (!result.Success ||
                !result.AssessmentID.HasValue)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage
                    ?? "Unable to deactivate target.";

                return RedirectToAction(
                    nameof(Index));
            }


            TempData["SuccessMessage"] =
                "Target deactivated successfully.";


            // Return to the remaining targets under the Assessment.
            return RedirectToAction(
                nameof(Index),
                new
                {
                    assessmentId =
                        result.AssessmentID.Value
                });
        }


    }


}