namespace CIITStackLab.Application.DTOs;

public sealed class McqImportPreviewDto
{
    public string PreviewToken { get; set; } = string.Empty;

    public int ContentId { get; set; }

    public string ContentLabel { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public List<string> FileErrors { get; set; } = new();

    public List<McqImportQuestionDto> Questions { get; set; } = new();

    public bool HasPreview => !string.IsNullOrWhiteSpace(PreviewToken);

    public int ValidQuestionCount => Questions.Count(question => question.IsValid);

    public int InvalidQuestionCount => Questions.Count - ValidQuestionCount;

    public bool CanImport =>
        HasPreview
        && FileErrors.Count == 0
        && Questions.Count > 0
        && Questions.All(question => question.IsValid);
}

public sealed class McqImportQuestionDto
{
    public int Number { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Option1 { get; set; } = string.Empty;

    public string Option2 { get; set; } = string.Empty;

    public string Option3 { get; set; } = string.Empty;

    public string Option4 { get; set; } = string.Empty;

    public int CorrectOptionNumber { get; set; }

    public bool IsDuplicateInDatabase { get; set; }

    public List<string> Issues { get; set; } = new();

    public bool IsValid => Issues.Count == 0;

    public string CorrectOptionLabel => CorrectOptionNumber switch
    {
        1 => "A",
        2 => "B",
        3 => "C",
        4 => "D",
        _ => "—"
    };
}

public sealed class McqImportOutcomeDto
{
    public bool Succeeded { get; init; }

    public int ImportedCount { get; init; }

    public string Message { get; init; } = string.Empty;

    public McqImportPreviewDto? Preview { get; init; }

    public static McqImportOutcomeDto Success(int importedCount)
    {
        return new McqImportOutcomeDto
        {
            Succeeded = true,
            ImportedCount = importedCount,
            Message = $"{importedCount} MCQ(s) imported successfully."
        };
    }

    public static McqImportOutcomeDto Failure(
        string message,
        McqImportPreviewDto? preview = null)
    {
        return new McqImportOutcomeDto
        {
            Succeeded = false,
            Message = message,
            Preview = preview
        };
    }
}
