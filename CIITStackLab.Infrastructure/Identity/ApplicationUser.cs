using Microsoft.AspNetCore.Identity;

namespace CIITStackLab.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}