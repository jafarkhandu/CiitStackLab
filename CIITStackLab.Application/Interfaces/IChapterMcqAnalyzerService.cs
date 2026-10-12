using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IChapterMcqAnalyzerService
{
    Task<IReadOnlyList<McqImportQuestionDto>> AnalyzeAsync(
        string chapterTitle,
        string sourceText,
        CancellationToken cancellationToken = default,
        Func<string, CancellationToken, Task>? reportProgress = null);
}
