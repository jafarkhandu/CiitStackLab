using CIITStackLab.Application.Interfaces;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public class TopicsController : Controller
{
    private readonly IAdminTopicService _topicService;
    public TopicsController(IAdminTopicService topicService) => _topicService = topicService;

    [HttpGet]
    public async Task<IActionResult> Index(int? editId = null, CancellationToken cancellationToken = default)
    {
        var activeTopics = await _topicService.GetActiveAsync(cancellationToken);
        var archivedTopics = await _topicService.GetArchivedAsync(cancellationToken);

        AdminTopicFormModel? editTopic = null;
        if (editId.HasValue)
        {
            var topic = activeTopics.FirstOrDefault(x => x.Id == editId.Value);
            if (topic is not null)
                editTopic = new AdminTopicFormModel
                {
                    Id = topic.Id, TopicName = topic.Title, PublicFolderId = topic.PublicFolderId,
                    Price = topic.Price, DurationMinutes = topic.DurationMinutes
                };
        }

        return View(new AdminTopicIndexViewModel { ActiveTopics = activeTopics, ArchivedTopics = archivedTopics, EditTopic = editTopic });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminTopicFormModel model, CancellationToken cancellationToken)
    {
        model.TopicName = model.TopicName?.Trim() ?? string.Empty;
        model.PublicFolderId = string.IsNullOrWhiteSpace(model.PublicFolderId) ? null : model.PublicFolderId.Trim();

        if (!ModelState.IsValid)
            return Fail("Please check the topic details.", model.Id);

        if (await _topicService.TopicNameExistsAsync(model.TopicName, model.Id == 0 ? null : model.Id, cancellationToken))
            return Fail("A topic with this name already exists.", model.Id);

        if (model.Id == 0)
        {
            var created = await _topicService.CreateAsync(model.TopicName, model.PublicFolderId, model.Price, model.DurationMinutes, cancellationToken);
            return created.Succeeded ? Success("Topic created successfully.") : Fail(created.Error ?? "Unable to create the topic.");
        }

        var updated = await _topicService.UpdateAsync(model.Id, model.TopicName, model.PublicFolderId, model.Price, model.DurationMinutes, cancellationToken);
        return updated.Succeeded ? Success("Topic updated successfully.") : Fail(updated.Error ?? "Unable to update the topic.", model.Id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => Result(await _topicService.DeleteAsync(id, cancellationToken), "Topic moved to archive.", "Unable to archive the topic.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
        => Result(await _topicService.RestoreAsync(id, cancellationToken), "Topic restored successfully.", "Unable to restore the topic.");

    private IActionResult Success(string message, int? editId = null)
    {
        TempData["AdminTopicMessage"] = message;
        TempData["AdminTopicMessageType"] = "success";
        return RedirectToAction(nameof(Index), editId.HasValue ? new { editId } : null)!;
    }

    private IActionResult Fail(string message, int? editId = null)
    {
        TempData["AdminTopicMessage"] = message;
        TempData["AdminTopicMessageType"] = "error";
        return RedirectToAction(nameof(Index), editId.HasValue ? new { editId } : null)!;
    }

    private IActionResult Result((bool Succeeded, string? Error) result, string success, string failure)
        => result.Succeeded ? Success(success) : Fail(result.Error ?? failure);
}
