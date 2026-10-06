namespace CIITStackLab.Domain.Entities;

public class CourseModule
{
    public int Id { get; set; }

    public int CourseId { get; set; }

    public int TopicId { get; set; }

    public int? Flag { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime? RestoredAt { get; set; }

    public Course Course { get; set; } = null!;

    public Topic Topic { get; set; } = null!;
}
