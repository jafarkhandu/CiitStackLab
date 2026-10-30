namespace CIITStackLab.WebApp.Models;

public sealed class MeetRoomViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string BatchName { get; init; } = string.Empty;
    public string BatchCode { get; init; } = string.Empty;
    public DateTime StartsAtUtc { get; init; }
    public int DurationMinutes { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsHost { get; init; }
}
