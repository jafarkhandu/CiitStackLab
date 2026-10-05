namespace CIITStackLab.Domain.Entities;

public class Lesson
{
    public int Id { get; set; }

    public int CourseModuleId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ShortDescription { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public int EstimatedMinutes { get; set; }

    public CourseModule CourseModule { get; set; } = null!;

    public ICollection<Topic> Topics { get; set; } = new List<Topic>();
}