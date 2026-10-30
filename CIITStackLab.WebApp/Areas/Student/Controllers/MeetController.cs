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
            .Include(item => item.MeetBatch)
            .Where(item => item.Id == id
                && item.Status == "Live"
                && item.StartsAtUtc <= now
                && item.StartsAtUtc.AddMinutes(item.DurationMinutes) > now
                && !item.MeetBatch.IsArchived
                && item.MeetBatch.Members.Any(member => member.StudentUserId == studentId))
            .Select(item => new MeetRoomViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description,
                BatchName = item.MeetBatch.Name,
                BatchCode = item.MeetBatch.BatchCode,
                StartsAtUtc = item.StartsAtUtc,
                DurationMinutes = item.DurationMinutes,
                Status = item.Status,
                IsHost = false
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (session is null)
            return NotFound();

        return View(session);
    }
}
