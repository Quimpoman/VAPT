using EVOLVE_VAPT.Services;
using EVOLVE_VAPT.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EVOLVE_VAPT.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AuditTrailService _auditTrailService;

        public AccountController(
     SignInManager<IdentityUser> signInManager,
     UserManager<IdentityUser> userManager,
     AuditTrailService auditTrailService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditTrailService = auditTrailService;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result =
                await _signInManager.PasswordSignInAsync(
                    model.Email,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: true);

            var identityUser =
    await _userManager.FindByEmailAsync(model.Email);

            if (result.Succeeded)
            {
                try
                {
                    //log successful login attempt
                    await _auditTrailService.LogAsync(
                        userId: identityUser?.Id,
                        userName: identityUser?.Email ?? model.Email,
                        actionType: "LOGIN",
                        module: "Account",
                        tableName: "AspNetUsers",
                        recordId: identityUser?.Id,
                        description: "User logged in successfully.",
                        requestPath: HttpContext.Request.Path,
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                        isSuccessful: true
                    );
                }
                catch
                {
                    // Do not allow an unaudited authenticated session.
                    await _signInManager.SignOutAsync();

                    throw;
                }


                if (!string.IsNullOrEmpty(returnUrl)
                    && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction(
                    "Index",
                    "Home");
            }

            //log unsuccessful login attempt
            await _auditTrailService.LogAsync(
    userId: identityUser?.Id,
    userName: model.Email,
    actionType: "LOGIN",
    module: "Account",
    tableName: "AspNetUsers",
    recordId: identityUser?.Id,
    description: result.IsLockedOut
        ? "Login attempt failed because the account is locked."
        : "Login attempt failed.",
    requestPath: HttpContext.Request.Path,
    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
    isSuccessful: false
);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your account has been temporarily locked.");
            }
            else
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid email or password.");
            }

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var user =
                await _userManager.GetUserAsync(User);

            var userId =
                user?.Id;

            var userName =
                user?.Email
                ?? User.Identity?.Name;


            try
            {
                await _auditTrailService.LogAsync(
                    userId: userId,
                    userName: userName,
                    actionType: "LOGOUT",
                    module: "Account",
                    tableName: "AspNetUsers",
                    recordId: userId,
                    description: "User logged out.",
                    requestPath: HttpContext.Request.Path,
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                    isSuccessful: true
                );
            }
            finally
            {
                await _signInManager.SignOutAsync();
            }


            return RedirectToAction(
                "Login",
                "Account");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}