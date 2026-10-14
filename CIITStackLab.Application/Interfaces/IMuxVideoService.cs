namespace CIITStackLab.Application.Interfaces;

public interface IMuxVideoService
{
    Task<MuxDirectUploadDto> CreateDirectUploadAsync(
        string corsOrigin,
        CancellationToken cancellationToken = default);

    Task<MuxVideoStatusDto> GetDirectUploadStatusAsync(
        string uploadId,
        CancellationToken cancellationToken = default);

    Task<MuxVideoUploadResult> CompleteDirectUploadAsync(
        string uploadId,
        CancellationToken cancellationToken = default);
}

public sealed record MuxDirectUploadDto(
    string UploadId,
    string UploadUrl);

public sealed record MuxVideoStatusDto(
    string Stage,
    int Progress,
    string? PlaybackId,
    string? Message);

public sealed record MuxVideoUploadResult(
    string AssetId,
    string PlaybackId);
