using CIITStackLab.Application.Interfaces;
using CIITStackLab.Infrastructure.Identity;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CIITStackLab.Infrastructure.Services;

public sealed record AdminEmailAudienceCounts(
    int AllStudents,
    int NoPurchase,
    int OnePurchaseInactive);

public sealed record AdminEmailCampaignResult(
    int Targeted,
    int Sent,
    int Failed);

public sealed class AdminEmailCampaignService
{
    private const string AllAudience = "all";
    private const string NoPurchaseAudience = "no-purchase";
    private const string OnePurchaseInactiveAudience = "one-purchase-inactive";

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ILogger<AdminEmailCampaignService> _logger;

    public AdminEmailCampaignService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        ILogger<AdminEmailCampaignService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<AdminEmailAudienceCounts> GetAudienceCountsAsync(
        int inactivityDays = 14,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetAudienceSnapshotAsync(inactivityDays, cancellationToken);

        return new AdminEmailAudienceCounts(
            snapshot.Students.Count,
            snapshot.Students.Count(user => snapshot.PurchaseCounts.GetValueOrDefault(user.Id) == 0),
            snapshot.Students.Count(user =>
                snapshot.PurchaseCounts.GetValueOrDefault(user.Id) == 1
                && (snapshot.LastAccessedAt.TryGetValue(user.Id, out var lastAccessedAt)
                    ? lastAccessedAt <= snapshot.InactivityCutoff
                    : snapshot.LastPurchasedAt.TryGetValue(user.Id, out var lastPurchasedAt)
                        && lastPurchasedAt <= snapshot.InactivityCutoff)));
    }

    public async Task<AdminEmailCampaignResult> SendAsync(
        string audience,
        string subject,
        string message,
        int inactivityDays,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("An email subject and message are required.");
        }

        var snapshot = await GetAudienceSnapshotAsync(inactivityDays, cancellationToken);
        var recipients = audience switch
        {
            AllAudience => snapshot.Students,
            NoPurchaseAudience => snapshot.Students
                .Where(user => snapshot.PurchaseCounts.GetValueOrDefault(user.Id) == 0)
                .ToList(),
            OnePurchaseInactiveAudience => snapshot.Students
                .Where(user =>
                    snapshot.PurchaseCounts.GetValueOrDefault(user.Id) == 1
                    && (snapshot.LastAccessedAt.TryGetValue(user.Id, out var lastAccessedAt)
                    ? lastAccessedAt <= snapshot.InactivityCutoff
                    : snapshot.LastPurchasedAt.TryGetValue(user.Id, out var lastPurchasedAt)
                        && lastPurchasedAt <= snapshot.InactivityCutoff))
                .ToList(),
            _ => throw new ArgumentException("Select a valid email audience.", nameof(audience))
        };

        var sent = 0;
        var failed = 0;

        foreach (var recipient in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _emailService.SendCampaignEmailAsync(
                    recipient.Email!,
                    recipient.UserName ?? recipient.Email!,
                    subject.Trim(),
                    message,
                    cancellationToken);

                sent++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failed++;
                _logger.LogWarning(
                    exception,
                    "Email campaign delivery failed for student account {UserId}.",
                    recipient.Id);
            }
        }

        return new AdminEmailCampaignResult(recipients.Count, sent, failed);
    }

    private async Task<AudienceSnapshot> GetAudienceSnapshotAsync(
        int inactivityDays,
        CancellationToken cancellationToken)
    {
        if (inactivityDays is < 1 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(inactivityDays));
        }

        var roleStudents = await _userManager.GetUsersInRoleAsync("Student");
        var students = roleStudents
            .Where(user =>
                user.IsActive
                && user.EmailConfirmed
                && !string.IsNullOrWhiteSpace(user.Email))
            .ToList();

        var studentIds = students.Select(user => user.Id).ToList();

        if (studentIds.Count == 0)
        {
            return new AudienceSnapshot(
                students,
                new Dictionary<string, int>(),
                new Dictionary<string, DateTime>(),
                new Dictionary<string, DateTime>(),
                DateTime.UtcNow.AddDays(-inactivityDays));
        }

        var purchaseRows = await _dbContext.StudentCoursePurchases
            .AsNoTracking()
            .Where(purchase => studentIds.Contains(purchase.UserId))
            .GroupBy(purchase => purchase.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Count = group.Count(),
                LatestPurchasedAt = group.Max(purchase => purchase.PurchasedAt)
            })
            .ToListAsync(cancellationToken);

        var purchaseCounts = purchaseRows.ToDictionary(item => item.UserId, item => item.Count);
        var lastPurchasedAt = purchaseRows.ToDictionary(item => item.UserId, item => item.LatestPurchasedAt);

        var lastAccessedAt = await _dbContext.StudentLessonProgress
            .AsNoTracking()
            .Where(progress => studentIds.Contains(progress.UserId)
                && _dbContext.StudentCoursePurchases.AsNoTracking().Any(purchase =>
                    purchase.UserId == progress.UserId
                    && purchase.CourseId == progress.CourseId))
            .GroupBy(progress => progress.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                LastAccessedAt = group.Max(progress => progress.LastAccessedAt)
            })
            .ToDictionaryAsync(
                item => item.UserId,
                item => item.LastAccessedAt,
                cancellationToken);

        return new AudienceSnapshot(
            students,
            purchaseCounts,
            lastAccessedAt,
            lastPurchasedAt,
            DateTime.UtcNow.AddDays(-inactivityDays));
    }

    private sealed record AudienceSnapshot(
        List<ApplicationUser> Students,
        Dictionary<string, int> PurchaseCounts,
        Dictionary<string, DateTime> LastAccessedAt,
        Dictionary<string, DateTime> LastPurchasedAt,
        DateTime InactivityCutoff);
}
