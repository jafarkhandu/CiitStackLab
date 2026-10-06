using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task VerifyAsync(ApplicationDbContext dbContext)
    {
        if (!await dbContext.Database.CanConnectAsync())
        {
            throw new InvalidOperationException(
                "Unable to connect to the configured CIIT Stack Lab database.");
        }
    }
}
