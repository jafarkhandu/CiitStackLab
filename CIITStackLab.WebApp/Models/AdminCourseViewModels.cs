using System.ComponentModel.DataAnnotations;

namespace CIITStackLab.WebApp.Models;

public sealed class AdminCourseIndexViewModel
{
    public IReadOnlyList<CIITStackLab.Application.DTOs.AdminCourseDto> ActiveCourses { get; init; } =
        Array.Empty<CIITStackLab.Application.DTOs.AdminCourseDto>();

    public IReadOnlyList<CIITStackLab.Application.DTOs.AdminArchivedCourseDto> ArchivedCourses { get; init; } =
        Array.Empty<CIITStackLab.Application.DTOs.AdminArchivedCourseDto>();
}

public sealed class AdminCourseFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Course name is required.")]
    [StringLength(100, ErrorMessage = "Course name cannot exceed 100 characters.")]
    [Display(Name = "Course name")]
    public string CourseName { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Fees cannot be negative.")]
    [Display(Name = "Fees amount")]
    public double? FeesAmount { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fees change date")]
    public DateTime? FeesChangeDate { get; set; }

    [Range(0, 100, ErrorMessage = "Installment percentage must be between 0 and 100.")]
    [Display(Name = "Installment percentage")]
    public double? InstallmentPercentage { get; set; }
}
