namespace CIITStackLab.Domain.Entities;

public class ContentInterviewQuestion
{
    public int Id { get; set; }

    public int? ContentId { get; set; }

    public string? Question { get; set; }

    public string? Answer { get; set; }

    public int? Flag { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime? RestoredAt { get; set; }

    public Lesson? Content { get; set; }
}
