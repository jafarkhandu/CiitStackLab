using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IInterviewQuestionAnalyzerService
{
    Task<IReadOnlyList<InterviewQuestionImportItemDto>> AnalyzeAsync(
        string contentTitle,
        string sourceText,
        CancellationToken cancellationToken = default,
        Func<string, CancellationToken, Task>? reportProgress = null);
}
