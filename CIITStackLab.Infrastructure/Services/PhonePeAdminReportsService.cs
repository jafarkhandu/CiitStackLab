using CIITStackLab.Application.DTOs;
using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.Infrastructure.Services;

public sealed class PhonePeAdminReportsService : IPhonePeAdminReportsService
{
    private readonly ApplicationDbContext _dbContext;

    public PhonePeAdminReportsService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PhonePeAdminRevenueReportPageDto> GetRevenueReportsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var endExclusive = endDate.Date.AddDays(1);

        var transactions = await _dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt < endExclusive)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var courseIds = transactions.Select(x => x.CourseId).Distinct().ToList();
        var userIds = transactions.Select(x => x.UserId).Distinct().ToList();

        var courses = await _dbContext.Courses
            .AsNoTracking()
            .Where(x => courseIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Title })
            .ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .Select(x => new { x.Id, x.UserName, x.Email })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var purchases = await _dbContext.StudentCoursePurchases
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && courseIds.Contains(x.CourseId))
            .Select(x => new { x.UserId, x.CourseId })
            .ToListAsync(cancellationToken);

        var purchasedCourses = purchases
            .Select(x => $"{x.UserId}|{x.CourseId}")
            .ToHashSet(StringComparer.Ordinal);

        static bool IsCompleted(string status) =>
            string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);

        static bool IsFailed(string status) =>
            string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase);

        var successful = transactions.Where(x => IsCompleted(x.Status)).ToList();
        var failed = transactions.Where(x => IsFailed(x.Status)).ToList();
        var pending = transactions.Where(x => !IsCompleted(x.Status) && !IsFailed(x.Status)).ToList();

        var rows = transactions
            .Take(50)
            .Select(transaction =>
            {
                users.TryGetValue(transaction.UserId, out var user);
                courses.TryGetValue(transaction.CourseId, out var courseTitle);

                return new PhonePeAdminPaymentReportRowDto
                {
                    PaymentId = transaction.Id,
                    StudentName = user?.UserName ?? user?.Email ?? "Unknown student",
                    StudentEmail = user?.Email ?? string.Empty,
                    CourseTitle = courseTitle ?? $"Course #{transaction.CourseId}",
                    CourseId = transaction.CourseId,
                    Amount = transaction.Amount,
                    Status = IsCompleted(transaction.Status)
                        ? "Completed"
                        : IsFailed(transaction.Status)
                            ? "Failed"
                            : "Pending",
                    MerchantOrderId = transaction.MerchantOrderId,
                    PhonePeOrderId = transaction.PhonePeOrderId ?? string.Empty,
                    CreatedAt = transaction.CreatedAt,
                    UpdatedAt = transaction.UpdatedAt,
                    CompletedAt = transaction.CompletedAt,
                    CourseAccessGranted = IsCompleted(transaction.Status)
                        && purchasedCourses.Contains($"{transaction.UserId}|{transaction.CourseId}")
                };
            })
            .ToList();

        return new PhonePeAdminRevenueReportPageDto
        {
            StartDate = start,
            EndDate = endExclusive.AddDays(-1),
            SuccessfulRevenue = successful.Sum(x => x.Amount),
            AttemptedAmount = transactions.Sum(x => x.Amount),
            FailedAmount = failed.Sum(x => x.Amount),
            PendingAmount = pending.Sum(x => x.Amount),
            PaymentCount = transactions.Count,
            SuccessfulPaymentCount = successful.Count,
            FailedPaymentCount = failed.Count,
            PendingPaymentCount = pending.Count,
            RecentPayments = rows
        };
    }
}
