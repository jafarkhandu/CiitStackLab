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
                CreatedAtUtc = batch.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return View(batches);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return NotFound();

        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId))
            return Challenge();

        var batch = await _dbContext.MeetBatches
            .AsNoTracking()
            .Where(item => item.Id == id
                && !item.IsArchived
                && item.Members.Any(member => member.StudentUserId == studentId))
            .Select(item => new StudentMeetBatchDetailsViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                BatchCode = item.BatchCode,
                MemberCount = item.Members.Count,
                JoinedAtUtc = item.Members
                    .Where(member => member.StudentUserId == studentId)
                    .Select(member => (DateTime?)member.AddedAtUtc)
                    .FirstOrDefault(),
                Sessions = item.Sessions
                    .OrderBy(session => session.StartsAtUtc)
                    .Select(session => new StudentMeetSessionViewModel
                    {
                        Id = session.Id,
                        Title = session.Title,
                        Description = session.Description,
                        StartsAtUtc = session.StartsAtUtc,
                        DurationMinutes = session.DurationMinutes,
                        Status = session.Status
                    })
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (batch is null)
            return NotFound();

        return View(batch);
    }

    [HttpGet]
    public async Task<IActionResult> Room(int id, CancellationToken cancellationToken)
    {
        var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(studentId))
            return Challenge();

        var session = await _dbContext.MeetSessions
            .AsNoTracking()
            .Include(item => item.MeetBatch)
            .Where(item => item.Id == id
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

        ViewData["BatchId"] = await _dbContext.MeetSessions
            .Where(item => item.Id == id)
            .Select(item => item.MeetBatchId)
            .SingleAsync(cancellationToken);

        return View(session);
    }
}
