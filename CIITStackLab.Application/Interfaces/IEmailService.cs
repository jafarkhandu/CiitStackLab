namespace CIITStackLab.Application.Interfaces;

public interface IEmailService
{
    Task SendRegistrationEmailAsync(
        string toEmail,
        string userName,
        CancellationToken cancellationToken = default);

    Task SendPurchaseInvoiceEmailAsync(
        string toEmail,
        string userName,
        string courseTitle,
        decimal amount,
        int purchaseId,
        DateTime purchasedAt,
        CancellationToken cancellationToken = default);

    Task SendExamLifecycleEmailAsync(
        string toEmail,
        string userName,
        string courseTitle,
        string topicTitle,
        int attemptNumber,
        DateTime scheduledAtUtc,
        string eventType,
        decimal? paymentAmount,
        string paymentStatus,
        string? merchantOrderId,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetOtpAsync(
        string toEmail,
        string otp,
        DateTime expiresAt,
        CancellationToken cancellationToken = default);
}
