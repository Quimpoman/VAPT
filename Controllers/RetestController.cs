using EVOLVE_VAPT.Services;
using EVOLVE_VAPT.ViewModels.Retest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;


namespace EVOLVE_VAPT.Controllers
{
    [Authorize]
    public class RetestController : Controller
    {
        private readonly RetestService
            _retestService;

        private readonly UserManager<IdentityUser>
            _userManager;


        public RetestController(
            RetestService retestService,
            UserManager<IdentityUser> userManager)
        {
            _retestService =
                retestService;

            _userManager =
                userManager;
        }


        [Authorize(Policy = "CanCreateRetests")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            long remediationId)
        {
            var user =
                await _userManager
                    .GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _retestService
                    .CreateRetestAsync(
                        remediationId,
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
                    ? "Retest Scan Job created successfully."
                    : result.ErrorMessage;


            if (result.Success &&
                result.ScanJobID.HasValue)
            {
                return RedirectToAction(
                    "Details",
                    "ScanJob",
                    new
                    {
                        id =
                            result.ScanJobID.Value
                    });
            }


            return RedirectToAction(
                "Details",
                "Remediation",
                new
                {
                    id =
                        remediationId
                });
        }

        [Authorize(Policy = "CanViewRetests")]
        public async Task<IActionResult> Index()
        {
            var model =
                await _retestService
                    .GetRetestsAsync();


            return View(model);
        }

        [Authorize(Policy = "CanViewRetests")]
        public async Task<IActionResult> Details(long id)
        {
            var model =
                await _retestService
                    .GetRetestDetailsAsync(id);


            if (model == null)
            {
                return NotFound();
            }


            if (model.RetestStatusCode ==
                "AWAITING_REVIEW")
            {
                var reviewModel =
                    new RetestReviewViewModel
                    {
                        FindingRetestID =
                            model.FindingRetestID
                    };


                await _retestService
                    .PopulateOutcomeOptionsAsync(
                        reviewModel);


                ViewBag.OutcomeOptions =
                    reviewModel.OutcomeOptions;
            }


            return View(model);
        }
        [Authorize(Policy = "CanReviewRetests")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(
    RetestReviewViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    "Please select an outcome and provide review notes.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id =
                            model.FindingRetestID
                    });
            }


            var user =
                await _userManager
                    .GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _retestService
                    .ReviewRetestAsync(
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
                    ? "Retest review completed successfully."
                    : result.ErrorMessage;


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        model.FindingRetestID
                });
        }
    }
}