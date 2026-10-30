namespace CIITStackLab.WebApp.Models;

public sealed class StudentMeetBatchListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string BatchCode { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public int? LiveSessionId { get; init; }
    public StudentMeetUpcomingSessionViewModel? UpcomingSession { get; init; }
    public IReadOnlyList<StudentMeetSessionHistoryViewModel> RecentSessions { get; init; } = [];
}

public sealed class StudentMeetSessionHistoryViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime SessionDateUtc { get; init; }
}

public sealed class StudentMeetUpcomingSessionViewModel
{
    public string Title { get; init; } = string.Empty;
    public DateTime StartsAtUtc { get; init; }
}
