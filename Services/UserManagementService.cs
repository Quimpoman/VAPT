using EVOLVE_VAPT.Security;
using EVOLVE_VAPT.ViewModels.Administration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EVOLVE_VAPT.Services
{
    public class UserManagementService
    {
        private readonly UserManager<IdentityUser>
            _userManager;


        public UserManagementService(
            UserManager<IdentityUser> userManager)
        {
            _userManager =
                userManager;
        }


        public async Task<
            IReadOnlyList<UserListItemViewModel>>
            GetUsersAsync()
        {
            var identityUsers =
                await _userManager
                    .Users
                    .OrderBy(x => x.Email)
                    .ToListAsync();


            var users =
                new List<UserListItemViewModel>();


            var now =
                DateTimeOffset.UtcNow;


            foreach (var user in identityUsers)
            {
                var roles =
                    await _userManager
                        .GetRolesAsync(user);


                /*
                 * DateTimeOffset.MaxValue is reserved by
                 * EVOLVE_VAPT to represent an account
                 * intentionally disabled by an administrator.
                 */
                var isAdministrativelyDisabled = IsAdministrativelyDisabled(user);

                var isTemporarilyLocked = IsTemporarilyLocked(user);


                string status;

                if (isAdministrativelyDisabled)
                {
                    status =
                        "Disabled";
                }
                else if (isTemporarilyLocked)
                {
                    status =
                        "Locked";
                }
                else
                {
                    status =
                        "Active";
                }


                users.Add(
                    new UserListItemViewModel
                    {
                        UserID =
                            user.Id,

                        Email =
                            user.Email
                            ?? string.Empty,

                        UserName =
                            user.UserName
                            ?? string.Empty,

                        EmailConfirmed =
                            user.EmailConfirmed,

                        IsAdministrativelyDisabled =
                            isAdministrativelyDisabled,

                        IsTemporarilyLocked =
                            isTemporarilyLocked,

                        Status =
                            status,

                        Roles =
                            roles
                                .OrderBy(x => x)
                                .ToList()
                    });
            }


            return users;
        }

        public async Task<UserDetailsViewModel?> GetUserDetailsAsync(
        string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }


            var user =
                await _userManager
                    .FindByIdAsync(userId);


            if (user == null)
            {
                return null;
            }


            var roles =
                await _userManager
                    .GetRolesAsync(user);


            var now =
                DateTimeOffset.UtcNow;


            var isAdministrativelyDisabled = IsAdministrativelyDisabled(user);

            var isTemporarilyLocked = IsTemporarilyLocked(user);


            string status;

            if (isAdministrativelyDisabled)
            {
                status =
                    "Disabled";
            }
            else if (isTemporarilyLocked)
            {
                status =
                    "Locked";
            }
            else
            {
                status =
                    "Active";
            }


            return new UserDetailsViewModel
            {
                UserID =
                    user.Id,

                Email =
                    user.Email
                    ?? string.Empty,

                UserName =
                    user.UserName
                    ?? string.Empty,

                EmailConfirmed =
                    user.EmailConfirmed,

                LockoutEnabled =
                    user.LockoutEnabled,

                LockoutEnd =
                    user.LockoutEnd,

                AccessFailedCount =
                    user.AccessFailedCount,

                IsAdministrativelyDisabled =
                    isAdministrativelyDisabled,

                IsTemporarilyLocked =
                    isTemporarilyLocked,

                Status =
                    status,

                Roles =
                    roles
                        .OrderBy(x => x)
                        .ToList()
            };
        }

        public IReadOnlyList<string> GetAssignableRoles()
        {
            return AppRoles.All;
        }


        public async Task<
            (IdentityResult Result, IdentityUser? User)>
            CreateUserAsync(
                UserCreateViewModel model)
        {
            var email =
                model.Email.Trim();


            /*
             * Never accept a role name directly just because
             * it came from the form.
             */
            if (!AppRoles.All.Contains(
                model.Role,
                StringComparer.Ordinal))
            {
                var result =
                    IdentityResult.Failed(
                        new IdentityError
                        {
                            Code =
                                "INVALID_ROLE",

                            Description =
                                "The selected role is not valid."
                        });


                return (result, null);
            }


            /*
             * Email is also used as the Identity username.
             * This prevents duplicate application accounts.
             */
            var existingUser =
                await _userManager
                    .FindByEmailAsync(email);


            if (existingUser != null)
            {
                var result =
                    IdentityResult.Failed(
                        new IdentityError
                        {
                            Code =
                                "DUPLICATE_USER",

                            Description =
                                "A user with this email address already exists."
                        });


                return (result, null);
            }


            var user =
                new IdentityUser
                {
                    UserName =
                        email,

                    Email =
                        email,

                    /*
                     * Accounts are provisioned directly by an
                     * Administrator in the current MVP.
                     *
                     * When email verification is implemented,
                     * this behavior can be changed.
                     */
                    EmailConfirmed =
                        true,

                    LockoutEnabled =
                        true
                };


            var createResult =
                await _userManager
                    .CreateAsync(
                        user,
                        model.InitialPassword);


            if (!createResult.Succeeded)
            {
                return (
                    createResult,
                    null);
            }


            var roleResult =
                await _userManager
                    .AddToRoleAsync(
                        user,
                        model.Role);


            if (!roleResult.Succeeded)
            {
                /*
                 * Do not leave behind a valid account with no
                 * intended authorization role.
                 */
                await _userManager
                    .DeleteAsync(user);


                return (
                    roleResult,
                    null);
            }


            return (
                IdentityResult.Success,
                user);
        }

        public async Task<UserRoleEditViewModel?> GetRoleEditAsync(
        string userId)
        {
            var user =
                await _userManager
                    .FindByIdAsync(userId);

            if (user == null)
            {
                return null;
            }


            var roles =
                await _userManager
                    .GetRolesAsync(user);


            return new UserRoleEditViewModel
            {
                UserID =
                    user.Id,

                Email =
                    user.Email
                    ?? string.Empty,

                SelectedRole =
                    roles.FirstOrDefault()
                    ?? string.Empty,

                AvailableRoles =
                    AppRoles.All
            };
        }

        public async Task<(IdentityResult Result, string? OldRole)>ChangeRoleAsync(
        string userId,
        string newRole,
        string currentUserId)
        {
            if (!AppRoles.All.Contains(
                newRole,
                StringComparer.Ordinal))
            {
                return (
                    IdentityResult.Failed(
                        new IdentityError
                        {
                            Code = "INVALID_ROLE",
                            Description =
                                "The selected role is not valid."
                        }),
                    null);
            }


            var user =
                await _userManager
                    .FindByIdAsync(userId);

            if (user == null)
            {
                return (
                    IdentityResult.Failed(
                        new IdentityError
                        {
                            Code = "USER_NOT_FOUND",
                            Description =
                                "The user could not be found."
                        }),
                    null);
            }


            var existingRoles =
                await _userManager
                    .GetRolesAsync(user);


            var oldRole =
                existingRoles.FirstOrDefault();


            if (oldRole == newRole)
            {
                return (
                    IdentityResult.Success,
                    oldRole);
            }


            /*
             * Prevent the currently logged-in Administrator
             * from removing their own Administrator role.
             */
            if (user.Id == currentUserId &&
                oldRole == AppRoles.Administrator &&
                newRole != AppRoles.Administrator)
            {
                return (
                    IdentityResult.Failed(
                        new IdentityError
                        {
                            Code = "SELF_DEMOTION_NOT_ALLOWED",
                            Description =
                                "You cannot remove your own Administrator role."
                        }),
                    oldRole);
            }


            /*
             * Protect the last Administrator account.
             */
            if (oldRole == AppRoles.Administrator &&
                newRole != AppRoles.Administrator)
            {
                var administrators =
                    await _userManager
                        .GetUsersInRoleAsync(
                            AppRoles.Administrator);


                var activeAdministratorCount =
                    administrators.Count(
                        x =>
                            !x.LockoutEnd.HasValue ||
                            x.LockoutEnd.Value <=
                            DateTimeOffset.UtcNow);


                if (activeAdministratorCount <= 1)
                {
                    return (
                        IdentityResult.Failed(
                            new IdentityError
                            {
                                Code =
                                    "LAST_ADMINISTRATOR",

                                Description =
                                    "The last active Administrator cannot be demoted."
                            }),
                        oldRole);
                }
            }


            /*
             * For the MVP, each user has exactly
             * one application role.
             */
            if (existingRoles.Any())
            {
                var removeResult =
                    await _userManager
                        .RemoveFromRolesAsync(
                            user,
                            existingRoles);


                if (!removeResult.Succeeded)
                {
                    return (
                        removeResult,
                        oldRole);
                }
            }


            var addResult =
                await _userManager
                    .AddToRoleAsync(
                        user,
                        newRole);


            if (!addResult.Succeeded)
            {
                /*
                 * Best-effort rollback if the new role
                 * cannot be assigned.
                 */
                if (!string.IsNullOrWhiteSpace(oldRole))
                {
                    await _userManager
                        .AddToRoleAsync(
                            user,
                            oldRole);
                }


                return (
                    addResult,
                    oldRole);
            }


            return (
                IdentityResult.Success,
                oldRole);
        }

        private static bool IsAdministrativelyDisabled(IdentityUser user)
        {
            return
                user.LockoutEnd.HasValue &&
                user.LockoutEnd.Value.Year == 9999;
        }


        private static bool IsTemporarilyLocked(
            IdentityUser user)
        {
            return
                !IsAdministrativelyDisabled(user) &&
                user.LockoutEnd.HasValue &&
                user.LockoutEnd.Value >
                    DateTimeOffset.UtcNow;
        }

        public async Task<IdentityResult> DisableUserAsync(
        string userId,
        string currentUserId)
        {
            var user =
                await _userManager
                    .FindByIdAsync(userId);


            if (user == null)
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "USER_NOT_FOUND",
                        Description =
                            "The user could not be found."
                    });
            }


            /*
             * Never allow an Administrator to disable
             * their own currently authenticated account.
             */
            if (user.Id == currentUserId)
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "SELF_DISABLE_NOT_ALLOWED",
                        Description =
                            "You cannot disable your own account."
                    });
            }


            if (IsAdministrativelyDisabled(user))
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "ALREADY_DISABLED",
                        Description =
                            "The user is already disabled."
                    });
            }


            var isAdministrator =
                await _userManager
                    .IsInRoleAsync(
                        user,
                        AppRoles.Administrator);


            if (isAdministrator)
            {
                var administrators =
                    await _userManager
                        .GetUsersInRoleAsync(
                            AppRoles.Administrator);


                /*
                 * Temporarily locked administrators still count
                 * because their lockout may expire or be cleared.
                 *
                 * Permanently disabled administrators do not.
                 */
                var enabledAdministratorCount =
                    administrators.Count(
                        x =>
                            !IsAdministrativelyDisabled(x));


                if (enabledAdministratorCount <= 1)
                {
                    return IdentityResult.Failed(
                        new IdentityError
                        {
                            Code = "LAST_ADMINISTRATOR",
                            Description =
                                "The last enabled Administrator cannot be disabled."
                        });
                }
            }


            var lockoutEnabledResult =
                await _userManager
                    .SetLockoutEnabledAsync(
                        user,
                        true);


            if (!lockoutEnabledResult.Succeeded)
            {
                return lockoutEnabledResult;
            }


            return await _userManager
                .SetLockoutEndDateAsync(
                    user,
                    DateTimeOffset.MaxValue);
        }

        public async Task<IdentityResult> EnableUserAsync(
        string userId)
        {
            var user =
                await _userManager
                    .FindByIdAsync(userId);


            if (user == null)
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "USER_NOT_FOUND",
                        Description =
                            "The user could not be found."
                    });
            }


            if (!IsAdministrativelyDisabled(user))
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "NOT_DISABLED",
                        Description =
                            "The user is not administratively disabled."
                    });
            }


            var result =
                await _userManager
                    .SetLockoutEndDateAsync(
                        user,
                        null);


            if (!result.Succeeded)
            {
                return result;
            }


            return await _userManager
                .ResetAccessFailedCountAsync(
                    user);
        }

        public async Task<IdentityResult> UnlockUserAsync(
        string userId)
        {
            var user =
                await _userManager
                    .FindByIdAsync(userId);


            if (user == null)
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "USER_NOT_FOUND",
                        Description =
                            "The user could not be found."
                    });
            }


            if (IsAdministrativelyDisabled(user))
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "USER_DISABLED",
                        Description =
                            "A disabled account must be enabled rather than unlocked."
                    });
            }


            if (!IsTemporarilyLocked(user))
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "NOT_LOCKED",
                        Description =
                            "The user account is not currently locked."
                    });
            }


            var result =
                await _userManager
                    .SetLockoutEndDateAsync(
                        user,
                        null);


            if (!result.Succeeded)
            {
                return result;
            }


            return await _userManager
                .ResetAccessFailedCountAsync(
                    user);
        }

        public async Task<ResetPasswordViewModel?> GetResetPasswordAsync(
        string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }


            var user =
                await _userManager
                    .FindByIdAsync(userId);


            if (user == null)
            {
                return null;
            }


            return new ResetPasswordViewModel
            {
                UserID =
                    user.Id,

                Email =
                    user.Email
                    ?? string.Empty
            };
        }

        public async Task<IdentityResult> ResetPasswordAsync(
        string userId,
        string newPassword)
        {
            var user =
                await _userManager
                    .FindByIdAsync(userId);


            if (user == null)
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "USER_NOT_FOUND",
                        Description =
                            "The user could not be found."
                    });
            }


            /*
             * Use Identity's reset-token workflow.
             *
             * Never manipulate PasswordHash directly.
             */
            var token =
                await _userManager
                    .GeneratePasswordResetTokenAsync(
                        user);


            var result =
                await _userManager
                    .ResetPasswordAsync(
                        user,
                        token,
                        newPassword);


            return result;
        }
    }
}