using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace CIITStackLab.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    // Existing AspNetUsers has this custom column already.
    public bool IsActive { get; set; } = true;

    // The existing AspNetUsers table has no FullName column.
    // Keep it available to the application without changing the database schema.
    [NotMapped]
    public string FullName { get; set; } = string.Empty;
}
