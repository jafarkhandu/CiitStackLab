using System.Security.Claims;
using CIITStackLab.Domain.Entities;
using CIITStackLab.Infrastructure.Payments;
using CIITStackLab.Infrastructure.Persistence;
using CIITStackLab.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CIITStackLab.WebApp.Controllers;

[Authorize(Roles = "Student")]
public sealed class RazorpayCheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PhonePePaymentService _payments;
    private readonly CoursePurchaseService _coursePurchaseService;
    private readonly ILogger<RazorpayCheckoutController> _logger;

    public RazorpayCheckoutController(
        ApplicationDbContext db,
        PhonePePaymentService payments,
        CoursePurchaseService coursePurchaseService,
        ILogger<RazorpayCheckoutController> logger)
    {
        _db = db;
        _payments = payments;
        _coursePurchaseService = coursePurchaseService;
        _logger = logger;
    }

    [HttpGet("Payments/RazorpayCheckout")]
    public async Task<IActionResult> Checkout(
        string merchantOrderId,
        string razorpayOrderId,
        string keyId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        if (string.IsNullOrWhiteSpace(merchantOrderId) || string.IsNullOrWhiteSpace(razorpayOrderId))
            return BadRequest("Payment order details are missing.");

        var transaction = await _db.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.MerchantOrderId == merchantOrderId && item.UserId == userId,
                cancellationToken);
        if (transaction is null) return NotFound();

        var order = await _payments.GetOrderAsync(razorpayOrderId, cancellationToken);
        if (!string.Equals(order.Receipt, merchantOrderId, StringComparison.Ordinal)
            || order.Amount != ToPaise(transaction.Amount))
        {
            return BadRequest("The payment order does not match this transaction.");
        }

        ViewData["RazorpayKeyId"] = string.IsNullOrWhiteSpace(keyId) ? _payments.KeyId : keyId;
        ViewData["RazorpayOrderId"] = razorpayOrderId;
        ViewData["MerchantOrderId"] = merchantOrderId;
        ViewData["RazorpayAmount"] = order.Amount;
        return View();
    }

    [HttpGet("Payments/Razorpay/Success")]
    public async Task<IActionResult> PaymentSuccess(
        string merchantOrderId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();
        if (string.IsNullOrWhiteSpace(merchantOrderId)) return BadRequest();

        var transaction = await _db.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.MerchantOrderId == merchantOrderId && item.UserId == userId,
                cancellationToken);

        if (transaction is null
            || !string.Equals(transaction.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("The payment has not been confirmed.");
        }

        if (string.Equals(transaction.PaymentPurpose, "ExamRetake", StringComparison.Ordinal))
        {
            return Redirect(GetSuccessUrl(transaction));
        }

        ViewData["CourseId"] = transaction.CourseId;
        ViewData["CourseUrl"] = GetSuccessUrl(transaction);
        return View();
    }

    [HttpPost("Payments/Razorpay/Verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(
        [FromBody] RazorpayVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        if (request is null
            || string.IsNullOrWhiteSpace(request.MerchantOrderId)
            || string.IsNullOrWhiteSpace(request.RazorpayOrderId)
            || string.IsNullOrWhiteSpace(request.RazorpayPaymentId)
            || string.IsNullOrWhiteSpace(request.RazorpaySignature))
        {
            return BadRequest(new { message = "Payment verification details are incomplete." });
        }

        var transaction = await _db.PaymentTransactions.FirstOrDefaultAsync(
            item => item.MerchantOrderId == request.MerchantOrderId && item.UserId == userId,
            cancellationToken);
        if (transaction is null) return NotFound(new { message = "The payment order could not be found." });

        if (string.Equals(transaction.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new
            {
                success = true,
                message = "Payment already confirmed.",
                redirectUrl = GetPostPaymentUrl(transaction)
            });
        }

        var order = await _payments.GetOrderAsync(request.RazorpayOrderId, cancellationToken);
        if (!string.Equals(order.Id, request.RazorpayOrderId, StringComparison.Ordinal)
            || !string.Equals(order.Receipt, transaction.MerchantOrderId, StringComparison.Ordinal)
            || order.Amount != ToPaise(transaction.Amount)
            || !string.Equals(order.Status, "paid", StringComparison.OrdinalIgnoreCase)
            || order.AmountPaid != order.Amount)
        {
            return BadRequest(new { message = "Razorpay has not confirmed the full payment yet." });
        }

        if (!_payments.VerifyPaymentSignature(
                request.RazorpayOrderId,
                request.RazorpayPaymentId,
                request.RazorpaySignature))
        {
            _logger.LogWarning(
                "Invalid Razorpay signature for merchant order {MerchantOrderId}.",
                transaction.MerchantOrderId);
            return BadRequest(new { message = "Payment verification failed." });
        }

        // Keep the verified Razorpay order reference in the existing gateway-reference
        // column until the dedicated Razorpay columns are introduced in a DB migration.
        transaction.PhonePeOrderId = request.RazorpayOrderId;

        if (string.Equals(transaction.PaymentPurpose, "ExamRetake", StringComparison.Ordinal))
        {
            transaction.Status = "Completed";
            transaction.CompletedAt ??= DateTime.UtcNow;
            transaction.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var alreadyPurchased = await _coursePurchaseService.HasPurchasedCourseAsync(
                transaction.UserId,
                transaction.CourseId,
                cancellationToken);
            var purchaseCompleted = await _coursePurchaseService.CompleteWithPriceAsync(
                transaction.UserId,
                transaction.CourseId,
                transaction.Amount,
                cancellationToken);

            if (!purchaseCompleted)
            {
                transaction.Status = "Failed";
                transaction.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                return BadRequest(new { message = "Payment was verified, but course access could not be completed." });
            }

            transaction.Status = "Completed";
            transaction.CompletedAt = DateTime.UtcNow;
            transaction.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            if (!alreadyPurchased)
            {
                _logger.LogInformation(
                    "Razorpay course purchase completed for user {UserId}, course {CourseId}.",
                    transaction.UserId,
                    transaction.CourseId);
            }
        }

        return Json(new
        {
            success = true,
            message = "Payment successful. Your access is now available.",
            redirectUrl = GetPostPaymentUrl(transaction)
        });
    }

    private string GetPostPaymentUrl(PaymentTransaction transaction)
    {
        if (string.Equals(transaction.PaymentPurpose, "ExamRetake", StringComparison.Ordinal))
            return GetSuccessUrl(transaction);

        return Url.Action(nameof(PaymentSuccess), "RazorpayCheckout", new
        {
            merchantOrderId = transaction.MerchantOrderId
        }) ?? GetSuccessUrl(transaction);
    }

    private string GetSuccessUrl(PaymentTransaction transaction)
    {
        if (string.Equals(transaction.PaymentPurpose, "ExamRetake", StringComparison.Ordinal))
            return Url.Action("Index", "StudentExaminations") ?? "/Examinations";

        return Url.Action("CourseEnvironment", "Courses", new { id = transaction.CourseId })
            ?? $"/Courses/Course/{transaction.CourseId}";
    }

    private static long ToPaise(decimal amount)
    {
        return checked((long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }
}

public sealed class RazorpayVerificationRequest
{
    public string MerchantOrderId { get; init; } = string.Empty;
    public string RazorpayOrderId { get; init; } = string.Empty;
    public string RazorpayPaymentId { get; init; } = string.Empty;
    public string RazorpaySignature { get; init; } = string.Empty;
}
