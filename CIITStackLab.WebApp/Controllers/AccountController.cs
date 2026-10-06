using CIITStackLab.Infrastructure.Identity;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
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

        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        if (await _userManager.IsInRoleAsync(user, "Student"))
        {
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

        // Public registration can never create an Admin.
        // Every newly registered account is assigned the Student role only.
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
