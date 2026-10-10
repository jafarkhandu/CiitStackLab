namespace CIITStackLab.Application.DTOs;

public sealed class AdminReportsOverviewDto
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int ActiveCourseCount { get; init; }
    public int ActiveTopicCount { get; init; }
    public int ActiveContentCount { get; init; }
    public int ActiveAssessmentCount { get; init; }
    public int ActiveStudentCount { get; init; }
    public int CourseActivityCount { get; init; }
    public int ContentActivityCount { get; init; }
    public int AssessmentActivityCount { get; init; }
    public IReadOnlyList<AdminReportTrendPointDto> Trend { get; init; } = Array.Empty<AdminReportTrendPointDto>();
    public IReadOnlyList<AdminReportActivityDto> RecentActivity { get; init; } = Array.Empty<AdminReportActivityDto>();
}

public sealed class AdminReportTrendPointDto
{
    public DateTime Date { get; init; }
    public int CourseChanges { get; init; }
    public int ContentChanges { get; init; }
    public int AssessmentChanges { get; init; }
    public int TotalChanges => CourseChanges + ContentChanges + AssessmentChanges;
}

public sealed class AdminReportActivityDto
{
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime Date { get; init; }
}

public sealed class AdminCourseReportPageDto
{
    public string? Search { get; init; }
    public bool IncludeArchived { get; init; }
    public int TotalCourses { get; init; }
    public int ActiveCourses { get; init; }
    public int ArchivedCourses { get; init; }
    public IReadOnlyList<AdminCourseReportRowDto> Courses { get; init; } = Array.Empty<AdminCourseReportRowDto>();
}

public sealed class AdminCourseReportRowDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public int TopicCount { get; init; }
    public int ContentCount { get; init; }
    public int McqCount { get; init; }
    public int InterviewCount { get; init; }
    public int PracticeCount { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool IsActive { get; init; }
}

public sealed class AdminStudentReportPageDto
{
    public string? Search { get; init; }
    public string Status { get; init; } = "all";
    public int TotalStudents { get; init; }
    public int ActiveStudents { get; init; }
    public int InactiveStudents { get; init; }
    public IReadOnlyList<AdminStudentReportRowDto> Students { get; init; } = Array.Empty<AdminStudentReportRowDto>();
}

public sealed class AdminStudentReportRowDto
{
    public string Id { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool EmailConfirmed { get; init; }
}

public sealed class AdminAssessmentReportPageDto
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int McqCount { get; init; }
    public int InterviewCount { get; init; }
    public int PracticeCount { get; init; }
    public int McqChanges { get; init; }
    public int InterviewChanges { get; init; }
    public int PracticeChanges { get; init; }
    public IReadOnlyList<AdminAssessmentReportRowDto> RecentItems { get; init; } = Array.Empty<AdminAssessmentReportRowDto>();
}

public sealed class AdminAssessmentReportRowDto
{
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminRevenueReportPageDto
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal TotalRecorded { get; init; }
    public decimal PendingAmount { get; init; }
    public int PaymentCount { get; init; }
    public int PaidPaymentCount { get; init; }
    public int PendingPaymentCount { get; init; }
    public IReadOnlyList<AdminPaymentReportRowDto> RecentPayments { get; init; } = Array.Empty<AdminPaymentReportRowDto>();
}

public sealed class AdminPaymentReportRowDto
{
    public int PaymentId { get; init; }
    public int RegistrationId { get; init; }
    public decimal Amount { get; init; }
    public string PaymentMode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsPaid { get; init; }
    public DateTime PaymentDate { get; init; }
}

public sealed class AdminActivityReportPageDto
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int LoginCount { get; init; }
    public int ActiveSessions { get; init; }
    public int CourseChanges { get; init; }
    public int ContentChanges { get; init; }
    public int AssessmentChanges { get; init; }
    public IReadOnlyList<AdminLoginActivityRowDto> RecentLogins { get; init; } = Array.Empty<AdminLoginActivityRowDto>();
}

public sealed class AdminLoginActivityRowDto
{
    public int ActivityId { get; init; }
    public int StudentId { get; init; }
    public DateTime LoginTime { get; init; }
    public DateTime? LogoutTime { get; init; }
    public string IpAddress { get; init; } = string.Empty;
}
