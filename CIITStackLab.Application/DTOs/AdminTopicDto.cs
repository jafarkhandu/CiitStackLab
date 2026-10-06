namespace CIITStackLab.Application.DTOs;

public sealed class AdminTopicDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? PublicFolderId { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminArchivedTopicDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? PublicFolderId { get; init; }
    public DateTime? DeletedAt { get; init; }
}
