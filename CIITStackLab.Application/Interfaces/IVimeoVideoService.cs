namespace CIITStackLab.Application.Interfaces;

public interface IVimeoVideoService
{
    Task<VimeoDirectUploadDto> CreateDirectUploadAsync(
        string fileName,
        long fileSize,
        string title,
        string? existingVideoUrl,
        CancellationToken cancellationToken = default);

    Task<VimeoVideoStatusDto> GetVideoStatusAsync(
        string videoId,
        CancellationToken cancellationToken = default);
}

public sealed record VimeoDirectUploadDto(
    string VideoId,
    string UploadUrl,
    string PlayerUrl);

public sealed record VimeoVideoStatusDto(
    string Stage,
    int Progress,
    string? PlayerUrl,
    string? Message);
