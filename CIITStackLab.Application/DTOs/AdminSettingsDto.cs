using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.Application.DTOs;

public sealed class AdminSettingsDto
{
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public bool TwoFactorEnabled { get; init; }
}

public sealed class AdminProfileSettingsInputDto
{
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(256)]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(30)]
    public string? PhoneNumber { get; set; }
}

public sealed class AdminChangePasswordInputDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "New password must be between 8 and 100 characters.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword), ErrorMessage = "New password and confirmation do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
