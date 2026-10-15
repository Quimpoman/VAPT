using EVOLVE_VAPT.Services;
using EVOLVE_VAPT.ViewModels.ScanProfile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EVOLVE_VAPT.Controllers
{
    [Authorize]
    public class ScanProfileController : Controller
    {
        private readonly ScanProfileService _scanProfileService;
        private readonly UserManager<IdentityUser> _userManager;


        public ScanProfileController(
            ScanProfileService scanProfileService,
            UserManager<IdentityUser> userManager)
        {
            _scanProfileService =
                scanProfileService;

            _userManager =
                userManager;
        }


        /// <summary>
        /// Displays active Scan Profiles.
        /// </summary>
        [Authorize(Policy = "CanViewScanProfiles")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model =
                await _scanProfileService
                    .GetScanProfilesAsync();


            return View(model);
        }


        /// <summary>
        /// Displays the Create Scan Profile form.
        /// </summary>
        [Authorize(Policy = "CanManageScanProfiles")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model =
                new ScanProfileFormViewModel();


            await _scanProfileService
                .PopulateFormOptionsAsync(model);


            return View(model);
        }


        /// <summary>
        /// Creates a new reusable Scan Profile.
        /// </summary>
        [Authorize(Policy = "CanManageScanProfiles")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            ScanProfileFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await _scanProfileService
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
                await _scanProfileService
                    .CreateScanProfileAsync(
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
                    ?? "Unable to create Scan Profile.");


                await _scanProfileService
                    .PopulateFormOptionsAsync(model);


                return View(model);
            }


            TempData["SuccessMessage"] =
                "Scan Profile created successfully.";


            return RedirectToAction(
                nameof(Index));
        }



        /// <summary>
        /// Displays a Scan Profile and its controlled
        /// scanner-option configuration.
        /// </summary>
        [Authorize(Policy = "CanViewScanProfiles")]
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model =
                await _scanProfileService
                    .GetScanProfileDetailsAsync(id);


            if (model == null)
            {
                return NotFound();
            }


            return View(model);
        }


        /// <summary>
        /// Creates or updates one option belonging to
        /// the Scan Profile's selected scanner.
        /// </summary>
        [Authorize(Policy = "CanManageScanProfiles")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOption(
            int scanProfileId,
            int scannerOptionId,
            string? optionValue)
        {
            var user =
                await _userManager.GetUserAsync(User);


            if (user == null)
            {
                return Challenge();
            }


            var result =
                await _scanProfileService
                    .SaveScanProfileOptionAsync(
                        scanProfileId,
                        scannerOptionId,
                        optionValue,
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
                    result.ErrorMessage
                    ?? "Unable to save scanner option.";
            }
            else
            {
                TempData["SuccessMessage"] =
                    "Scanner option saved successfully.";
            }


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id = scanProfileId
                });
        }

        /// <summary>
        /// Displays an existing Scan Profile for editing.
        /// </summary>
        [Authorize(Policy = "CanManageScanProfiles")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model =
                await _scanProfileService
                    .GetScanProfileForEditAsync(id);


            if (model == null)
            {
                return NotFound();
            }


            return View(model);
        }

        /// <summary>
        /// Saves changes to a Scan Profile.
        /// </summary>
        [Authorize(Policy = "CanManageScanProfiles")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            ScanProfileFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await _scanProfileService
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
                await _scanProfileService
                    .UpdateScanProfileAsync(
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
                    ?? "Unable to update Scan Profile.");


                await _scanProfileService
                    .PopulateFormOptionsAsync(model);


                return View(model);
            }


            TempData["SuccessMessage"] =
                result.OptionsReset
                    ? "Scan Profile updated successfully. Existing scanner options were reset because the scanner configuration changed."
                    : "Scan Profile updated successfully.";


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id = model.ScanProfileID
                });
        }


        /// <summary>
        /// Soft-deactivates a Scan Profile.
        /// </summary>
        [Authorize(Policy = "CanManageScanProfiles")]
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
                await _scanProfileService
                    .DeactivateScanProfileAsync(
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
                    result.ErrorMessage
                    ?? "Unable to deactivate Scan Profile.";


                return RedirectToAction(
                    nameof(Index));
            }


            TempData["SuccessMessage"] =
                "Scan Profile deactivated successfully.";


            return RedirectToAction(
                nameof(Index));
        }

    }
}