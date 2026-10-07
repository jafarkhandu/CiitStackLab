namespace CIITStackLab.Domain.Entities;

public class Topic
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? PublicFolderId { get; set; }

    public decimal Price { get; set; }

    public int? DurationMinutes { get; set; }

    public int? Flag { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime? RestoredAt { get; set; }

    public ICollection<CourseModule> CourseModules { get; set; } = new List<CourseModule>();

    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
}
