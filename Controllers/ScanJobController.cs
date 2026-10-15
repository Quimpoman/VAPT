using EVOLVE_VAPT.Services;
using EVOLVE_VAPT.ViewModels.ScanJob;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EVOLVE_VAPT.Controllers
{
    [Authorize]
    public class ScanJobController : Controller
    {
        private readonly ScanJobService _scanJobService;
        private readonly UserManager<IdentityUser> _userManager;


        public ScanJobController(
            ScanJobService scanJobService,
            UserManager<IdentityUser> userManager)
        {
            _scanJobService =
                scanJobService;

            _userManager =
                userManager;
        }


        [Authorize(Policy = "CanViewScanJobs")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model =
                await _scanJobService
                    .GetScanJobsAsync();


            return View(model);
        }


        [Authorize(Policy = "CanCreateScanJobs")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model =
                new ScanJobCreateViewModel();


            await _scanJobService
                .PopulateCreateOptionsAsync(model);


            return View(model);
        }


        [Authorize(Policy = "CanCreateScanJobs")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            ScanJobCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await _scanJobService
                    .PopulateCreateOptionsAsync(model);

                return View(model);
            }


            var user =
                await _userManager.GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _scanJobService
                    .CreateScanJobAsync(
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
                    result.ErrorMessage
                    ?? "Unable to create Scan Job.");


                await _scanJobService
                    .PopulateCreateOptionsAsync(model);


                return View(model);
            }


            TempData["SuccessMessage"] =
                "Scan Job created successfully.";


            return RedirectToAction(
                nameof(Index));
        }

        [Authorize(Policy = "CanViewScanJobs")]
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var model =
                await _scanJobService
                    .GetScanJobDetailsAsync(id);


            if (model == null)
            {
                return NotFound();
            }


            return View(model);
        }

        [Authorize(Policy = "CanApproveScanJobs")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(long id, string? approvalNotes)
        {
            var user =
                await _userManager.GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _scanJobService
                    .ApproveScanJobAsync(
                        id,
                        user.Id,
                        user.Email
                            ?? user.UserName
                            ?? user.Id,
                        approvalNotes,
                        HttpContext.Request.Path,
                        HttpContext.Connection
                            .RemoteIpAddress
                            ?.ToString()
                    );


            TempData[
                result.Success
                    ? "SuccessMessage"
                    : "ErrorMessage"] =
                result.Success
                    ? "Scan Job approved and queued successfully."
                    : result.ErrorMessage;


            return RedirectToAction(
                nameof(Details),
                new { id });
        }


        [Authorize(Policy = "CanApproveScanJobs")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(long id, string reason)
        {
            var user =
                await _userManager.GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _scanJobService
                    .RejectScanJobAsync(
                        id,
                        reason,
                        user.Id,
                        user.Email
                            ?? user.UserName
                            ?? user.Id,
                        HttpContext.Request.Path,
                        HttpContext.Connection
                            .RemoteIpAddress
                            ?.ToString()
                    );


            TempData[
                result.Success
                    ? "SuccessMessage"
                    : "ErrorMessage"] =
                result.Success
                    ? "Scan Job rejected."
                    : result.ErrorMessage;


            return RedirectToAction(
                nameof(Details),
                new { id });
        }


        [Authorize(Policy = "CanCancelScanJobs")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(
    long id,
    string reason)
        {
            var user =
                await _userManager.GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _scanJobService
                    .CancelScanJobAsync(
                        id,
                        reason,
                        user.Id,
                        user.Email
                            ?? user.UserName
                            ?? user.Id,
                        HttpContext.Request.Path,
                        HttpContext.Connection
                            .RemoteIpAddress
                            ?.ToString()
                    );


            TempData[
                result.Success
                    ? "SuccessMessage"
                    : "ErrorMessage"] =
                result.Success
                    ? "Scan Job cancelled successfully."
                    : result.ErrorMessage;


            return RedirectToAction(
                nameof(Details),
                new { id });
        }

    }
}