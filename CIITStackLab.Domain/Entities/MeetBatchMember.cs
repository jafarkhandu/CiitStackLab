namespace CIITStackLab.Domain.Entities;

public sealed class MeetBatchMember
{
    public int Id { get; set; }
    public int MeetBatchId { get; set; }
    public string StudentUserId { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; }

    public MeetBatch MeetBatch { get; set; } = null!;
}
