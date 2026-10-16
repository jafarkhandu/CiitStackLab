using System.Security.Cryptography;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Identity;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

[AllowAnonymous]
public sealed class PasswordResetController : Controller
{
    private const string PasswordResetOtpKey = "PasswordResetOtp";
    private const string PasswordResetEmailKey = "PasswordResetEmail";
    private const string PasswordResetExpiryKey = "PasswordResetExpiry";
    private const string PasswordResetAttemptsKey = "PasswordResetAttempts";
    private const string PasswordResetVerifiedKey = "PasswordResetVerified";
    private const string PasswordResetResendAvailableKey = "PasswordResetResendAvailable";
    private const int ResendCooldownSeconds = 60;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ILogger<PasswordResetController> _logger;

    public PasswordResetController(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        ILogger<PasswordResetController> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestOtp(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Account/ForgotPassword.cshtml", model);
        }

        var email = model.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);

        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "No active account was found with this email address.");
            return View("~/Views/Account/ForgotPassword.cshtml", model);
        }

        return await IssueOtpAsync(user.Email, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(CancellationToken cancellationToken)
    {
        var email = HttpContext.Session.GetString(PasswordResetEmailKey);
        var storedOtp = HttpContext.Session.GetString(PasswordResetOtpKey);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(storedOtp))
        {
            return RedirectToAction("ForgotPassword", "Account");
        }

        var resendAvailableValue = HttpContext.Session.GetString(PasswordResetResendAvailableKey);
        if (DateTime.TryParse(
                resendAvailableValue,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var resendAvailableAt)
            && DateTime.UtcNow < resendAvailableAt)
        {
            var seconds = Math.Max(
                1,
                (int)Math.Ceiling((resendAvailableAt - DateTime.UtcNow).TotalSeconds));

            TempData["PasswordResetMessage"] =
                $"Please wait {seconds} seconds before requesting another OTP.";

            return RedirectToAction("VerifyPasswordResetOtp", "Account");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
        {
            ClearPasswordResetSession();
            return RedirectToAction("ForgotPassword", "Account");
        }

        return await IssueOtpAsync(user.Email, cancellationToken, isResend: true);
    }

    private async Task<IActionResult> IssueOtpAsync(
        string email,
        CancellationToken cancellationToken,
        bool isResend = false)
    {
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        try
        {
            await _emailService.SendPasswordResetOtpAsync(
                email,
                otp,
                expiresAt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Password reset OTP could not be sent to {Email}.",
                email);

            if (isResend)
            {
                TempData["PasswordResetMessage"] =
                    "We could not send a new OTP right now. Please try again.";
                return RedirectToAction("VerifyPasswordResetOtp", "Account");
            }

            ModelState.AddModelError(
                string.Empty,
                "We could not send the password reset OTP right now. Please try again.");

            return View(
                "~/Views/Account/ForgotPassword.cshtml",
                new ForgotPasswordViewModel { Email = email });
        }

        HttpContext.Session.SetString(PasswordResetOtpKey, otp);
        HttpContext.Session.SetString(PasswordResetEmailKey, email);
        HttpContext.Session.SetString(PasswordResetExpiryKey, expiresAt.ToString("O"));
        HttpContext.Session.SetInt32(PasswordResetAttemptsKey, 0);
        HttpContext.Session.Remove(PasswordResetVerifiedKey);
        HttpContext.Session.SetString(
            PasswordResetResendAvailableKey,
            DateTime.UtcNow.AddSeconds(ResendCooldownSeconds).ToString("O"));

        TempData["PasswordResetMessage"] = isResend
            ? "A new OTP has been sent to your registered email address."
            : "A password reset OTP has been sent to your registered email address.";

        return RedirectToAction("VerifyPasswordResetOtp", "Account");
    }

    private void ClearPasswordResetSession()
    {
        HttpContext.Session.Remove(PasswordResetOtpKey);
        HttpContext.Session.Remove(PasswordResetEmailKey);
        HttpContext.Session.Remove(PasswordResetExpiryKey);
        HttpContext.Session.Remove(PasswordResetAttemptsKey);
        HttpContext.Session.Remove(PasswordResetVerifiedKey);
        HttpContext.Session.Remove(PasswordResetResendAvailableKey);
    }
}
