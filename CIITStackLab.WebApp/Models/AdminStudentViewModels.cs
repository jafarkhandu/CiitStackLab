namespace CIITStackLab.WebApp.Models;

public sealed class AdminStudentIndexViewModel
{
    public IReadOnlyList<CIITStackLab.Application.DTOs.AdminStudentDto> ActiveStudents { get; init; } =
        Array.Empty<CIITStackLab.Application.DTOs.AdminStudentDto>();

    public IReadOnlyList<CIITStackLab.Application.DTOs.AdminStudentDto> InactiveStudents { get; init; } =
        Array.Empty<CIITStackLab.Application.DTOs.AdminStudentDto>();
}
