using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CIITStackLab.Infrastructure.Services;

public sealed class AdminSettingsService : IAdminSettingsService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminSettingsService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<AdminSettingsDto?> GetAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return user is null
            ? null
            : new AdminSettingsDto
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                TwoFactorEnabled = user.TwoFactorEnabled
            };
    }

    public async Task<(bool Succeeded, string? Error)> UpdateProfileAsync(
        string userId,
        AdminProfileSettingsInputDto input,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return (false, "Your account could not be found.");
        }

        var email = string.IsNullOrWhiteSpace(input.Email)
            ? null
            : input.Email.Trim();

        var phone = string.IsNullOrWhiteSpace(input.PhoneNumber)
            ? null
            : input.PhoneNumber.Trim();

        if (email is not null)
        {
            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser is not null && existingUser.Id != user.Id)
            {
                return (false, "That email address is already used by another account.");
            }
        }

        user.Email = email;
        user.PhoneNumber = phone;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return (false, "Your profile settings could not be saved.");
        }

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> ChangePasswordAsync(
        string userId,
        AdminChangePasswordInputDto input,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return (false, "Your account could not be found.");
        }

        var result = await _userManager.ChangePasswordAsync(
            user,
            input.CurrentPassword,
            input.NewPassword);

        if (!result.Succeeded)
        {
            var firstError = result.Errors.FirstOrDefault()?.Description;

            return (
                false,
                firstError ?? "Your password could not be changed. Please verify the current password.");
        }

        return (true, null);
    }
}
