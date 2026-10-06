namespace CIITStackLab.Domain.Entities;

public class Lesson
{
    public int Id { get; set; }

    public int? TopicId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Slides { get; set; }

    public string? VideoName { get; set; }

    public int? Flag { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime? RestoredAt { get; set; }

    public Topic Topic { get; set; } = null!;
}
