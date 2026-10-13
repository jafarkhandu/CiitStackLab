using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IPracticeProgramImportService
{
    void CachePreview(PracticeProgramImportPreviewDto preview, string userId);

    PracticeProgramImportPreviewDto? GetPreview(string previewToken, string userId);

    Task<PracticeProgramImportPreviewDto> PreviewAsync(
        int noteId,
        string fileName,
        Stream fileStream,
        string userId,
        CancellationToken cancellationToken = default,
        Func<string, CancellationToken, Task>? reportProgress = null);

    Task<PracticeProgramImportOutcomeDto> ConfirmAsync(
        string previewToken,
        string userId,
        CancellationToken cancellationToken = default);
}
