namespace CIITStackLab.Application.DTOs;

public sealed class InterviewQuestionImportPreviewDto
{
    public string PreviewToken { get; set; } = string.Empty;

    public int TopicId { get; set; }

    public string TopicTitle { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public List<string> FileErrors { get; set; } = new();

    public List<InterviewQuestionImportItemDto> Questions { get; set; } = new();

    public int ValidQuestionCount => Questions.Count(question => question.IsValid);

    public int InvalidQuestionCount => Questions.Count - ValidQuestionCount;

    public bool CanImport =>
        !string.IsNullOrWhiteSpace(PreviewToken)
        && FileErrors.Count == 0
        && Questions.Count == 20
        && Questions.All(question => question.IsValid);
}

public sealed class InterviewQuestionImportItemDto
{
    public int Number { get; set; }

    public int SourceLine { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    public bool IsDuplicateInDatabase { get; set; }

    public List<string> Issues { get; set; } = new();

    public bool IsValid => Issues.Count == 0;
}

public sealed class InterviewQuestionImportOutcomeDto
{
    public bool Succeeded { get; init; }

    public int ImportedCount { get; init; }

    public string Message { get; init; } = string.Empty;

    public InterviewQuestionImportPreviewDto? Preview { get; init; }

    public static InterviewQuestionImportOutcomeDto Success(int importedCount)
    {
        return new InterviewQuestionImportOutcomeDto
        {
            Succeeded = true,
            ImportedCount = importedCount,
            Message = importedCount + " interview question(s) imported successfully."
        };
    }

    public static InterviewQuestionImportOutcomeDto Failure(
        string message,
        InterviewQuestionImportPreviewDto? preview = null)
    {
        return new InterviewQuestionImportOutcomeDto
        {
            Succeeded = false,
            Message = message,
            Preview = preview
        };
    }
}

public sealed class InterviewQuestionTopicLookupDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
}
