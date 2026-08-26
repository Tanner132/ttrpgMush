using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SeattleByNight.Application.Authorization;
using SeattleByNight.Infrastructure.Identity;

namespace SeattleByNight.Infrastructure.Persistence.Seed;

/// <summary>
/// Grants the Administrator role to a configured account so a fresh deployment has
/// a first administrator. Migrations create the role definitions but deliberately
/// assign them to nobody, and role management is itself administrator-gated, so
/// without this there is no way into a new database except hand-written SQL.
/// </summary>
public static class AdministratorBootstrapper
{
    public static async Task PromoteAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email);

        // The account has to register through the app first. A missing account is
        // the normal state of a brand new deployment, and a typo in configuration
        // must not stop the API from starting, so neither case throws.
        if (user is null)
        {
            logger.LogWarning(
                "Bootstrap administrator {Email} is not registered yet. Register that account, then restart the API to grant it the {Role} role.",
                email,
                ApplicationRoles.Administrator);
            return;
        }

        if (await userManager.IsInRoleAsync(user, ApplicationRoles.Administrator))
        {
            return;
        }

        var result = await userManager.AddToRoleAsync(user, ApplicationRoles.Administrator);

        if (result.Succeeded)
        {
            logger.LogInformation(
                "Granted the {Role} role to bootstrap administrator {Email}.",
                ApplicationRoles.Administrator,
                email);
            return;
        }

        logger.LogError(
            "Could not grant the {Role} role to bootstrap administrator {Email}: {Errors}",
            ApplicationRoles.Administrator,
            email,
            string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
