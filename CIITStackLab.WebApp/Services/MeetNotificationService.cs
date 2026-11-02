using CIITStackLab.WebApp.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CIITStackLab.WebApp.Services;

public sealed class MeetNotificationService
{
    private readonly IHubContext<MeetHub> _hub;

    public MeetNotificationService(IHubContext<MeetHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyAdminsAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        return SendToastAsync(_hub.Clients.Group(MeetHub.AdminGroup), title, message, "info", cancellationToken);
    }

    public Task NotifySessionAdminsAsync(int sessionId, string title, string message, string type = "info", CancellationToken cancellationToken = default)
    {
        return SendToastAsync(_hub.Clients.Group(MeetHub.AdminSessionGroup(sessionId)), title, message, type, cancellationToken);
    }

    public Task NotifyBatchAsync(int batchId, string title, string message, string type = "info", CancellationToken cancellationToken = default)
    {
        return SendToastAsync(_hub.Clients.Group(MeetHub.BatchGroup(batchId)), title, message, type, cancellationToken);
    }

    public Task NotifyStudentAsync(string studentUserId, string title, string message, string type = "info", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studentUserId))
            return Task.CompletedTask;

        return SendToastAsync(_hub.Clients.User(studentUserId), title, message, type, cancellationToken);
    }

    private static Task SendToastAsync(
        IClientProxy target,
        string title,
        string message,
        string type,
        CancellationToken cancellationToken)
    {
        return target.SendAsync("MeetToast", new
        {
            title,
            message,
            type,
            createdAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }
}
