using EVOLVE_VAPT.Services;
using EVOLVE_VAPT.ViewModels.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EVOLVE_VAPT.Controllers
{
    [Authorize(
        Policy = "CanManageAdministration")]
    public class UserManagementController
        : Controller
    {
        private readonly UserManagementService
            _userManagementService;


        private readonly AuditTrailService _auditTrailService;


        public UserManagementController(
     UserManagementService userManagementService,
     AuditTrailService auditTrailService)
        {
            _userManagementService =
                userManagementService;

            _auditTrailService =
                auditTrailService;
        }
        

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users =
                await _userManagementService
                    .GetUsersAsync();


            return View(users);
        }
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var user =
                await _userManagementService
                    .GetUserDetailsAsync(id);


            if (user == null)
            {
                return NotFound();
            }


            return View(user);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model =
                new UserCreateViewModel
                {
                    AvailableRoles =
                        _userManagementService
                            .GetAssignableRoles()
                };


            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserCreateViewModel model)
        {
            /*
             * AvailableRoles is not posted by the browser,
             * so always rebuild it server-side.
             */
            model.AvailableRoles =
                _userManagementService
                    .GetAssignableRoles();


            if (!ModelState.IsValid)
            {
                return View(model);
            }


            var creation =
                await _userManagementService
                    .CreateUserAsync(model);


            if (!creation.Result.Succeeded ||
                creation.User == null)
            {
                foreach (var error
                    in creation.Result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }


                return View(model);
            }


            var createdUser =
                creation.User;


            /*
             * Never include the initial password,
             * password hash, reset tokens, or security
             * stamp in the audit entry.
             */
            await _auditTrailService.LogAsync(
                userId:
                    User.FindFirstValue(
                        ClaimTypes.NameIdentifier),

                userName:
                    User.Identity?.Name,

                actionType:
                    "CREATE_USER",

                module:
                    "Administration",

                tableName:
                    "AspNetUsers",

                recordId:
                    createdUser.Id,

                description:
                    $"Created user account '{createdUser.Email}'.",

                newValues:
                    new
                    {
                        createdUser.Email,
                        createdUser.UserName,
                        createdUser.EmailConfirmed,
                        Role = model.Role
                    },

                requestPath:
                    HttpContext.Request.Path,

                ipAddress:
                    HttpContext.Connection
                        .RemoteIpAddress?
                        .ToString(),

                isSuccessful:
                    true
            );


            TempData["SuccessMessage"] =
                $"User '{createdUser.Email}' was created successfully.";


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        createdUser.Id
                });
        }

        [HttpGet]
        public async Task<IActionResult> EditRole(string id)
        {
            var model =
                await _userManagementService
                    .GetRoleEditAsync(id);


            if (model == null)
            {
                return NotFound();
            }


            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(UserRoleEditViewModel model)
        {
            model.AvailableRoles =
                _userManagementService
                    .GetAssignableRoles();


            if (!ModelState.IsValid)
            {
                return View(model);
            }


            var currentUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);


            if (string.IsNullOrWhiteSpace(
                currentUserId))
            {
                return Challenge();
            }


            var result =
                await _userManagementService
                    .ChangeRoleAsync(
                        model.UserID,
                        model.SelectedRole,
                        currentUserId);


            if (!result.Result.Succeeded)
            {
                foreach (var error
                    in result.Result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }


                return View(model);
            }


            await _auditTrailService.LogAsync(
                userId:
                    currentUserId,

                userName:
                    User.Identity?.Name,

                actionType:
                    "CHANGE_USER_ROLE",

                module:
                    "Administration",

                tableName:
                    "AspNetUserRoles",

                recordId:
                    model.UserID,

                description:
                    $"Changed role for user '{model.Email}'.",

                oldValues:
                    new
                    {
                        Role = result.OldRole
                    },

                newValues:
                    new
                    {
                        Role = model.SelectedRole
                    },

                requestPath:
                    HttpContext.Request.Path,

                ipAddress:
                    HttpContext.Connection
                        .RemoteIpAddress?
                        .ToString(),

                isSuccessful:
                    true
            );


            TempData["SuccessMessage"] =
                "User role updated successfully.";


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        model.UserID
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable(string id)
        {
            var currentUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);


            if (string.IsNullOrWhiteSpace(
                currentUserId))
            {
                return Challenge();
            }


            var user =
                await _userManagementService
                    .GetUserDetailsAsync(id);


            if (user == null)
            {
                return NotFound();
            }


            var result =
                await _userManagementService
                    .DisableUserAsync(
                        id,
                        currentUserId);


            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] =
                    string.Join(
                        " ",
                        result.Errors
                            .Select(x => x.Description));


                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id
                    });
            }


            await _auditTrailService.LogAsync(
                userId:
                    currentUserId,

                userName:
                    User.Identity?.Name,

                actionType:
                    "DISABLE_USER",

                module:
                    "Administration",

                tableName:
                    "AspNetUsers",

                recordId:
                    id,

                description:
                    $"Disabled user account '{user.Email}'.",

                oldValues:
                    new
                    {
                        Status = user.Status
                    },

                newValues:
                    new
                    {
                        Status = "Disabled"
                    },

                requestPath:
                    HttpContext.Request.Path,

                ipAddress:
                    HttpContext.Connection
                        .RemoteIpAddress?
                        .ToString(),

                isSuccessful:
                    true
            );


            TempData["SuccessMessage"] =
                $"User '{user.Email}' was disabled.";


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enable(
    string id)
        {
            var currentUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);


            if (string.IsNullOrWhiteSpace(
                currentUserId))
            {
                return Challenge();
            }


            var user =
                await _userManagementService
                    .GetUserDetailsAsync(id);


            if (user == null)
            {
                return NotFound();
            }


            var result =
                await _userManagementService
                    .EnableUserAsync(id);


            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] =
                    string.Join(
                        " ",
                        result.Errors
                            .Select(x => x.Description));


                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id
                    });
            }


            await _auditTrailService.LogAsync(
                userId:
                    currentUserId,

                userName:
                    User.Identity?.Name,

                actionType:
                    "ENABLE_USER",

                module:
                    "Administration",

                tableName:
                    "AspNetUsers",

                recordId:
                    id,

                description:
                    $"Enabled user account '{user.Email}'.",

                oldValues:
                    new
                    {
                        Status = "Disabled"
                    },

                newValues:
                    new
                    {
                        Status = "Active"
                    },

                requestPath:
                    HttpContext.Request.Path,

                ipAddress:
                    HttpContext.Connection
                        .RemoteIpAddress?
                        .ToString(),

                isSuccessful:
                    true
            );


            TempData["SuccessMessage"] =
                $"User '{user.Email}' was enabled.";


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(
    string id)
        {
            var currentUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);


            if (string.IsNullOrWhiteSpace(
                currentUserId))
            {
                return Challenge();
            }


            var user =
                await _userManagementService
                    .GetUserDetailsAsync(id);


            if (user == null)
            {
                return NotFound();
            }


            var result =
                await _userManagementService
                    .UnlockUserAsync(id);


            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] =
                    string.Join(
                        " ",
                        result.Errors
                            .Select(x => x.Description));


                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id
                    });
            }


            await _auditTrailService.LogAsync(
                userId:
                    currentUserId,

                userName:
                    User.Identity?.Name,

                actionType:
                    "UNLOCK_USER",

                module:
                    "Administration",

                tableName:
                    "AspNetUsers",

                recordId:
                    id,

                description:
                    $"Unlocked user account '{user.Email}'.",

                oldValues:
                    new
                    {
                        Status = "Locked",
                        user.AccessFailedCount
                    },

                newValues:
                    new
                    {
                        Status = "Active",
                        AccessFailedCount = 0
                    },

                requestPath:
                    HttpContext.Request.Path,

                ipAddress:
                    HttpContext.Connection
                        .RemoteIpAddress?
                        .ToString(),

                isSuccessful:
                    true
            );


            TempData["SuccessMessage"] =
                $"User '{user.Email}' was unlocked.";


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id
                });
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(
    string id)
        {
            var model =
                await _userManagementService
                    .GetResetPasswordAsync(id);


            if (model == null)
            {
                return NotFound();
            }


            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword( ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }


            var currentUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);


            if (string.IsNullOrWhiteSpace(
                currentUserId))
            {
                return Challenge();
            }


            /*
             * Do not trust Email from the submitted form.
             * Reload the target user server-side.
             */
            var targetUser =
                await _userManagementService
                    .GetUserDetailsAsync(
                        model.UserID);


            if (targetUser == null)
            {
                return NotFound();
            }


            var result =
                await _userManagementService
                    .ResetPasswordAsync(
                        model.UserID,
                        model.NewPassword);


            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }


                /*
                 * Restore trusted display information.
                 */
                model.Email =
                    targetUser.Email;


                return View(model);
            }


            await _auditTrailService.LogAsync(
                userId:
                    currentUserId,

                userName:
                    User.Identity?.Name,

                actionType:
                    "RESET_PASSWORD",

                module:
                    "Administration",

                tableName:
                    "AspNetUsers",

                recordId:
                    model.UserID,

                description:
                    $"Administrator reset password for user '{targetUser.Email}'.",

                /*
                 * Deliberately no OldValues/NewValues.
                 *
                 * Passwords, hashes, reset tokens and
                 * security stamps must never be audited.
                 */
                oldValues:
                    null,

                newValues:
                    null,

                requestPath:
                    HttpContext.Request.Path,

                ipAddress:
                    HttpContext.Connection
                        .RemoteIpAddress?
                        .ToString(),

                isSuccessful:
                    true
            );


            TempData["SuccessMessage"] =
                $"Password for '{targetUser.Email}' was reset successfully.";


            return RedirectToAction(
                nameof(Details),
                new
                {
                    id =
                        model.UserID
                });
        }
    }
}