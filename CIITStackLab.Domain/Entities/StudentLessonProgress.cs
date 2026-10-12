namespace CIITStackLab.Domain.Entities;

public sealed class StudentLessonProgress
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int CourseId { get; set; }

    public int ContentId { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime LastAccessedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
