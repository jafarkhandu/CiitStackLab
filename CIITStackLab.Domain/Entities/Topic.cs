namespace CIITStackLab.Domain.Entities;

public class Topic
{
    public int Id { get; set; }

    public int LessonId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public Lesson Lesson { get; set; } = null!;
}