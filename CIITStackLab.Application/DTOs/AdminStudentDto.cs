namespace CIITStackLab.Application.DTOs;

public sealed class AdminStudentDto
{
    public string Id { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public bool IsActive { get; init; }
    public bool EmailConfirmed { get; init; }
}

public sealed class AdminStudentPageDto
{
    public IReadOnlyList<AdminStudentDto> ActiveStudents { get; init; } =
        Array.Empty<AdminStudentDto>();

    public IReadOnlyList<AdminStudentDto> InactiveStudents { get; init; } =
        Array.Empty<AdminStudentDto>();
}
