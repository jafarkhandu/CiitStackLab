using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.WebApp.Models;

public sealed class AdminCourseIndexViewModel
{
    public IReadOnlyList<CIITStackLab.Application.DTOs.AdminCourseDto> ActiveCourses { get; init; } = Array.Empty<CIITStackLab.Application.DTOs.AdminCourseDto>();
    public IReadOnlyList<CIITStackLab.Application.DTOs.AdminArchivedCourseDto> ArchivedCourses { get; init; } = Array.Empty<CIITStackLab.Application.DTOs.AdminArchivedCourseDto>();
    public AdminCourseFormModel? EditCourse { get; init; }
}

public sealed class AdminCourseFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Course name is required.")]
    [StringLength(100, ErrorMessage = "Course name cannot exceed 100 characters.")]
    public string CourseName { get; set; } = string.Empty;
}
