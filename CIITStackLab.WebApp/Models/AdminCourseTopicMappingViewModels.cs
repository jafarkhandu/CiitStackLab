using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.WebApp.Models;

public sealed class AdminCourseTopicMappingViewModel
{
    public IReadOnlyList<CIITStackLab.Application.DTOs.AdminMappingCourseDto> Courses { get; init; } =
        Array.Empty<CIITStackLab.Application.DTOs.AdminMappingCourseDto>();

    public int? SelectedCourseId { get; init; }

    public string? SelectedCourseName { get; init; }

    public IReadOnlyList<CIITStackLab.Application.DTOs.AdminMappingTopicDto> Topics { get; init; } =
        Array.Empty<CIITStackLab.Application.DTOs.AdminMappingTopicDto>();

    public int AssignedTopicCount { get; init; }
}

public sealed class AdminCourseTopicMappingSaveModel
{
    [Required]
    public int CourseId { get; set; }

    public List<int> TopicIds { get; set; } = new();
}
