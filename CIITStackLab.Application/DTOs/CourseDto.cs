namespace CIITStackLab.Application.DTOs;

public sealed class CourseDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ShortDescription { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Level { get; init; } = string.Empty;
    public int DurationHours { get; init; }
    public bool IsPublished { get; init; }
}