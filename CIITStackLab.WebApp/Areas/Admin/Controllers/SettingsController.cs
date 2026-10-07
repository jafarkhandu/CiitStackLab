using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public sealed class SettingsController : Controller
{
    private readonly IAdminSettingsService _settingsService;

    public SettingsController(IAdminSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var settings = await _settingsService.GetAsync(userId, cancellationToken);

        return settings is null
            ? NotFound()
            : View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(
        AdminProfileSettingsInputDto input,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            await LoadSettingsIntoViewDataAsync(userId, cancellationToken);
            return View("Index", new AdminSettingsDto
            {
                UserId = userId,
                UserName = User.Identity?.Name ?? string.Empty,
                Email = input.Email,
                PhoneNumber = input.PhoneNumber
            });
        }

        var result = await _settingsService.UpdateProfileAsync(
            userId,
            input,
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["AdminSettingsMessage"] = result.Error;
            TempData["AdminSettingsMessageType"] = "error";
            return RedirectToAction(nameof(Index));
        }

        TempData["AdminSettingsMessage"] = "Profile settings saved successfully.";
        TempData["AdminSettingsMessageType"] = "success";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        AdminChangePasswordInputDto input,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["AdminSettingsMessage"] = "Please correct the password form and try again.";
            TempData["AdminSettingsMessageType"] = "error";
            return RedirectToAction(nameof(Index), new { section = "security" });
        }

        var result = await _settingsService.ChangePasswordAsync(
            userId,
            input,
            cancellationToken);

        TempData["AdminSettingsMessage"] = result.Succeeded
            ? "Password changed successfully."
            : result.Error ?? "Password change failed.";

        TempData["AdminSettingsMessageType"] =
            result.Succeeded ? "success" : "error";

        return RedirectToAction(nameof(Index), new { section = "security" });
    }

    private async Task LoadSettingsIntoViewDataAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetAsync(userId, cancellationToken);
        ViewData["Settings"] = settings;
    }
}
