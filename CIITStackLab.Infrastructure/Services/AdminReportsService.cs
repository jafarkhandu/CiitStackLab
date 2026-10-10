using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class AdminReportsService : IAdminReportsService
{
    private readonly ApplicationDbContext _dbContext;

    public AdminReportsService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminReportsOverviewDto> GetOverviewAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var endExclusive = endDate.Date.AddDays(1);

        var activeCourseCount = await _dbContext.Courses
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var activeTopicCount = await _dbContext.Topics
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var activeContentCount = await _dbContext.Lessons
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var activeMcqCount = await _dbContext.ContentQuestions
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var activeInterviewCount = await _dbContext.ContentInterviewQuestions
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var activePracticeCount = await _dbContext.ContentProgramQuestions
            .AsNoTracking()
            .CountAsync(x => x.Flag == 0, cancellationToken);

        var studentRoleId = await _dbContext.Roles
            .AsNoTracking()
            .Where(x => x.NormalizedName == "STUDENT")
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var activeStudentCount = 0;

        if (!string.IsNullOrWhiteSpace(studentRoleId))
        {
            activeStudentCount = await (
                from userRole in _dbContext.UserRoles.AsNoTracking()
                join user in _dbContext.Users.AsNoTracking()
                    on userRole.UserId equals user.Id
                where userRole.RoleId == studentRoleId
                      && user.IsActive
                select user.Id)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        var courseActivity = await _dbContext.Courses
            .AsNoTracking()
            .Where(x => x.Flag == 0
                && (x.UpdatedAt ?? x.CreatedAt) >= start
                && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new ReportActivityRow
            {
                Type = "Course",
                Title = x.Title,
                Date = x.UpdatedAt ?? x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var contentActivity = await _dbContext.Lessons
            .AsNoTracking()
            .Where(x => x.Flag == 0
                && (x.UpdatedAt ?? x.CreatedAt) >= start
                && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new ReportActivityRow
            {
                Type = "Learning Content",
                Title = x.Title ?? "Untitled content",
                Date = x.UpdatedAt ?? x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var mcqActivity = await _dbContext.ContentQuestions
            .AsNoTracking()
            .Where(x => x.Flag == 0
                && (x.UpdatedAt ?? x.CreatedAt) >= start
                && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new ReportActivityRow
            {
                Type = "MCQ",
                Title = x.Question,
                Date = x.UpdatedAt ?? x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var interviewActivity = await _dbContext.ContentInterviewQuestions
            .AsNoTracking()
            .Where(x => x.Flag == 0
                && (x.UpdatedAt ?? x.CreatedAt) >= start
                && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new ReportActivityRow
            {
                Type = "Interview Question",
                Title = x.Question,
                Date = x.UpdatedAt ?? x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var practiceActivity = await _dbContext.ContentProgramQuestions
            .AsNoTracking()
            .Where(x => x.Flag == 0
                && (x.UpdatedAt ?? x.CreatedAt) >= start
                && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new ReportActivityRow
            {
                Type = "Practice Program",
                Title = x.QuestionTitle,
                Date = x.UpdatedAt ?? x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var assessmentActivity = mcqActivity
            .Concat(interviewActivity)
            .Concat(practiceActivity)
            .ToList();

        var trend = Enumerable.Range(0, (endDate.Date - start).Days + 1)
            .Select(offset =>
            {
                var date = start.AddDays(offset);

                return new AdminReportTrendPointDto
                {
                    Date = date,
                    CourseChanges = CountForDate(courseActivity, date),
                    ContentChanges = CountForDate(contentActivity, date),
                    AssessmentChanges = CountForDate(assessmentActivity, date)
                };
            })
            .ToList();

        var recentActivity = courseActivity
            .Concat(contentActivity)
            .Concat(assessmentActivity)
            .OrderByDescending(x => x.Date)
            .Take(10)
            .Select(x => new AdminReportActivityDto
            {
                Type = x.Type,
                Title = x.Title,
                Date = x.Date
            })
            .ToList();

        return new AdminReportsOverviewDto
        {
            StartDate = start,
            EndDate = endDate.Date,
            ActiveCourseCount = activeCourseCount,
            ActiveTopicCount = activeTopicCount,
            ActiveContentCount = activeContentCount,
            ActiveAssessmentCount = activeMcqCount + activeInterviewCount + activePracticeCount,
            ActiveStudentCount = activeStudentCount,
            CourseActivityCount = courseActivity.Count,
            ContentActivityCount = contentActivity.Count,
            AssessmentActivityCount = assessmentActivity.Count,
            Trend = trend,
            RecentActivity = recentActivity
        };
    }

    private static int CountForDate(
        IEnumerable<ReportActivityRow> rows,
        DateTime date)
        => rows.Count(x => x.Date.Date == date.Date);

    private sealed class ReportActivityRow
    {
        public string Type { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public DateTime Date { get; init; }
    }
}
