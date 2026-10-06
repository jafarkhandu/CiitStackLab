using CIITStackLab.Application.Interfaces;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class TopicsController : Controller
{
    private readonly IAdminTopicService _topicService;

    public TopicsController(IAdminTopicService topicService)
    {
        _topicService = topicService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? editId = null,
        CancellationToken cancellationToken = default)
    {
        var activeTopics = await _topicService.GetActiveAsync(cancellationToken);
        var archivedTopics = await _topicService.GetArchivedAsync(cancellationToken);

        AdminTopicFormModel? editTopic = null;

        if (editId.HasValue)
        {
            var topic = activeTopics.FirstOrDefault(x => x.Id == editId.Value);
            if (topic is not null)
            {
                editTopic = new AdminTopicFormModel
                {
                    Id = topic.Id,
                    TopicName = topic.Title,
                    PublicFolderId = topic.PublicFolderId
                };
            }
        }

        return View(new AdminTopicIndexViewModel
        {
            ActiveTopics = activeTopics,
            ArchivedTopics = archivedTopics,
            EditTopic = editTopic
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        AdminTopicFormModel model,
        CancellationToken cancellationToken)
    {
        model.TopicName = model.TopicName?.Trim() ?? string.Empty;
        model.PublicFolderId = string.IsNullOrWhiteSpace(model.PublicFolderId)
            ? null
            : model.PublicFolderId.Trim();

        if (!ModelState.IsValid)
        {
            TempData["AdminTopicMessage"] = "Please check the topic details.";
            TempData["AdminTopicMessageType"] = "error";
            return RedirectToAction(nameof(Index), new
            {
                editId = model.Id == 0 ? (int?)null : model.Id
            });
        }

        if (await _topicService.TopicNameExistsAsync(
                model.TopicName,
                model.Id == 0 ? null : model.Id,
                cancellationToken))
        {
            TempData["AdminTopicMessage"] = "A topic with this name already exists.";
            TempData["AdminTopicMessageType"] = "error";

            return RedirectToAction(nameof(Index), new
            {
                editId = model.Id == 0 ? (int?)null : model.Id
            });
        }

        if (model.Id == 0)
        {
            var created = await _topicService.CreateAsync(
                model.TopicName,
                model.PublicFolderId,
                cancellationToken);

            return created.Succeeded
                ? RedirectToIndexWithMessage("Topic created successfully.", "success")
                : RedirectToIndexWithMessage(created.Error ?? "Unable to create the topic.", "error");
        }

        var updated = await _topicService.UpdateAsync(
            model.Id,
            model.TopicName,
            model.PublicFolderId,
            cancellationToken);

        return updated.Succeeded
            ? RedirectToIndexWithMessage("Topic updated successfully.", "success")
            : RedirectToIndexWithMessage(updated.Error ?? "Unable to update the topic.", "error", model.Id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _topicService.DeleteAsync(id, cancellationToken);

        return result.Succeeded
            ? RedirectToIndexWithMessage("Topic moved to archive.", "success")
            : RedirectToIndexWithMessage(result.Error ?? "Unable to archive the topic.", "error");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
    {
        var result = await _topicService.RestoreAsync(id, cancellationToken);

        return result.Succeeded
            ? RedirectToIndexWithMessage("Topic restored successfully.", "success")
            : RedirectToIndexWithMessage(result.Error ?? "Unable to restore the topic.", "error");
    }

    private IActionResult RedirectToIndexWithMessage(string message, string type, int? editId = null)
    {
        TempData["AdminTopicMessage"] = message;
        TempData["AdminTopicMessageType"] = type;

        return RedirectToAction(nameof(Index), editId.HasValue
            ? new { editId }
            : null)!;
    }
}
