using EVOLVE_VAPT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EVOLVE_VAPT.Controllers
{
    [Authorize]
    public class FindingController : Controller
    {
        private readonly FindingService
            _findingService;

        private readonly UserManager<IdentityUser>
            _userManager;


        public FindingController(
            FindingService findingService,
            UserManager<IdentityUser> userManager)
        {
            _findingService =
                findingService;

            _userManager =
                userManager;
        }


        [Authorize(Policy = "CanViewFindings")]
        public async Task<IActionResult> Index()
        {
            var model =
                await _findingService
                    .GetFindingsAsync();


            return View(model);
        }


        [Authorize(Policy = "CanViewFindings")]
        public async Task<IActionResult> Details(
            long id)
        {
            var model =
                await _findingService
                    .GetFindingDetailsAsync(id);


            if (model == null)
            {
                return NotFound();
            }


            return View(model);
        }


        [Authorize(Policy = "CanReviewFindings")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartReview(
            long id)
        {
            var user =
                await _userManager
                    .GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _findingService
                    .StartReviewAsync(
                        id,
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
                    ? "Finding moved to Under Review."
                    : result.ErrorMessage;


            return RedirectToAction(
                nameof(Details),
                new { id });
        }


        [Authorize(Policy = "CanReviewFindings")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(
            long id,
            string? notes)
        {
            var user =
                await _userManager
                    .GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _findingService
                    .ConfirmFindingAsync(
                        id,
                        notes,
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
                    ? "Finding confirmed."
                    : result.ErrorMessage;


            return RedirectToAction(
                nameof(Details),
                new { id });
        }


        [Authorize(Policy = "CanReviewFindings")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FalsePositive(
            long id,
            string reason)
        {
            var user =
                await _userManager
                    .GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _findingService
                    .MarkFalsePositiveAsync(
                        id,
                        reason,
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
                    ? "Finding marked as False Positive."
                    : result.ErrorMessage;


            return RedirectToAction(
                nameof(Details),
                new { id });
        }
    }
}