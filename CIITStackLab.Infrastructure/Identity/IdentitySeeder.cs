using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CIITStackLab.Infrastructure.Identity;

public static class IdentitySeeder
{
    // The application must start even when the ERP database currently has
    // no application-specific Student/Admin roles. Existing roles are reused
    // when present; Student is created lazily only when the first public
    // registration requires it.
    public static Task VerifyAsync(IServiceProvider services)
    {
        return Task.CompletedTask;
    }
}
