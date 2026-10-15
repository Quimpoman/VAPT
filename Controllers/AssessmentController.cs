using EVOLVE_VAPT.Services;
using EVOLVE_VAPT.ViewModels.Assessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;


namespace EVOLVE_VAPT.Controllers
{
    [Authorize(Policy = "CanPerformAssessments")]
    public class AssessmentController : Controller
    {
        private readonly AssessmentService _assessmentService;

        private readonly TargetService
            _targetService;

        private readonly ScanJobService
            _scanJobService;

        private readonly UserManager<IdentityUser>
            _userManager;

        private readonly AuditTrailService
            _auditTrailService;

        public AssessmentController(
      AssessmentService assessmentService,
      TargetService targetService,
      ScanJobService scanJobService,
      UserManager<IdentityUser> userManager,
      AuditTrailService auditTrailService)
        {
            _assessmentService =
                assessmentService;

            _targetService =
                targetService;

            _scanJobService =
                scanJobService;

            _userManager =
                userManager;

            _auditTrailService =
                auditTrailService;
        }


        public async Task<IActionResult> Index()
        {
            var assessments =
                await _assessmentService.GetAssessmentsAsync();

            return View(assessments);
        }
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new AssessmentFormViewModel();

            await _assessmentService
                .PopulateFormOptionsAsync(model);

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AssessmentFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await _assessmentService
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
                await _assessmentService.CreateAssessmentAsync(
                    model,
                    user.Id,
                    user.Email ?? user.UserName ?? user.Id,
                    HttpContext.Request.Path,
                    HttpContext.Connection.RemoteIpAddress?.ToString()
                );


            if (!result.Success)
            {
                ModelState.AddModelError(
                    nameof(model.AssessmentCode),
                    result.ErrorMessage!);

                await _assessmentService
                    .PopulateFormOptionsAsync(model);

                return View(model);
            }


            TempData["SuccessMessage"] =
                "Assessment created successfully.";

            return RedirectToAction(nameof(Index));
        }


        public async Task<IActionResult> Details(int id)
        {
            var assessment =
                await _assessmentService
                    .GetAssessmentDetailsAsync(id);

            if (assessment == null)
            {
                return NotFound();
            }
            /*
 * Populate the Assessment overview with its current
 * target assignments and most recent Scan Jobs.
 */
            assessment.Targets =
                await _targetService
                    .GetAssessmentTargetsAsync(id);


            assessment.ScanJobs =
                await _scanJobService
                    .GetAssessmentScanJobsAsync(
                        id,
                        5);


            var user =
                await _userManager.GetUserAsync(User);

            await _auditTrailService.LogAsync(
                userId: user?.Id,
                userName:
                    user?.Email
                    ?? User.Identity?.Name,
                actionType: "VIEW",
                module: "Assessment",
                tableName: "mst_Assessment",
                recordId: id.ToString(),
                description:
                    $"Viewed assessment {assessment.AssessmentCode}.",
                requestPath:
                    HttpContext.Request.Path,
                ipAddress:
                    HttpContext.Connection
                        .RemoteIpAddress
                        ?.ToString(),
                isSuccessful: true
            );


            return View(assessment);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model =
                await _assessmentService
                    .GetAssessmentForEditAsync(id);

            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AssessmentFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await _assessmentService
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
                await _assessmentService
                    .UpdateAssessmentAsync(
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

                await _assessmentService
                    .PopulateFormOptionsAsync(model);

                return View(model);
            }


            TempData["SuccessMessage"] =
                "Assessment updated successfully.";


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id = model.AssessmentID
                });
        }

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
                await _assessmentService
                    .DeactivateAssessmentAsync(
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

            if (!result.Success)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage;

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            TempData["SuccessMessage"] =
                "Assessment deactivated successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}