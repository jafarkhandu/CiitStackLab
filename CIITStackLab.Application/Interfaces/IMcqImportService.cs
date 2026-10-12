using CIITStackLab.Application.DTOs;

namespace CIITStackLab.Application.Interfaces;

public interface IMcqImportService
{
    Task<McqImportPreviewDto> PreviewAsync(
        int topicId,
        string fileName,
        Stream fileStream,
        string userId,
        CancellationToken cancellationToken = default);

    Task<McqImportOutcomeDto> ConfirmAsync(
        string previewToken,
        string userId,
        CancellationToken cancellationToken = default);
}
