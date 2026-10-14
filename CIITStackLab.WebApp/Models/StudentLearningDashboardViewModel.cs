using CIITStackLab.Application.DTOs;

namespace CIITStackLab.WebApp.Models;

public sealed class StudentLearningDashboardViewModel
{
    public IReadOnlyList<CourseDto> Courses { get; init; } = Array.Empty<CourseDto>();

    public int CompletedLessons { get; init; }

    public int TrackedLessons { get; init; }

    public int ProgressPercent => TrackedLessons == 0
        ? 0
        : (int)Math.Round(CompletedLessons * 100d / TrackedLessons);

    public IReadOnlyList<StudentLearningActivityViewModel> RecentActivity { get; init; }
        = Array.Empty<StudentLearningActivityViewModel>();

    public string? MostRecentCourseTitle { get; init; }

    public DateTime? MostRecentActivityAt { get; init; }
}

public sealed class StudentLearningActivityViewModel
{
    public string CourseTitle { get; init; } = string.Empty;

    public int ContentId { get; init; }

    public bool IsCompleted { get; init; }

    public DateTime LastAccessedAt { get; init; }
}
