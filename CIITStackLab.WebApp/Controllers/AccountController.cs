using CIITStackLab.Infrastructure.Identity;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace CIITStackLab.WebApp.Controllers;

public class AccountController : Controller
{
    private const string AdminRole = "Admin";
    private const string SuperUserRole = "Super User";
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
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

        // Public registration can never create Admin or Super User access.
        // The Student role is created lazily on first registration if the
        // current ERP database does not contain it yet.
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

        await _signInManager.SignInAsync(user, isPersistent: false);

        return RedirectToAction("Index", "Home");
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
}
