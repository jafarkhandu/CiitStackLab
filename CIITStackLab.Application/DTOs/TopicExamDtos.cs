namespace CIITStackLab.Application.DTOs;

public sealed class TopicExamScheduleRequest
{
    public int CourseId { get; set; }
    public int TopicId { get; set; }
    public DateTime ScheduledAtLocal { get; set; }
    public int TimezoneOffsetMinutes { get; set; }
}

public sealed class TopicExamSummaryDto
{
    public long AttemptId { get; init; }
    public int CourseId { get; init; }
    public int TopicId { get; init; }
    public string TopicTitle { get; init; } = string.Empty;
    public DateTime ScheduledAtUtc { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? DeadlineAtUtc { get; init; }
    public DateTime? SubmittedAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public int Score { get; init; }
    public int TotalQuestions { get; init; }
    public bool Passed { get; init; }
    public int WarningCount { get; init; }
    public string? TerminationReason { get; init; }
}

public sealed class TopicExamQuestionDto
{
    public int QuestionId { get; init; }
    public string Question { get; init; } = string.Empty;
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();
    public int? SelectedOptionNumber { get; init; }
}

public sealed class TopicExamSessionDto
{
    public long AttemptId { get; init; }
    public DateTime ScheduledAtUtc { get; init; }
    public DateTime? VerificationStartedAtUtc { get; init; }
    public DateTime? DeadlineAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<TopicExamQuestionDto> Questions { get; init; } = Array.Empty<TopicExamQuestionDto>();
}

public sealed class TopicExamAnswerInput
{
    public int QuestionId { get; set; }
    public int? SelectedOptionNumber { get; set; }
}

public sealed class TopicExamSecurityEventInput
{
    public string EventType { get; set; } = string.Empty;
    public string? Details { get; set; }
}
