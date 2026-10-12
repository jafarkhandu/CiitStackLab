using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IInterviewQuestionImportService
{
    void CachePreview(InterviewQuestionImportPreviewDto preview, string userId);

    InterviewQuestionImportPreviewDto? GetPreview(string previewToken, string userId);

    Task<InterviewQuestionImportPreviewDto> PreviewAsync(
        int contentId,
        string fileName,
        Stream fileStream,
        string userId,
        CancellationToken cancellationToken = default,
        Func<string, CancellationToken, Task>? reportProgress = null);

    Task<InterviewQuestionImportOutcomeDto> ConfirmAsync(
        string previewToken,
        string userId,
        CancellationToken cancellationToken = default);
}
