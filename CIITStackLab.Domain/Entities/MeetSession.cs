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
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public bool WaitingRoomEnabled { get; set; } = true;
    public bool RecordingEnabled { get; set; }
    public string RecordingStatus { get; set; } = "Disabled";
    public DateTime? RecordingStartedAtUtc { get; set; }
    public DateTime? RecordingEndedAtUtc { get; set; }
    public DateTime? RecordingRetentionUntilUtc { get; set; }
    public DateTime? RecordingDeletedAtUtc { get; set; }

    public MeetBatch MeetBatch { get; set; } = null!;
    public ICollection<MeetSessionParticipant> Participants { get; set; } = new List<MeetSessionParticipant>();
}
