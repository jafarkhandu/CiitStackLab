using System.Security.Claims;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.WebApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Areas.Student.Controllers;

[Area("Student")]
[Authorize(Roles = "Student")]
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
        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId))
            return Challenge();

        var now = DateTime.UtcNow;
        var batches = await _dbContext.MeetBatches
            .AsNoTracking()
            .Where(batch => !batch.IsArchived
                && batch.Members.Any(member => member.StudentUserId == studentId))
            .OrderBy(batch => batch.Name)
            .Select(batch => new StudentMeetBatchListItemViewModel
            {
                Id = batch.Id,
                Name = batch.Name,
                Description = batch.Description,
                BatchCode = batch.BatchCode,
                MemberCount = batch.Members.Count,
                CreatedAtUtc = batch.CreatedAtUtc,
                LiveSessionId = batch.Sessions
                    .Where(session => session.Status == "Live"
                        && session.StartsAtUtc <= now
                        && session.StartsAtUtc.AddMinutes(session.DurationMinutes) > now)
                    .OrderByDescending(session => session.StartsAtUtc)
                    .Select(session => (int?)session.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return View(batches);
    }

    [HttpGet]
    public async Task<IActionResult> Room(int id, CancellationToken cancellationToken)
    {
        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId))
            return Challenge();

        var now = DateTime.UtcNow;
        var session = await _dbContext.MeetSessions
            .AsNoTracking()
            .Where(item => item.Id == id
                && item.Status == "Live"
                && item.StartedAtUtc.HasValue
                && item.StartedAtUtc.Value.AddMinutes(item.DurationMinutes) > now
                && !item.MeetBatch.IsArchived
                && item.MeetBatch.Members.Any(member => member.StudentUserId == studentId))
            .Select(item => new
            {
                item.Id,
                item.MeetBatchId,
                item.Title,
                item.Description,
                BatchName = item.MeetBatch.Name,
                BatchCode = item.MeetBatch.BatchCode,
                item.StartsAtUtc,
                item.DurationMinutes,
                item.Status,
                item.WaitingRoomEnabled,
                item.RecordingEnabled,
                item.RecordingStatus
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (session is null)
            return NotFound();

        var participant = await _dbContext.MeetSessionParticipants
            .SingleOrDefaultAsync(x => x.MeetSessionId == id && x.StudentUserId == studentId, cancellationToken);

        if (participant is null)
        {
            participant = new MeetSessionParticipant
            {
                MeetSessionId = id,
                StudentUserId = studentId,
                Status = session.WaitingRoomEnabled ? "Pending" : "Admitted",
                RequestedAtUtc = now,
                AdmittedAtUtc = session.WaitingRoomEnabled ? null : now
            };
            _dbContext.MeetSessionParticipants.Add(participant);
        }
        else if (participant.Status == "Left")
        {
            participant.Status = session.WaitingRoomEnabled ? "Pending" : "Admitted";
            participant.RequestedAtUtc = now;
            participant.AdmittedAtUtc = session.WaitingRoomEnabled ? null : now;
            participant.LeftAtUtc = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return View(new MeetRoomViewModel
        {
            Id = session.Id,
            Title = session.Title,
            Description = session.Description,
            BatchName = session.BatchName,
            BatchCode = session.BatchCode,
            StartsAtUtc = session.StartsAtUtc,
            DurationMinutes = session.DurationMinutes,
            Status = session.Status,
            IsHost = false,
            WaitingRoomEnabled = session.WaitingRoomEnabled,
            ParticipantStatus = participant.Status,
            RecordingEnabled = session.RecordingEnabled,
            RecordingStatus = session.RecordingStatus
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(int id, CancellationToken cancellationToken)
    {
        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId))
            return Challenge();

        var participant = await _dbContext.MeetSessionParticipants
            .Include(x => x.MeetSession)
            .SingleOrDefaultAsync(x => x.MeetSessionId == id && x.StudentUserId == studentId, cancellationToken);

        if (participant is not null && participant.Status is "Pending" or "Admitted")
        {
            participant.Status = "Left";
            participant.LeftAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(Index));
    }
}}
