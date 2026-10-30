namespace CIITStackLab.WebApp.Models;

public sealed class StudentMeetBatchListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string BatchCode { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class StudentMeetBatchDetailsViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string BatchCode { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public DateTime? JoinedAtUtc { get; init; }
    public IReadOnlyList<StudentMeetSessionViewModel> Sessions { get; init; } = [];
}

public sealed class StudentMeetSessionViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime StartsAtUtc { get; init; }
    public int DurationMinutes { get; init; }
    public string Status { get; init; } = string.Empty;
}
