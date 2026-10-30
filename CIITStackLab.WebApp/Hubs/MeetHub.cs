using System.Security.Claims;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Hubs;

[Authorize]
public sealed class MeetHub : Hub
{
    private readonly ApplicationDbContext _dbContext;

    public MeetHub(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public static string SessionGroup(int sessionId) => $"meet-session-{sessionId}";
    public static string BatchGroup(int batchId) => $"meet-batch-{batchId}";
    public const string AdminGroup = "meet-admins";

    public async Task JoinAdminUpdates()
    {
        if (!Context.User!.IsInRole("Admin") && !Context.User.IsInRole("Super User"))
            throw new HubException("Access denied.");

        await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup, Context.ConnectionAborted);
    }

    public async Task JoinMyBatchUpdates()
    {
        var studentId = Context.UserIdentifier;
        if (string.IsNullOrWhiteSpace(studentId) || !Context.User!.IsInRole("Student"))
            throw new HubException("Access denied.");

        var batchIds = await _dbContext.MeetBatchMembers
            .AsNoTracking()
            .Where(member => member.StudentUserId == studentId && !member.MeetBatch.IsArchived)
            .Select(member => member.MeetBatchId)
            .ToListAsync(Context.ConnectionAborted);

        foreach (var batchId in batchIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, BatchGroup(batchId), Context.ConnectionAborted);
    }

    public async Task JoinMeetingRoom(int sessionId)
    {
        if (sessionId <= 0)
            throw new HubException("Invalid meeting.");

        var session = await _dbContext.MeetSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId && !item.MeetBatch.IsArchived)
            .Select(item => new
            {
                item.Id,
                item.MeetBatchId,
                item.Status,
                item.StartedAtUtc,
                item.DurationMinutes
            })
            .SingleOrDefaultAsync(Context.ConnectionAborted);

        if (session is null)
            throw new HubException("Meeting not found.");

        var isAdmin = Context.User!.IsInRole("Admin") || Context.User.IsInRole("Super User");
        if (!isAdmin)
        {
            var studentId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(studentId)
                || !Context.User.IsInRole("Student")
                || session.Status != "Live"
                || !session.StartedAtUtc.HasValue
                || session.StartedAtUtc.Value.AddMinutes(session.DurationMinutes) <= DateTime.UtcNow
                || !await _dbContext.MeetBatchMembers.AsNoTracking().AnyAsync(
                    member => member.MeetBatchId == session.MeetBatchId
                        && member.StudentUserId == studentId,
                    Context.ConnectionAborted)
                || !await _dbContext.MeetSessionParticipants.AsNoTracking().AnyAsync(
                    participant => participant.MeetSessionId == sessionId
                        && participant.StudentUserId == studentId
                        && (participant.Status == "Pending" || participant.Status == "Admitted"),
                    Context.ConnectionAborted))
            {
                throw new HubException("You are not allowed to join this meeting.");
            }
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId), Context.ConnectionAborted);
    }
}
