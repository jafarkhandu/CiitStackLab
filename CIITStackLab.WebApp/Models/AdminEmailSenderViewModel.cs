using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.WebApp.Models;

public sealed class AdminEmailSenderViewModel
{
    [Required]
    public string Audience { get; set; } = "all";

    [Required]
    [StringLength(160, MinimumLength = 3)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(10000, MinimumLength = 10)]
    public string Message { get; set; } = string.Empty;

    [Range(1, 90)]
    public int InactivityDays { get; set; } = 14;

    public bool ConfirmRecipientEligibility { get; set; }

    public int AllStudentsCount { get; set; }

    public int NoPurchaseCount { get; set; }

    public int OnePurchaseInactiveCount { get; set; }
}
