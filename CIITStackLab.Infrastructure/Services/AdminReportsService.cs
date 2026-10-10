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
        var end = endDate.Date;
        var endExclusive = end.AddDays(1);

        var activeCourseCount = await _dbContext.Courses.AsNoTracking().CountAsync(x => x.Flag == 0, cancellationToken);
        var activeTopicCount = await _dbContext.Topics.AsNoTracking().CountAsync(x => x.Flag == 0, cancellationToken);
        var activeContentCount = await _dbContext.Lessons.AsNoTracking().CountAsync(x => x.Flag == 0, cancellationToken);
        var activeMcqCount = await _dbContext.ContentQuestions.AsNoTracking().CountAsync(x => x.Flag == 0, cancellationToken);
        var activeInterviewCount = await _dbContext.ContentInterviewQuestions.AsNoTracking().CountAsync(x => x.Flag == 0, cancellationToken);
        var activePracticeCount = await _dbContext.ContentProgramQuestions.AsNoTracking().CountAsync(x => x.Flag == 0, cancellationToken);
        var activeStudentCount = await GetActiveStudentCountAsync(cancellationToken);

        var courseActivity = await _dbContext.Courses.AsNoTracking()
            .Where(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new ReportActivityRow { Type = "Course", Title = x.Title, Date = (x.UpdatedAt ?? x.CreatedAt)!.Value })
            .ToListAsync(cancellationToken);

        var contentActivity = await _dbContext.Lessons.AsNoTracking()
            .Where(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new ReportActivityRow { Type = "Learning Content", Title = x.Title ?? "Untitled content", Date = (x.UpdatedAt ?? x.CreatedAt)!.Value })
            .ToListAsync(cancellationToken);

        var assessmentActivity = await GetAssessmentActivityAsync(start, endExclusive, cancellationToken);

        var trend = Enumerable.Range(0, (end - start).Days + 1)
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
            .Select(x => new AdminReportActivityDto { Type = x.Type, Title = x.Title, Date = x.Date })
            .ToList();

        return new AdminReportsOverviewDto
        {
            StartDate = start,
            EndDate = end,
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

    public async Task<AdminCourseReportPageDto> GetCourseReportsAsync(
        string? search,
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = search?.Trim();
        var courses = await _dbContext.Courses.AsNoTracking()
            .Where(x => includeArchived || x.Flag == 0)
            .OrderBy(x => x.Title)
            .Select(x => new AdminCourseReportRowDto
            {
                Id = x.Id,
                Title = x.Title,
                UpdatedAt = x.UpdatedAt ?? x.CreatedAt,
                IsActive = x.Flag == 0
            })
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            courses = courses
                .Where(x => x.Title.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var courseIds = courses.Select(x => x.Id).ToList();
        var modules = await _dbContext.CourseModules.AsNoTracking()
            .Where(x => courseIds.Contains(x.CourseId) && x.Flag == 0 && x.Topic.Flag == 0)
            .Select(x => new { x.CourseId, x.TopicId })
            .ToListAsync(cancellationToken);

        var topicIds = modules.Select(x => x.TopicId).Distinct().ToList();
        var content = await _dbContext.Lessons.AsNoTracking()
            .Where(x => topicIds.Contains(x.TopicId) && x.Flag == 0)
            .Select(x => new { x.Id, x.TopicId })
            .ToListAsync(cancellationToken);

        var contentIds = content.Select(x => x.Id).ToList();
        var mcqCounts = await _dbContext.ContentQuestions.AsNoTracking()
            .Where(x => contentIds.Contains(x.ContentId) && x.Flag == 0)
            .GroupBy(x => x.ContentId)
            .Select(g => new { ContentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ContentId, x => x.Count, cancellationToken);

        var interviewCounts = await _dbContext.ContentInterviewQuestions.AsNoTracking()
            .Where(x => contentIds.Contains(x.ContentId) && x.Flag == 0)
            .GroupBy(x => x.ContentId)
            .Select(g => new { ContentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ContentId, x => x.Count, cancellationToken);

        var practiceCounts = await _dbContext.ContentProgramQuestions.AsNoTracking()
            .Where(x => contentIds.Contains(x.ContentId) && x.Flag == 0)
            .GroupBy(x => x.ContentId)
            .Select(g => new { ContentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ContentId, x => x.Count, cancellationToken);

        var result = courses.Select(course =>
        {
            var courseTopicIds = modules.Where(x => x.CourseId == course.Id).Select(x => x.TopicId).Distinct().ToList();
            var courseContentIds = content.Where(x => courseTopicIds.Contains(x.TopicId)).Select(x => x.Id).ToList();
            return new AdminCourseReportRowDto
            {
                Id = course.Id,
                Title = course.Title,
                UpdatedAt = course.UpdatedAt,
                IsActive = course.IsActive,
                TopicCount = courseTopicIds.Count,
                ContentCount = courseContentIds.Count,
                McqCount = courseContentIds.Sum(id => mcqCounts.GetValueOrDefault(id)),
                InterviewCount = courseContentIds.Sum(id => interviewCounts.GetValueOrDefault(id)),
                PracticeCount = courseContentIds.Sum(id => practiceCounts.GetValueOrDefault(id))
            };
        }).ToList();

        return new AdminCourseReportPageDto
        {
            Search = normalizedSearch,
            IncludeArchived = includeArchived,
            TotalCourses = await _dbContext.Courses.AsNoTracking().CountAsync(cancellationToken),
            ActiveCourses = await _dbContext.Courses.AsNoTracking().CountAsync(x => x.Flag == 0, cancellationToken),
            ArchivedCourses = await _dbContext.Courses.AsNoTracking().CountAsync(x => x.Flag != 0, cancellationToken),
            Courses = result
        };
    }

    public async Task<AdminStudentReportPageDto> GetStudentReportsAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = search?.Trim();
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();
        var studentRoleId = await _dbContext.Roles.AsNoTracking()
            .Where(x => x.NormalizedName == "STUDENT")
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(studentRoleId))
        {
            return new AdminStudentReportPageDto { Search = normalizedSearch, Status = normalizedStatus };
        }

        var students = await (
            from userRole in _dbContext.UserRoles.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking() on userRole.UserId equals user.Id
            where userRole.RoleId == studentRoleId
            select new AdminStudentReportRowDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                IsActive = user.IsActive,
                EmailConfirmed = user.EmailConfirmed
            })
            .Distinct()
            .OrderBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        if (normalizedStatus == "active")
            students = students.Where(x => x.IsActive).ToList();
        else if (normalizedStatus == "inactive")
            students = students.Where(x => !x.IsActive).ToList();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            students = students.Where(x =>
                x.UserName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || x.Email.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || x.PhoneNumber.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return new AdminStudentReportPageDto
        {
            Search = normalizedSearch,
            Status = normalizedStatus,
            TotalStudents = await GetStudentCountAsync(studentRoleId, null, cancellationToken),
            ActiveStudents = await GetStudentCountAsync(studentRoleId, true, cancellationToken),
            InactiveStudents = await GetStudentCountAsync(studentRoleId, false, cancellationToken),
            Students = students
        };
    }

    public async Task<AdminAssessmentReportPageDto> GetAssessmentReportsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;
        var endExclusive = end.AddDays(1);

        var mcqCount = await _dbContext.ContentQuestions.AsNoTracking().CountAsync(x => x.Flag == 0, cancellationToken);
        var interviewCount = await _dbContext.ContentInterviewQuestions.AsNoTracking().CountAsync(x => x.Flag == 0, cancellationToken);
        var practiceCount = await _dbContext.ContentProgramQuestions.AsNoTracking().CountAsync(x => x.Flag == 0, cancellationToken);

        var mcqs = await _dbContext.ContentQuestions.AsNoTracking()
            .Where(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new AdminAssessmentReportRowDto { Type = "MCQ", Title = x.Question ?? "Untitled MCQ", UpdatedAt = x.UpdatedAt ?? x.CreatedAt })
            .ToListAsync(cancellationToken);

        var interviews = await _dbContext.ContentInterviewQuestions.AsNoTracking()
            .Where(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new AdminAssessmentReportRowDto { Type = "Interview Question", Title = x.Question ?? "Untitled interview question", UpdatedAt = x.UpdatedAt ?? x.CreatedAt })
            .ToListAsync(cancellationToken);

        var practice = await _dbContext.ContentProgramQuestions.AsNoTracking()
            .Where(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new AdminAssessmentReportRowDto { Type = "Practice Program", Title = x.QuestionTitle ?? "Untitled practice program", UpdatedAt = x.UpdatedAt ?? x.CreatedAt })
            .ToListAsync(cancellationToken);

        return new AdminAssessmentReportPageDto
        {
            StartDate = start,
            EndDate = end,
            McqCount = mcqCount,
            InterviewCount = interviewCount,
            PracticeCount = practiceCount,
            McqChanges = mcqs.Count,
            InterviewChanges = interviews.Count,
            PracticeChanges = practice.Count,
            RecentItems = mcqs.Concat(interviews).Concat(practice).OrderByDescending(x => x.UpdatedAt).Take(20).ToList()
        };
    }

    public async Task<AdminRevenueReportPageDto> GetRevenueReportsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;
        var endExclusive = end.AddDays(1);
        var payments = await _dbContext.StudentPayments.AsNoTracking()
            .Where(x => x.Flag == 0 && x.PaymentDate >= start && x.PaymentDate < endExclusive)
            .OrderByDescending(x => x.PaymentDate)
            .ToListAsync(cancellationToken);

        var paid = payments.Where(x => x.IsPaid).ToList();
        var pending = payments.Where(x => !x.IsPaid).ToList();

        return new AdminRevenueReportPageDto
        {
            StartDate = start,
            EndDate = end,
            TotalPaid = paid.Sum(x => x.PaymentAmount),
            TotalRecorded = payments.Sum(x => x.PaymentAmount),
            PendingAmount = pending.Sum(x => x.PaymentAmount),
            PaymentCount = payments.Count,
            PaidPaymentCount = paid.Count,
            PendingPaymentCount = pending.Count,
            RecentPayments = payments.Take(25).Select(x => new AdminPaymentReportRowDto
            {
                PaymentId = x.Id,
                RegistrationId = x.RegistrationId,
                Amount = x.PaymentAmount,
                PaymentMode = x.PaymentMode ?? "Not specified",
                Description = x.PaymentDescription ?? string.Empty,
                IsPaid = x.IsPaid,
                PaymentDate = x.PaymentDate
            }).ToList()
        };
    }

    public async Task<AdminActivityReportPageDto> GetActivityReportsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;
        var endExclusive = end.AddDays(1);

        var loginQuery = _dbContext.StudentLoginActivities.AsNoTracking()
            .Where(x => x.Flag == 0 && x.LoginTime >= start && x.LoginTime < endExclusive);

        var courseChanges = await _dbContext.Courses.AsNoTracking()
            .CountAsync(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive, cancellationToken);
        var contentChanges = await _dbContext.Lessons.AsNoTracking()
            .CountAsync(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive, cancellationToken);
        var assessmentChanges = (await _dbContext.ContentQuestions.AsNoTracking().CountAsync(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive, cancellationToken))
            + await _dbContext.ContentInterviewQuestions.AsNoTracking().CountAsync(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive, cancellationToken)
            + await _dbContext.ContentProgramQuestions.AsNoTracking().CountAsync(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive, cancellationToken);

        var recentLogins = await loginQuery
            .OrderByDescending(x => x.LoginTime)
            .Take(25)
            .Select(x => new AdminLoginActivityRowDto
            {
                ActivityId = x.Id,
                StudentId = x.StudentId,
                LoginTime = x.LoginTime,
                LogoutTime = x.LogoutTime,
                IpAddress = x.IpAddress ?? "Not recorded"
            })
            .ToListAsync(cancellationToken);

        return new AdminActivityReportPageDto
        {
            StartDate = start,
            EndDate = end,
            LoginCount = await loginQuery.CountAsync(cancellationToken),
            ActiveSessions = await loginQuery.CountAsync(x => x.LogoutTime == null, cancellationToken),
            CourseChanges = courseChanges,
            ContentChanges = contentChanges,
            AssessmentChanges = assessmentChanges,
            RecentLogins = recentLogins
        };
    }

    private async Task<int> GetActiveStudentCountAsync(CancellationToken cancellationToken)
    {
        var roleId = await _dbContext.Roles.AsNoTracking()
            .Where(x => x.NormalizedName == "STUDENT")
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(roleId) ? 0 : await GetStudentCountAsync(roleId, true, cancellationToken);
    }

    private async Task<int> GetStudentCountAsync(string roleId, bool? isActive, CancellationToken cancellationToken)
    {
        return await (
            from userRole in _dbContext.UserRoles.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking() on userRole.UserId equals user.Id
            where userRole.RoleId == roleId && (!isActive.HasValue || user.IsActive == isActive.Value)
            select user.Id)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private async Task<List<ReportActivityRow>> GetAssessmentActivityAsync(
        DateTime start,
        DateTime endExclusive,
        CancellationToken cancellationToken)
    {
        var mcqs = await _dbContext.ContentQuestions.AsNoTracking()
            .Where(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new ReportActivityRow { Type = "MCQ", Title = x.Question ?? "Untitled MCQ", Date = (x.UpdatedAt ?? x.CreatedAt)!.Value })
            .ToListAsync(cancellationToken);
        var interviews = await _dbContext.ContentInterviewQuestions.AsNoTracking()
            .Where(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new ReportActivityRow { Type = "Interview Question", Title = x.Question ?? "Untitled interview question", Date = (x.UpdatedAt ?? x.CreatedAt)!.Value })
            .ToListAsync(cancellationToken);
        var practice = await _dbContext.ContentProgramQuestions.AsNoTracking()
            .Where(x => x.Flag == 0 && (x.UpdatedAt ?? x.CreatedAt) >= start && (x.UpdatedAt ?? x.CreatedAt) < endExclusive)
            .Select(x => new ReportActivityRow { Type = "Practice Program", Title = x.QuestionTitle ?? "Untitled practice program", Date = (x.UpdatedAt ?? x.CreatedAt)!.Value })
            .ToListAsync(cancellationToken);
        return mcqs.Concat(interviews).Concat(practice).ToList();
    }

    private static int CountForDate(IEnumerable<ReportActivityRow> rows, DateTime date)
        => rows.Count(x => x.Date.Date == date.Date);

    private sealed class ReportActivityRow
    {
        public string Type { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public DateTime Date { get; init; }
    }
}
