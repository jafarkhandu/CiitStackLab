namespace CIITStackLab.Application.DTOs;

public sealed class PracticeProgramImportPreviewDto
{
    public string PreviewToken { get; set; } = Guid.NewGuid().ToString("N");

    public int ContentId { get; set; }

    public string ContentTitle { get; set; } = string.Empty;

    public string TopicTitle { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public List<PracticeProgramImportQuestionDto> Programs { get; set; } = new();

    public List<string> FileErrors { get; set; } = new();

    public int ValidQuestionCount => Programs.Count(item => item.IsValid);

    public int InvalidQuestionCount => Programs.Count(item => !item.IsValid);

    public bool CanImport =>
        FileErrors.Count == 0
        && Programs.Count > 0
        && Programs.All(item => item.IsValid);
}

public sealed class PracticeProgramImportQuestionDto
{
    public int Number { get; set; }

    public int SourceRow { get; set; }

    public string QuestionTitle { get; set; } = string.Empty;

    public string QuestionDescription { get; set; } = string.Empty;

    public bool IsDuplicateInDatabase { get; set; }

    public bool IsDuplicateInUpload { get; set; }

    public List<string> Issues { get; set; } = new();

    public bool IsValid =>
        Issues.Count == 0
        && !IsDuplicateInDatabase
        && !IsDuplicateInUpload;
}

public sealed class PracticeProgramImportOutcomeDto
{
    public bool Succeeded { get; init; }

    public string Message { get; init; } = string.Empty;

    public PracticeProgramImportPreviewDto? Preview { get; init; }
}
