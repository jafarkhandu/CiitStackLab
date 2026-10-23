using System.Security.Claims;
using CIITStackLab.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Controllers;

[Authorize(Roles = "Student")]
public sealed class PaymentsController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public PaymentsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("Payments/HistoryData")]
    public async Task<IActionResult> HistoryData(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var transactions = await _dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(transaction => transaction.UserId == userId)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Select(transaction => new
            {
                transaction.Id,
                transaction.CourseId,
                transaction.MerchantOrderId,
                transaction.Amount,
                transaction.Status,
                transaction.CreatedAt,
                transaction.UpdatedAt,
                transaction.CompletedAt
            })
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
            return Json(new { records = Array.Empty<object>() });

        var courseIds = transactions.Select(transaction => transaction.CourseId).Distinct().ToList();
        var courses = await _dbContext.Courses
            .AsNoTracking()
            .Where(course => courseIds.Contains(course.Id))
            .Select(course => new { course.Id, course.Title })
            .ToDictionaryAsync(course => course.Id, course => course.Title, cancellationToken);

        var records = transactions.Select(transaction => new
        {
            id = transaction.Id,
            courseId = transaction.CourseId,
            courseTitle = courses.TryGetValue(transaction.CourseId, out var title) ? title : "Course unavailable",
            merchantOrderId = transaction.MerchantOrderId,
            amount = transaction.Amount,
            status = NormalizeStatus(transaction.Status),
            createdAt = transaction.CreatedAt,
            updatedAt = transaction.UpdatedAt,
            completedAt = transaction.CompletedAt,
            gateway = "Razorpay",
            paymentMethod = "Razorpay Checkout"
        });

        return Json(new { records });
    }

    [HttpGet("Payments/Details/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var transaction = await _dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(item => item.Id == id && item.UserId == userId)
            .Select(item => new
            {
                item.Id,
                item.CourseId,
                item.MerchantOrderId,
                item.Amount,
                item.Status,
                item.CreatedAt,
                item.UpdatedAt,
                item.CompletedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (transaction is null) return NotFound();

        var courseTitle = await _dbContext.Courses
            .AsNoTracking()
            .Where(course => course.Id == transaction.CourseId)
            .Select(course => course.Title)
            .FirstOrDefaultAsync(cancellationToken);

        var purchase = await _dbContext.StudentCoursePurchases
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.CourseId == transaction.CourseId)
            .OrderByDescending(item => item.PurchasedAt)
            .Select(item => new { item.Id, item.PurchasePrice, item.PurchasedAt })
            .FirstOrDefaultAsync(cancellationToken);

        var isCompleted = string.Equals(transaction.Status, "Completed", StringComparison.OrdinalIgnoreCase);

        return Json(new
        {
            id = transaction.Id,
            courseId = transaction.CourseId,
            courseTitle = courseTitle ?? "Course unavailable",
            amount = transaction.Amount,
            status = NormalizeStatus(transaction.Status),
            createdAt = transaction.CreatedAt,
            updatedAt = transaction.UpdatedAt,
            completedAt = transaction.CompletedAt,
            merchantOrderId = transaction.MerchantOrderId,
            gateway = "Razorpay",
            paymentMethod = "Razorpay Checkout",
            purchase = isCompleted && purchase is not null
                ? new
                {
                    id = purchase.Id,
                    amount = purchase.PurchasePrice,
                    purchasedAt = purchase.PurchasedAt
                }
                : null
        });
    }

    private static string NormalizeStatus(string status)
    {
        return status switch
        {
            "Completed" => "SUCCESS",
            "Failed" => "FAILED",
            "Pending" => "PENDING",
            "Created" => "CREATED",
            _ => status.ToUpperInvariant()
        };
    }
}
