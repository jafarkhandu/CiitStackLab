using CIITStackLab.Infrastructure.Services;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public sealed class EmailSenderController : Controller
{
    private readonly AdminEmailCampaignService _campaignService;

    public EmailSenderController(AdminEmailCampaignService campaignService)
    {
        _campaignService = campaignService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new AdminEmailSenderViewModel();
        await PopulateAudienceCountsAsync(model, cancellationToken);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(
        AdminEmailSenderViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.Audience is not ("all" or "no-purchase" or "one-purchase-inactive"))
        {
            ModelState.AddModelError(nameof(model.Audience), "Select a valid recipient group.");
        }

        if (!model.ConfirmRecipientEligibility)
        {
            ModelState.AddModelError(
                nameof(model.ConfirmRecipientEligibility),
                "Confirm that the selected recipients have permission to receive this communication.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAudienceCountsAsync(model, cancellationToken);
            return View("Index", model);
        }

        var result = await _campaignService.SendAsync(
            model.Audience,
            model.Subject,
            model.Message,
            model.InactivityDays,
            cancellationToken);

        TempData["AdminEmailSenderMessageType"] =
            result.Failed == 0 ? "success" : "warning";
        TempData["AdminEmailSenderMessage"] =
            $"Campaign finished. {result.Sent} email(s) sent, {result.Failed} failed, from {result.Targeted} eligible recipient(s).";

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAudienceCountsAsync(
        AdminEmailSenderViewModel model,
        CancellationToken cancellationToken)
    {
        var counts = await _campaignService.GetAudienceCountsAsync(
            model.InactivityDays,
            cancellationToken);

        model.AllStudentsCount = counts.AllStudents;
        model.NoPurchaseCount = counts.NoPurchase;
        model.OnePurchaseInactiveCount = counts.OnePurchaseInactive;
    }
}
