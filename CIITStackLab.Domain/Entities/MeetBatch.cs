namespace CIITStackLab.Domain.Entities;

public sealed class MeetBatch
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<MeetBatchMember> Members { get; set; } = new List<MeetBatchMember>();
}
