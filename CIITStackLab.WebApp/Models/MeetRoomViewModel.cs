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
    public bool WaitingRoomEnabled { get; init; }
    public string ParticipantStatus { get; init; } = string.Empty;
    public bool RecordingEnabled { get; init; }
    public string RecordingStatus { get; init; } = string.Empty;
    public int ParticipantCount { get; init; }
    public IReadOnlyList<MeetRoomParticipantViewModel> Participants { get; init; } = [];
}

public sealed class MeetRoomParticipantViewModel
{
    public int Id { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool CanUseMicrophone { get; init; }
    public bool CanUseCamera { get; init; }
    public bool CanShareScreen { get; init; }
}
