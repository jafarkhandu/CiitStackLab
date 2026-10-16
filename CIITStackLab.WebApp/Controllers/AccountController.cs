using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Identity;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public class AccountController : Controller
{
    private const string AdminRole = "Admin";
    private const string SuperUserRole = "Super User";
    private const string PasswordResetOtpKey = "PasswordResetOtp";
    private const string PasswordResetEmailKey = "PasswordResetEmail";
    private const string PasswordResetExpiryKey = "PasswordResetExpiry";
    private const string PasswordResetAttemptsKey = "PasswordResetAttempts";
    private const string PasswordResetVerifiedKey = "PasswordResetVerified";
    private const int MaxPasswordResetOtpAttempts = 5;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailService _emailService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IEmailService emailService,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userName = model.UserName.Trim();
        var user = await _userManager.FindByNameAsync(userName);

        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            model.Password,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);

        if (await _userManager.IsInRoleAsync(user, AdminRole) ||
            await _userManager.IsInRoleAsync(user, SuperUserRole))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        if (await _userManager.IsInRoleAsync(user, "Student"))
        {
            if (!string.IsNullOrWhiteSpace(model.ReturnUrl)
                && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        await _signInManager.SignOutAsync();
        ModelState.AddModelError(
            string.Empty,
            "Your account does not have an assigned application role.");

        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userName = model.UserName.Trim();
        var email = model.Email.Trim();

        if (await _userManager.FindByNameAsync(userName) is not null)
        {
            ModelState.AddModelError(nameof(model.UserName), "This username is already in use.");
            return View(model);
        }

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            IsActive = true,
            FullName = model.FullName.Trim()
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        if (!await _roleManager.RoleExistsAsync("Student"))
        {
            var createRoleResult = await _roleManager.CreateAsync(
                new IdentityRole("Student"));

            if (!createRoleResult.Succeeded && !await _roleManager.RoleExistsAsync("Student"))
            {
                await _userManager.DeleteAsync(user);

                foreach (var error in createRoleResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "Student");

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);

            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        try
        {
            await _emailService.SendRegistrationEmailAsync(
                email,
                userName,
                HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Registration email could not be sent to {Email}.",
                email);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);

        if (user is not null && user.IsActive && !string.IsNullOrWhiteSpace(user.Email))
        {
            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var expiresAt = DateTime.UtcNow.AddMinutes(10);

            HttpContext.Session.SetString(PasswordResetOtpKey, otp);
            HttpContext.Session.SetString(PasswordResetEmailKey, email);
            HttpContext.Session.SetString(
                PasswordResetExpiryKey,
                expiresAt.ToString("O"));
            HttpContext.Session.SetInt32(PasswordResetAttemptsKey, 0);
            HttpContext.Session.Remove(PasswordResetVerifiedKey);

            try
            {
                await _emailService.SendPasswordResetOtpAsync(
                    email,
                    otp,
                    expiresAt,
                    HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                ClearPasswordResetSession();
                _logger.LogWarning(
                    ex,
                    "Password reset OTP could not be sent to {Email}.",
                    email);
            }
        }

        TempData["PasswordResetMessage"] =
            "If an active account exists for that email, a password reset OTP has been sent.";

        return RedirectToAction(nameof(VerifyPasswordResetOtp));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult VerifyPasswordResetOtp()
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString(PasswordResetOtpKey)))
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        ViewData["PasswordResetMessage"] = TempData["PasswordResetMessage"];
        return View(new VerifyPasswordResetOtpViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyPasswordResetOtp(VerifyPasswordResetOtpViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var storedOtp = HttpContext.Session.GetString(PasswordResetOtpKey);
        var expiresValue = HttpContext.Session.GetString(PasswordResetExpiryKey);
        var attempts = HttpContext.Session.GetInt32(PasswordResetAttemptsKey) ?? 0;

        if (string.IsNullOrWhiteSpace(storedOtp) || string.IsNullOrWhiteSpace(expiresValue))
        {
            ModelState.AddModelError(string.Empty, "Your OTP session has expired. Please request a new OTP.");
            return View(model);
        }

        if (!DateTime.TryParse(
                expiresValue,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var expiresAt)
            || DateTime.UtcNow > expiresAt)
        {
            ClearPasswordResetSession();
            ModelState.AddModelError(string.Empty, "Your OTP has expired. Please request a new OTP.");
            return View(model);
        }

        if (attempts >= MaxPasswordResetOtpAttempts)
        {
            ClearPasswordResetSession();
            ModelState.AddModelError(string.Empty, "Too many incorrect OTP attempts. Please request a new OTP.");
            return View(model);
        }

        var providedOtp = model.Otp.Trim();
        var matches = providedOtp.Length == storedOtp.Length
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedOtp),
                Encoding.UTF8.GetBytes(storedOtp));

        if (!matches)
        {
            attempts++;
            HttpContext.Session.SetInt32(PasswordResetAttemptsKey, attempts);
            ModelState.AddModelError(
                string.Empty,
                attempts >= MaxPasswordResetOtpAttempts
                    ? "Too many incorrect OTP attempts. Please request a new OTP."
                    : "The OTP is incorrect.");

            if (attempts >= MaxPasswordResetOtpAttempts)
            {
                ClearPasswordResetSession();
            }

            return View(model);
        }

        HttpContext.Session.SetString(PasswordResetVerifiedKey, "true");
        HttpContext.Session.Remove(PasswordResetOtpKey);
        HttpContext.Session.Remove(PasswordResetAttemptsKey);

        return RedirectToAction(nameof(ResetPassword));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword()
    {
        if (HttpContext.Session.GetString(PasswordResetVerifiedKey) != "true")
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (HttpContext.Session.GetString(PasswordResetVerifiedKey) != "true")
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = HttpContext.Session.GetString(PasswordResetEmailKey);
        if (string.IsNullOrWhiteSpace(email))
        {
            ClearPasswordResetSession();
            return RedirectToAction(nameof(ForgotPassword));
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            ClearPasswordResetSession();
            return RedirectToAction(nameof(ForgotPassword));
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(
            user,
            resetToken,
            model.Password);

        if (!resetResult.Succeeded)
        {
            foreach (var error in resetResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        ClearPasswordResetSession();
        TempData["PasswordResetSuccess"] =
            "Your password has been reset successfully. You can now sign in.";

        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        return View(new StudentProfileViewModel
        {
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber
        });
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(StudentProfileViewModel model)
    {
        ModelState.Remove(nameof(model.CurrentPassword));
        ModelState.Remove(nameof(model.NewPassword));
        ModelState.Remove(nameof(model.ConfirmNewPassword));

        if (!ModelState.IsValid)
        {
            return View("Profile", model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var normalizedUserName = model.UserName.Trim();
        var normalizedEmail = model.Email.Trim();

        var existingUserName = await _userManager.FindByNameAsync(normalizedUserName);
        if (existingUserName is not null && existingUserName.Id != user.Id)
        {
            ModelState.AddModelError(nameof(model.UserName), "This username is already in use.");
            return View("Profile", model);
        }

        var existingEmail = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingEmail is not null && existingEmail.Id != user.Id)
        {
            ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
            return View("Profile", model);
        }

        user.UserName = normalizedUserName;
        user.Email = normalizedEmail;
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber)
            ? null
            : model.PhoneNumber.Trim();

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("Profile", model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["ProfileSuccess"] = "Your profile details have been updated.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(StudentProfileViewModel model)
    {
        ModelState.Remove(nameof(model.UserName));
        ModelState.Remove(nameof(model.Email));
        ModelState.Remove(nameof(model.PhoneNumber));

        if (string.IsNullOrWhiteSpace(model.CurrentPassword))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Enter your current password.");
        }

        if (!ModelState.IsValid)
        {
            return View("Profile", await PopulateProfileFieldsAsync(model));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await _userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("Profile", await PopulateProfileFieldsAsync(model));
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["PasswordSuccess"] = "Your password has been changed successfully.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task<StudentProfileViewModel> PopulateProfileFieldsAsync(StudentProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is not null)
        {
            model.UserName = user.UserName ?? string.Empty;
            model.Email = user.Email ?? string.Empty;
            model.PhoneNumber = user.PhoneNumber;
        }

        return model;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private void ClearPasswordResetSession()
    {
        HttpContext.Session.Remove(PasswordResetOtpKey);
        HttpContext.Session.Remove(PasswordResetEmailKey);
        HttpContext.Session.Remove(PasswordResetExpiryKey);
        HttpContext.Session.Remove(PasswordResetAttemptsKey);
        HttpContext.Session.Remove(PasswordResetVerifiedKey);
    }
}
