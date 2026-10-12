namespace CIITStackLab.Domain.Entities;

public sealed class StudentAssessmentAttempt
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int CourseId { get; set; }

    public int ContentId { get; set; }

    public int Score { get; set; }

    public int TotalQuestions { get; set; }

    public string AnswersJson { get; set; } = "[]";

    public DateTime SubmittedAt { get; set; }
}
