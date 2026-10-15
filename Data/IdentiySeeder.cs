using EVOLVE_VAPT.Security;
using Microsoft.AspNetCore.Identity;

namespace EVOLVE_VAPT.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            var userManager =
                serviceProvider
                    .GetRequiredService<
                        UserManager<IdentityUser>>();

            var roleManager =
                serviceProvider
                    .GetRequiredService<
                        RoleManager<IdentityRole>>();


            /*
             * These roles are application constants.
             * They are not user-editable configuration.
             */
            var roles =
                new[]
                {
                    AppRoles.Administrator,
                    AppRoles.SecurityAnalyst,
                    AppRoles.Reviewer,
                    AppRoles.ReadOnlyClient
                };


            foreach (var roleName in roles)
            {
                if (!await roleManager
                    .RoleExistsAsync(roleName))
                {
                    var roleResult =
                        await roleManager.CreateAsync(
                            new IdentityRole(roleName));

                    if (!roleResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Unable to create Identity role '{roleName}': "
                            + string.Join(
                                "; ",
                                roleResult.Errors
                                    .Select(x => x.Description)));
                    }
                }
            }


            /*
             * Bootstrap credentials should come from
             * configuration / User Secrets / environment
             * variables, never source code.
             */
            var adminEmail =
                configuration[
                    "BootstrapAdmin:Email"];

            var adminPassword =
                configuration[
                    "BootstrapAdmin:Password"];


            if (string.IsNullOrWhiteSpace(
                    adminEmail) ||
                string.IsNullOrWhiteSpace(
                    adminPassword))
            {
                /*
                 * Roles may still be seeded even when
                 * bootstrap admin creation is disabled.
                 */
                return;
            }


            var admin =
                await userManager
                    .FindByEmailAsync(
                        adminEmail);


            if (admin == null)
            {
                admin =
                    new IdentityUser
                    {
                        UserName =
                            adminEmail,

                        Email =
                            adminEmail,

                        EmailConfirmed =
                            true
                    };


                var createResult =
                    await userManager.CreateAsync(
                        admin,
                        adminPassword);


                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Unable to create bootstrap administrator: "
                        + string.Join(
                            "; ",
                            createResult.Errors
                                .Select(x => x.Description)));
                }
            }


            if (!await userManager
                .IsInRoleAsync(
                    admin,
                    AppRoles.Administrator))
            {
                var roleResult =
                    await userManager.AddToRoleAsync(
                        admin,
                        AppRoles.Administrator);


                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Unable to assign Administrator role: "
                        + string.Join(
                            "; ",
                            roleResult.Errors
                                .Select(x => x.Description)));
                }
            }
        }
    }
}