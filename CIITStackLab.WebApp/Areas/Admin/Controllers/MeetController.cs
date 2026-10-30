using System.Security.Cryptography;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Identity;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.WebApp.Models;
using CIITStackLab.WebApp.Services;
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
    private readonly MeetRealtimeService _meetRealtime;

    public MeetController(ApplicationDbContext dbContext, MeetRealtimeService meetRealtime)
    {
        _dbContext = dbContext;
        _meetRealtime = meetRealtime;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        await CompleteExpiredSessionsAsync(cancellationToken);

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
                Status = x.Status,
                StartedAtUtc = x.StartedAtUtc,
                EndedAtUtc = x.EndedAtUtc,
                CancelledAtUtc = x.CancelledAtUtc
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

    [HttpGet]
    public async Task<IActionResult> Room(int id, CancellationToken cancellationToken)
    {
        await CompleteExpiredSessionsAsync(cancellationToken);

        var session = await _dbContext.MeetSessions
            .AsNoTracking()
            .Include(item => item.MeetBatch)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (session is null || session.MeetBatch.IsArchived)
            return NotFound();

        var participants = await (
            from participant in _dbContext.MeetSessionParticipants.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking()
                on participant.StudentUserId equals user.Id
            where participant.MeetSessionId == id
            orderby participant.RequestedAtUtc
            select new MeetRoomParticipantViewModel
            {
                Id = participant.Id,
                StudentName = user.UserName ?? user.Email ?? "Student",
                Status = participant.Status,
                CanUseMicrophone = participant.CanUseMicrophone,
                CanUseCamera = participant.CanUseCamera,
                CanShareScreen = participant.CanShareScreen
            }).ToListAsync(cancellationToken);

        return View(new MeetRoomViewModel
        {
            Id = session.Id,
            Title = session.Title,
            Description = session.Description,
            BatchName = session.MeetBatch.Name,
            BatchCode = session.MeetBatch.BatchCode,
            StartsAtUtc = session.StartsAtUtc,
            DurationMinutes = session.DurationMinutes,
            Status = session.Status,
            IsHost = true,
            WaitingRoomEnabled = session.WaitingRoomEnabled,
            RecordingEnabled = session.RecordingEnabled,
            RecordingStatus = session.RecordingStatus,
            RecordingRetentionUntilUtc = session.RecordingRetentionUntilUtc,
            ParticipantCount = participants.Count(x => x.Status == "Admitted"),
            Participants = participants
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id, CancellationToken cancellationToken)
    {
        var session = await _dbContext.MeetSessions
            .Include(x => x.MeetBatch)
            .ThenInclude(x => x.Members)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (session is null || session.MeetBatch.IsArchived)
            return NotFound();

        if (session.MeetBatch.Members.Count == 0)
            return RedirectWithMessage("Assign at least one active student before starting this meeting.", false);

        if (session.Status != "Scheduled")
            return RedirectWithMessage("Only scheduled meetings can be started.", false);

        var now = DateTime.UtcNow;
        var anotherLiveMeeting = await _dbContext.MeetSessions.AnyAsync(
            x => x.MeetBatchId == session.MeetBatchId
                && x.Id != session.Id
                && x.Status == "Live"
                && x.StartedAtUtc.HasValue
                && x.StartedAtUtc.Value.AddMinutes(x.DurationMinutes) > now,
            cancellationToken);

        if (anotherLiveMeeting)
            return RedirectWithMessage("Another meeting is already live for this batch.", false);

        session.Status = "Live";
        session.StartedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _meetRealtime.PublishSessionUpdateAsync(id, cancellationToken);

        return RedirectToAction(nameof(Room), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> End(int id, CancellationToken cancellationToken)
    {
        var session = await _dbContext.MeetSessions
            .Include(x => x.Participants)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (session is null)
            return NotFound();

        if (session.Status == "Live")
        {
            var now = DateTime.UtcNow;
            session.Status = "Completed";
            session.EndedAtUtc = now;
            if (session.RecordingStatus == "Recording")
            {
                session.RecordingStatus = "Stopped";
                session.RecordingEndedAtUtc = session.EndedAtUtc;
                session.RecordingRetentionUntilUtc = now.AddDays(90);
            }

            foreach (var participant in session.Participants.Where(x => x.Status is "Admitted" or "Pending"))
            {
                participant.Status = "Left";
                participant.LeftAtUtc = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _meetRealtime.PublishSessionUpdateAsync(id, cancellationToken);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var session = await _dbContext.MeetSessions
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (session is null)
            return NotFound();

        if (session.Status != "Scheduled")
            return RedirectWithMessage("Only scheduled meetings can be cancelled.", false);

        session.Status = "Cancelled";
        session.CancelledAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _meetRealtime.PublishSessionUpdateAsync(id, cancellationToken);

        return RedirectWithMessage("Meeting cancelled.", true);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetWaitingRoom(int id, bool enabled, CancellationToken cancellationToken)
    {
        var session = await _dbContext.MeetSessions
            .Include(x => x.Participants)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (session is null)
            return NotFound();

        session.WaitingRoomEnabled = enabled;
        if (!enabled)
        {
            var now = DateTime.UtcNow;
            foreach (var participant in session.Participants.Where(x => x.Status == "Pending"))
            {
                participant.Status = "Admitted";
                participant.AdmittedAtUtc = now;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _meetRealtime.PublishSessionUpdateAsync(id, cancellationToken);
        return RedirectToAction(nameof(Room), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewParticipant(int id, string decision, CancellationToken cancellationToken)
    {
        var participant = await _dbContext.MeetSessionParticipants
            .Include(x => x.MeetSession)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (participant is null)
            return NotFound();

        if (participant.Status != "Pending"
            || participant.MeetSession.Status != "Live"
            || !participant.MeetSession.StartedAtUtc.HasValue
            || participant.MeetSession.StartedAtUtc.Value.AddMinutes(participant.MeetSession.DurationMinutes) <= DateTime.UtcNow)
            return RedirectToAction(nameof(Room), new { id = participant.MeetSessionId });

        if (string.Equals(decision, "admit", StringComparison.OrdinalIgnoreCase))
        {
            participant.Status = "Admitted";
            participant.AdmittedAtUtc = DateTime.UtcNow;
        }
        else if (string.Equals(decision, "reject", StringComparison.OrdinalIgnoreCase))
        {
            participant.Status = "Rejected";
        }
        else
        {
            return BadRequest();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _meetRealtime.PublishSessionUpdateAsync(participant.MeetSessionId, cancellationToken);
        return RedirectToAction(nameof(Room), new { id = participant.MeetSessionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveParticipant(int id, CancellationToken cancellationToken)
    {
        var participant = await _dbContext.MeetSessionParticipants
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (participant is null)
            return NotFound();

        if (participant.Status == "Admitted" || participant.Status == "Pending")
        {
            participant.Status = "Removed";
            participant.LeftAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _meetRealtime.PublishSessionUpdateAsync(participant.MeetSessionId, cancellationToken);
        }

        return RedirectToAction(nameof(Room), new { id = participant.MeetSessionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateParticipantPermissions(
        int id,
        bool canUseMicrophone,
        bool canUseCamera,
        bool canShareScreen,
        CancellationToken cancellationToken)
    {
        var participant = await _dbContext.MeetSessionParticipants
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (participant is null)
            return NotFound();

        participant.CanUseMicrophone = canUseMicrophone;
        participant.CanUseCamera = canUseCamera;
        participant.CanShareScreen = canShareScreen;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _meetRealtime.PublishSessionUpdateAsync(participant.MeetSessionId, cancellationToken);

        return RedirectToAction(nameof(Room), new { id = participant.MeetSessionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRecording(int id, string action, CancellationToken cancellationToken)
    {
        var session = await _dbContext.MeetSessions
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (session is null)
            return NotFound();

        var now = DateTime.UtcNow;
        switch (action?.ToLowerInvariant())
        {
            case "enable" when session.Status is "Scheduled" or "Live":
                session.RecordingEnabled = true;
                if (session.RecordingStatus is "Disabled" or "Deleted")
                    session.RecordingStatus = "Ready";
                break;
            case "disable" when session.RecordingStatus != "Recording":
                session.RecordingEnabled = false;
                session.RecordingStatus = "Disabled";
                break;
            case "start" when session.Status == "Live" && session.RecordingEnabled
                && session.RecordingStatus is "Ready" or "Stopped":
                session.RecordingStatus = "Recording";
                session.RecordingStartedAtUtc = now;
                session.RecordingEndedAtUtc = null;
                session.RecordingRetentionUntilUtc = null;
                session.RecordingDeletedAtUtc = null;
                break;
            case "stop" when session.RecordingStatus == "Recording":
                session.RecordingStatus = "Stopped";
                session.RecordingEndedAtUtc = now;
                session.RecordingRetentionUntilUtc = now.AddDays(90);
                break;
            case "delete" when session.RecordingStatus == "Stopped":
                session.RecordingStatus = "Deleted";
                session.RecordingDeletedAtUtc = now;
                session.RecordingRetentionUntilUtc = null;
                session.RecordingEnabled = false;
                break;
            default:
                return RedirectWithMessage("Recording action is not valid for the current meeting state.", false);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _meetRealtime.PublishSessionUpdateAsync(id, cancellationToken);
        return RedirectToAction(nameof(Room), new { id });
    }

    private async Task CompleteExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expired = await _dbContext.MeetSessions
            .Include(x => x.Participants)
            .Where(x => x.Status == "Live"
                && x.StartedAtUtc.HasValue
                && x.StartedAtUtc.Value.AddMinutes(x.DurationMinutes) <= now)
            .ToListAsync(cancellationToken);

        foreach (var session in expired)
        {
            session.Status = "Completed";
            var endedAt = session.StartedAtUtc!.Value.AddMinutes(session.DurationMinutes);
            session.EndedAtUtc = endedAt;
            if (session.RecordingStatus == "Recording")
            {
                session.RecordingStatus = "Stopped";
                session.RecordingEndedAtUtc = endedAt;
                session.RecordingRetentionUntilUtc = endedAt.AddDays(90);
            }

            foreach (var participant in session.Participants.Where(x => x.Status is "Admitted" or "Pending"))
            {
                participant.Status = "Left";
                participant.LeftAtUtc = now;
            }
        }

        if (expired.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            foreach (var session in expired)
                await _meetRealtime.PublishSessionUpdateAsync(session.Id, cancellationToken);
        }
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
            || model.DurationMinutes is < 5 or > 480
            || model.ClientUtcOffsetMinutes is < -840 or > 840)
        {
            return CreateSessionResponse("Check the meeting title, description and duration.", false);
        }

        var batch = await _dbContext.MeetBatches
            .Include(x => x.Members)
            .SingleOrDefaultAsync(x => x.Id == model.MeetBatchId && !x.IsArchived, cancellationToken);

        if (batch is null || batch.Members.Count == 0)
        {
            return CreateSessionResponse("Select an active batch that has assigned students.", false);
        }

        var creatorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(creatorId))
            return Forbid();

        var isInstant = string.Equals(meetingMode, "instant", StringComparison.OrdinalIgnoreCase);
        if (isInstant)
        {
            var now = DateTime.UtcNow;
            var hasActiveMeeting = await _dbContext.MeetSessions.AnyAsync(
                session => session.MeetBatchId == batch.Id
                    && session.Status == "Live"
                    && session.StartedAtUtc.HasValue
                    && session.StartedAtUtc.Value.AddMinutes(session.DurationMinutes) > now,
                cancellationToken);

            if (hasActiveMeeting)
            {
                return CreateSessionResponse("This batch already has an active meeting.", false);
            }
        }
        DateTime startsAtUtc;
        if (isInstant)
        {
            startsAtUtc = DateTime.UtcNow;
        }
        else
        {
            if (!model.StartsAtLocal.HasValue)
            {
                return CreateSessionResponse("Choose a date and time for the scheduled meeting.", false);
            }

            var localStart = DateTime.SpecifyKind(model.StartsAtLocal.Value, DateTimeKind.Unspecified);
            startsAtUtc = DateTime.SpecifyKind(
                localStart.AddMinutes(model.ClientUtcOffsetMinutes),
                DateTimeKind.Utc);

            if (startsAtUtc <= DateTime.UtcNow.AddMinutes(1))
            {
                return CreateSessionResponse("Scheduled meeting time must be in the future.", false);
            }
        }

        var session = new MeetSession
        {
            MeetBatchId = batch.Id,
            Title = title,
            Description = description,
            RoomName = $"ciit-{Guid.NewGuid():N}",
            StartsAtUtc = startsAtUtc,
            DurationMinutes = model.DurationMinutes,
            Status = isInstant ? "Live" : "Scheduled",
            StartedAtUtc = isInstant ? DateTime.UtcNow : null,
            WaitingRoomEnabled = true,
            RecordingEnabled = false,
            RecordingStatus = "Disabled",
            CreatedByUserId = creatorId,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.MeetSessions.Add(session);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _meetRealtime.PublishSessionUpdateAsync(session.Id, cancellationToken);
        var successMessage = isInstant
            ? "Instant meeting created successfully."
            : "Meeting scheduled successfully.";
        return CreateSessionResponse(successMessage, true, session.Id);
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

    private IActionResult CreateSessionResponse(string message, bool succeeded, int? sessionId = null)
    {
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            return Json(new { success = succeeded, message, sessionId });

        TempData["MeetBatchMessage"] = message;
        TempData["MeetBatchMessageType"] = succeeded ? "success" : "error";
        return RedirectToAction(nameof(Index));
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
