namespace CIITStackLab.Domain.Entities;

public sealed class PracticeExamAttempt
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int CourseId { get; set; }

    public int TopicId { get; set; }

    public string QuestionIdsJson { get; set; } = "[]";

    public string AnswersJson { get; set; } = "{}";

    public int Score { get; set; }

    public int TotalQuestions { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }
}
