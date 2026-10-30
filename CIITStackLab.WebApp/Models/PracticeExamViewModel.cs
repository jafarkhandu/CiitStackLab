namespace CIITStackLab.WebApp.Models;

public sealed class PracticeExamTakeViewModel
{
    public int AttemptId { get; init; }

    public int CourseId { get; init; }

    public int TopicId { get; init; }

    public string CourseTitle { get; init; } = string.Empty;

    public string TopicTitle { get; init; } = string.Empty;

    public DateTime StartedAtUtc { get; init; }

    public IReadOnlyList<PracticeExamQuestionViewModel> Questions { get; init; } =
        Array.Empty<PracticeExamQuestionViewModel>();
}

public sealed class PracticeExamQuestionViewModel
{
    public int QuestionId { get; init; }

    public string Question { get; init; } = string.Empty;

    public string Option1 { get; init; } = string.Empty;

    public string Option2 { get; init; } = string.Empty;

    public string Option3 { get; init; } = string.Empty;

    public string Option4 { get; init; } = string.Empty;
}

public sealed class PracticeExamResultViewModel
{
    public int AttemptId { get; init; }

    public string CourseTitle { get; init; } = string.Empty;

    public string TopicTitle { get; init; } = string.Empty;

    public int Score { get; init; }

    public int TotalQuestions { get; init; }

    public DateTime StartedAtUtc { get; init; }

    public DateTime SubmittedAtUtc { get; init; }

    public int Percentage => TotalQuestions == 0
        ? 0
        : (int)Math.Round(Score * 100d / TotalQuestions);
}
