namespace CIITStackLab.Domain.Entities;

public sealed class MeetSession
{
    public int Id { get; set; }
    public int MeetBatchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public MeetBatch MeetBatch { get; set; } = null!;
}
