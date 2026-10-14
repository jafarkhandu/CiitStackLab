namespace CIITStackLab.Application.Interfaces;

public interface IMuxVideoService
{
    Task<MuxVideoUploadResult> UploadVideoAsync(
        Stream videoStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}

public sealed record MuxVideoUploadResult(
    string AssetId,
    string PlaybackId);
