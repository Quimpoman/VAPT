using EVOLVE_VAPT.Services;
using EVOLVE_VAPT.ViewModels.Remediation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EVOLVE_VAPT.Controllers
{
    [Authorize]
    public class RemediationController : Controller
    {
        private readonly RemediationService
            _remediationService;

        private readonly UserManager<IdentityUser>
            _userManager;


        public RemediationController(
            RemediationService remediationService,
            UserManager<IdentityUser> userManager)
        {
            _remediationService =
                remediationService;

            _userManager =
                userManager;
        }


        [Authorize(Policy = "CanViewRemediation")]
        public async Task<IActionResult> Index()
        {
            var model =
                await _remediationService
                    .GetRemediationsAsync();

            return View(model);
        }


        [Authorize(Policy = "CanViewRemediation")]
        public async Task<IActionResult> Details(
            long id)
        {
            var model =
                await _remediationService
                    .GetRemediationDetailsAsync(id);


            if (model == null)
            {
                return NotFound();
            }


            return View(model);
        }


        [Authorize(Policy = "CanManageRemediation")]
        [HttpGet]
        public async Task<IActionResult> Create(
            long findingId)
        {
            var model =
                await _remediationService
                    .GetStartFormAsync(
                        findingId);


            if (model == null)
            {
                return NotFound();
            }


            return View(model);
        }


        [Authorize(Policy = "CanManageRemediation")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            RemediationFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await _remediationService
                    .PopulateUserOptionsAsync(
                        model);

                return View(model);
            }


            var user =
                await _userManager
                    .GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _remediationService
                    .StartRemediationAsync(
                        model,
                        user.Id,
                        user.Email
                            ?? user.UserName
                            ?? user.Id,
                        HttpContext.Request.Path,
                        HttpContext.Connection
                            .RemoteIpAddress
                            ?.ToString());


            if (!result.Success)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage!);

                await _remediationService
                    .PopulateUserOptionsAsync(
                        model);

                return View(model);
            }


            TempData["SuccessMessage"] =
                "Remediation started successfully.";


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        result
                            .FindingRemediationID
                });
        }

        [Authorize(Policy = "CanManageRemediation")]
        [HttpGet]
        public async Task<IActionResult> Edit(
        long id)
        {
            var model =
                await _remediationService
                    .GetRemediationForEditAsync(
                        id);


            if (model == null)
            {
                return NotFound();
            }


            return View(model);
        }


        [Authorize(Policy = "CanManageRemediation")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            RemediationFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await _remediationService
                    .PopulateUserOptionsAsync(
                        model);

                return View(model);
            }


            var user =
                await _userManager
                    .GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _remediationService
                    .UpdateRemediationAsync(
                        model,
                        user.Id,
                        user.Email
                            ?? user.UserName
                            ?? user.Id,
                        HttpContext.Request.Path,
                        HttpContext.Connection
                            .RemoteIpAddress
                            ?.ToString());


            TempData[
                result.Success
                    ? "SuccessMessage"
                    : "ErrorMessage"] =
                result.Success
                    ? "Remediation updated successfully."
                    : result.ErrorMessage;


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        model.FindingRemediationID
                });
        }


        [Authorize(Policy = "CanManageRemediation")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRetest(
            long id,
            string submissionNotes)
        {
            var user =
                await _userManager
                    .GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _remediationService
                    .SubmitForRetestAsync(
                        id,
                        submissionNotes,
                        user.Id,
                        user.Email
                            ?? user.UserName
                            ?? user.Id,
                        HttpContext.Request.Path,
                        HttpContext.Connection
                            .RemoteIpAddress
                            ?.ToString());


            TempData[
                result.Success
                    ? "SuccessMessage"
                    : "ErrorMessage"] =
                result.Success
                    ? "Remediation submitted for retesting."
                    : result.ErrorMessage;


            return RedirectToAction(
                nameof(Details),
                new { id });
        }
    }
}
