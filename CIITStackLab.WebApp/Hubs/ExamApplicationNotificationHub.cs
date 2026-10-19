using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CIITStackLab.WebApp.Hubs;

[Authorize(Roles = "Admin,Super User")]
public sealed class ExamApplicationNotificationHub : Hub
{
    public const string AdminGroup = "exam-admins";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        await base.OnConnectedAsync();
    }
}
