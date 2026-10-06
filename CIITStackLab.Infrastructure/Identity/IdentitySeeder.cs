using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CIITStackLab.Infrastructure.Identity;

public static class IdentitySeeder
{
    private static readonly string[] RequiredRoles = ["Admin", "Student"];

    // Read-only verification. Existing ERP Identity data is reused.
    public static async Task VerifyAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in RequiredRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                throw new InvalidOperationException(
                    $"Required Identity role '{role}' was not found in the existing ERP database.");
            }
        }
    }
}
