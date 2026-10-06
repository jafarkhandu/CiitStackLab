using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CIITStackLab.Infrastructure.Identity;

public static class IdentitySeeder
{
    // Student is required because public registration assigns this role.
    // Admin and Super User are optional runtime roles in the existing ERP database.
    // Their absence must never prevent the application from starting.
    private static readonly string[] RequiredRoles = ["Student"];

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
