namespace CIITStackLab.Application.Interfaces;

public interface IMuxVideoService
{
    Task<MuxDirectUploadDto> CreateDirectUploadAsync(
        string corsOrigin,
        CancellationToken cancellationToken = default);
}

public sealed record MuxDirectUploadDto(
    string UploadId,
    string UploadUrl,
    string Status);
