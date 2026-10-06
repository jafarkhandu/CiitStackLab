namespace CIITStackLab.Application.DTOs;

public sealed class AdminCourseDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public double? FeesAmount { get; init; }
    public DateTime? FeesChangeDate { get; init; }
    public double? InstallmentPercentage { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminArchivedCourseDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime? DeletedAt { get; init; }
}
