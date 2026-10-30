using System.Security.Cryptography;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Identity;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Super User")]
public sealed class MeetController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public MeetController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var batches = await _dbContext.MeetBatches
            .AsNoTracking()
            .Include(x => x.Members)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var students = await GetActiveStudentsAsync(cancellationToken);
        var sessions = await _dbContext.MeetSessions
            .AsNoTracking()
            .Include(x => x.MeetBatch)
            .Where(x => !x.MeetBatch.IsArchived)
            .OrderBy(x => x.StartsAtUtc)
            .Select(x => new MeetSessionListItemViewModel
            {
                Id = x.Id,
                MeetBatchId = x.MeetBatchId,
                BatchName = x.MeetBatch.Name,
                Title = x.Title,
                Description = x.Description,
                StartsAtUtc = x.StartsAtUtc,
                DurationMinutes = x.DurationMinutes,
                Status = x.Status
            })
            .ToListAsync(cancellationToken);

        return View(new MeetBatchIndexViewModel
        {
            ActiveBatches = batches.Where(x => !x.IsArchived).Select(MapBatch).ToList(),
            ArchivedBatches = batches.Where(x => x.IsArchived).Select(MapBatch).ToList(),
            Students = students,
            Sessions = sessions
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSession(
        MeetSessionCreateViewModel model,
        string meetingMode,
        CancellationToken cancellationToken)
    {
        var title = model.Title?.Trim() ?? string.Empty;
        var description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

        if (title.Length == 0 || title.Length > 150 || description?.Length > 1000
            || model.DurationMinutes is < 5 or > 480)
        {
            TempData["MeetBatchMessage"] = "Check the meeting title, description and duration.";
            TempData["MeetBatchMessageType"] = "error";
            return RedirectToAction(nameof(Index));
        }

        var batch = await _dbContext.MeetBatches
            .Include(x => x.Members)
            .SingleOrDefaultAsync(x => x.Id == model.MeetBatchId && !x.IsArchived, cancellationToken);

        if (batch is null || batch.Members.Count == 0)
        {
            TempData["MeetBatchMessage"] = "Select an active batch that has assigned students.";
            TempData["MeetBatchMessageType"] = "error";
            return RedirectToAction(nameof(Index));
        }

        var creatorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(creatorId))
            return Forbid();

        var isInstant = string.Equals(meetingMode, "instant", StringComparison.OrdinalIgnoreCase);
        DateTime startsAtUtc;
        if (isInstant)
        {
            startsAtUtc = DateTime.UtcNow;
        }
        else
        {
            if (!model.StartsAtLocal.HasValue)
            {
                TempData["MeetBatchMessage"] = "Choose a date and time for the scheduled meeting.";
                TempData["MeetBatchMessageType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            var localStart = DateTime.SpecifyKind(model.StartsAtLocal.Value, DateTimeKind.Unspecified);
            startsAtUtc = DateTime.SpecifyKind(
                localStart.AddMinutes(model.ClientUtcOffsetMinutes),
                DateTimeKind.Utc);

            if (startsAtUtc <= DateTime.UtcNow.AddMinutes(1))
            {
                TempData["MeetBatchMessage"] = "Scheduled meeting time must be in the future.";
                TempData["MeetBatchMessageType"] = "error";
                return RedirectToAction(nameof(Index));
            }
        }

        _dbContext.MeetSessions.Add(new MeetSession
        {
            MeetBatchId = batch.Id,
            Title = title,
            Description = description,
            RoomName = $"ciit-{Guid.NewGuid():N}",
            StartsAtUtc = startsAtUtc,
            DurationMinutes = model.DurationMinutes,
            Status = isInstant ? "Live" : "Scheduled",
            CreatedByUserId = creatorId,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        TempData["MeetBatchMessage"] = isInstant
            ? "Instant meeting created. Live room is ready for the conferencing phase."
            : "Meeting scheduled successfully.";
        TempData["MeetBatchMessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string? name,
        string? description,
        List<string>? studentIds,
        CancellationToken cancellationToken)
    {
        name = name?.Trim();
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        studentIds = studentIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList() ?? [];

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return CreateResponse("Enter a batch name (maximum 100 characters).", false);

        if (description?.Length > 500)
            return CreateResponse("Description cannot exceed 500 characters.", false);

        var validStudentIds = await GetValidStudentIdsAsync(studentIds, cancellationToken);
        if (validStudentIds.Count != studentIds.Count)
            return CreateResponse("One or more selected accounts are not active Student accounts. Please refresh and try again.", false);

        var now = DateTime.UtcNow;
        var batch = new MeetBatch
        {
            Name = name,
            Description = description,
            BatchCode = await GenerateUniqueBatchCodeAsync(cancellationToken),
            CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        foreach (var studentId in validStudentIds)
        {
            _dbContext.MeetBatchMembers.Add(new MeetBatchMember
            {
                MeetBatch = batch,
                StudentUserId = studentId,
                AddedAtUtc = now
            });
        }

        _dbContext.MeetBatches.Add(batch);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreateResponse("Meet batch created successfully.", true);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var batch = await _dbContext.MeetBatches
            .AsNoTracking()
            .Include(x => x.Members)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (batch is null)
            return NotFound();

        var students = await GetActiveStudentsAsync(cancellationToken);

        var model = new MeetBatchDetailsViewModel
        {
            Id = batch.Id,
            Name = batch.Name,
            Description = batch.Description,
            BatchCode = batch.BatchCode,
            IsArchived = batch.IsArchived,
            CreatedAtUtc = batch.CreatedAtUtc,
            Students = students,
            SelectedStudentIds = batch.Members.Select(x => x.StudentUserId).ToList()
        };

        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return PartialView("_DetailsModal", model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        MeetBatchDetailsViewModel model,
        CancellationToken cancellationToken)
    {
        var batch = await _dbContext.MeetBatches
            .Include(x => x.Members)
            .SingleOrDefaultAsync(x => x.Id == model.Id, cancellationToken);

        if (batch is null)
            return NotFound();

        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Description = string.IsNullOrWhiteSpace(model.Description)
            ? null
            : model.Description.Trim();

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Name))
            return UpdateResponse("Please check the batch name and description.", false, batch.Id);

        var selectedIds = (model.SelectedStudentIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var validStudentIds = await GetValidStudentIdsAsync(selectedIds, cancellationToken);
        if (validStudentIds.Count != selectedIds.Count)
            return UpdateResponse("One or more selected accounts are not active Student accounts. Please refresh and try again.", false, batch.Id);

        batch.Name = model.Name;
        batch.Description = model.Description;
        batch.UpdatedAtUtc = DateTime.UtcNow;

        var selectedSet = validStudentIds.ToHashSet(StringComparer.Ordinal);
        var existingMembers = batch.Members.ToList();
        var existingSet = existingMembers
            .Select(member => member.StudentUserId)
            .ToHashSet(StringComparer.Ordinal);

        var removedMembers = existingMembers
            .Where(member => !selectedSet.Contains(member.StudentUserId))
            .ToList();

        _dbContext.MeetBatchMembers.RemoveRange(removedMembers);

        var now = DateTime.UtcNow;
        var newMembers = validStudentIds
            .Where(studentId => !existingSet.Contains(studentId))
            .Select(studentId => new MeetBatchMember
            {
                MeetBatchId = batch.Id,
                StudentUserId = studentId,
                AddedAtUtc = now
            })
            .ToList();

        if (newMembers.Count > 0)
            _dbContext.MeetBatchMembers.AddRange(newMembers);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return UpdateResponse("Batch details and members updated successfully.", true, batch.Id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id, CancellationToken cancellationToken)
    {
        var batch = await _dbContext.MeetBatches
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (batch is null)
            return NotFound();

        if (!batch.IsArchived)
        {
            batch.IsArchived = true;
            batch.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return RedirectWithMessage("Batch moved to archive.", true);
    }

    private async Task<IReadOnlyList<MeetStudentOptionViewModel>> GetActiveStudentsAsync(
        CancellationToken cancellationToken)
    {
        var studentRoleId = await _dbContext.Roles
            .AsNoTracking()
            .Where(x => x.NormalizedName == "STUDENT")
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(studentRoleId))
            return [];

        return await (
            from userRole in _dbContext.UserRoles.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking()
                on userRole.UserId equals user.Id
            where userRole.RoleId == studentRoleId
                && user.IsActive
                && !_dbContext.UserRoles.AsNoTracking().Any(adminRole =>
                    adminRole.UserId == user.Id
                    && _dbContext.Roles.AsNoTracking().Any(role =>
                        role.Id == adminRole.RoleId
                        && (role.NormalizedName == "ADMIN" || role.NormalizedName == "SUPER USER")))
            orderby user.UserName
            select new MeetStudentOptionViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<List<string>> GetValidStudentIdsAsync(
        IReadOnlyCollection<string> requestedIds,
        CancellationToken cancellationToken)
    {
        if (requestedIds.Count == 0)
            return [];

        var students = await GetActiveStudentsAsync(cancellationToken);
        var validIds = students.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        return requestedIds.Where(validIds.Contains).ToList();
    }

    private async Task<string> GenerateUniqueBatchCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = $"MEET-{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}";
            var exists = await _dbContext.MeetBatches
                .AnyAsync(x => x.BatchCode == code, cancellationToken);

            if (!exists)
                return code;
        }

        throw new InvalidOperationException("A unique Meet batch code could not be generated.");
    }

    private IActionResult CreateResponse(string message, bool succeeded)
    {
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return Json(new { success = succeeded, message });

        return RedirectWithMessage(message, succeeded);
    }

    private IActionResult UpdateResponse(string message, bool succeeded, int batchId)
    {
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return Json(new { success = succeeded, message });

        return RedirectWithMessage(message, succeeded, nameof(Details), batchId);
    }

    private async Task<IActionResult> ReturnDetailsWithErrorAsync(
        int id,
        string message,
        CancellationToken cancellationToken)
    {
        TempData["MeetBatchMessage"] = message;
        TempData["MeetBatchMessageType"] = "error";
        return RedirectToAction(nameof(Details), new { id })!;
    }

    private IActionResult RedirectWithMessage(
        string message,
        bool succeeded,
        string action = nameof(Index),
        int? id = null)
    {
        TempData["MeetBatchMessage"] = message;
        TempData["MeetBatchMessageType"] = succeeded ? "success" : "error";

        return id.HasValue
            ? RedirectToAction(action, new { id })
            : RedirectToAction(action)!;
    }

    private static MeetBatchListItemViewModel MapBatch(MeetBatch batch)
        => new()
        {
            Id = batch.Id,
            Name = batch.Name,
            Description = batch.Description,
            BatchCode = batch.BatchCode,
            MemberCount = batch.Members.Count,
            CreatedAtUtc = batch.CreatedAtUtc
        };
}
