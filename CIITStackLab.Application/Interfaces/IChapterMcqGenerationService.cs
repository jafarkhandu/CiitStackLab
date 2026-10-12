using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IChapterMcqGenerationService
{
    Task<IReadOnlyList<McqImportQuestionDto>> GenerateAsync(
        string chapterTitle,
        string sourceText,
        CancellationToken cancellationToken = default,
        Func<string, CancellationToken, Task>? reportProgress = null);
}
