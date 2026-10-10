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
    public IReadOnlyList<AdminReportTrendPointDto> Trend { get; init; } =
        Array.Empty<AdminReportTrendPointDto>();
    public IReadOnlyList<AdminReportActivityDto> RecentActivity { get; init; } =
        Array.Empty<AdminReportActivityDto>();
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
