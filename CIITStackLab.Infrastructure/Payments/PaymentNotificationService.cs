using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace CIITStackLab.Infrastructure.Payments;

public sealed class PaymentNotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentNotificationService> _logger;

    public PaymentNotificationService(IConfiguration configuration, ILogger<PaymentNotificationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendPaymentFailureEmailAsync(string toEmail, string userName, string courseTitle, decimal amount, string merchantOrderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new InvalidOperationException("The student does not have a registered email address.");

        var senderEmail = GetRequiredSetting("EmailSettings:EmailId");
        var password = GetRequiredSetting("EmailSettings:Password");
        var host = GetRequiredSetting("EmailSettings:Host");
        var portValue = GetRequiredSetting("EmailSettings:Port");

        if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
            throw new InvalidOperationException("EmailSettings:Port must be a valid TCP port.");

        var senderName = _configuration["EmailSettings:Name"] ?? string.Empty;
        var useSsl = bool.TryParse(_configuration["EmailSettings:UseSSL"], out var configuredUseSsl)
            ? configuredUseSsl
            : port == 465;
        var secureSocketOptions = useSsl
            ? port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        var safeUserName = System.Net.WebUtility.HtmlEncode(userName);
        var safeCourseTitle = System.Net.WebUtility.HtmlEncode(courseTitle);
        var safeOrderId = System.Net.WebUtility.HtmlEncode(merchantOrderId);
        var formattedAmount = $"INR {amount:0.00}";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Payment failed — CIIT Stack Lab";
        message.Body = new BodyBuilder
        {
            TextBody = $"Hello {userName}, your payment for {courseTitle} ({formattedAmount}) was not completed. No course access was granted. Merchant order: {merchantOrderId}. If money was debited, please wait for the gateway/bank reversal or contact support.",
            HtmlBody = $"""
            <!doctype html>
            <html><body style="margin:0;padding:32px 14px;background:#eef4ff;font-family:Arial,Helvetica,sans-serif;color:#172033;">
            <div style="max-width:720px;margin:0 auto;background:#fff;border:1px solid #dce6f4;border-radius:24px;overflow:hidden;box-shadow:0 18px 50px rgba(23,105,255,.14);">
                <div style="padding:32px 34px;background:linear-gradient(135deg,#dc3545,#ef4444 58%,#fb7185);color:#fff;">
                    <div style="font-size:24px;font-weight:800;">CIIT Stack Lab</div>
                    <div style="margin-top:7px;font-size:13px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;opacity:.92;">Payment status update</div>
                    <div style="margin-top:28px;font-size:32px;font-weight:800;">Payment Failed</div>
                    <div style="margin-top:9px;font-size:15px;line-height:1.6;">Your payment was not completed and your course access remains protected.</div>
                </div>
                <div style="padding:34px;font-size:16px;line-height:1.7;">
                    <p style="margin:0 0 18px;">Hello <strong>{safeUserName}</strong>,</p>
                    <p style="margin:0 0 24px;">We could not complete the payment for your course. <strong>No course access was granted.</strong></p>
                    <div style="margin:26px 0;padding:22px 24px;border:1px solid #fecaca;border-radius:18px;background:#fff7f7;">
                        <div style="font-size:12px;font-weight:800;letter-spacing:.1em;text-transform:uppercase;color:#b91c1c;margin-bottom:14px;">Payment details</div>
                        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;font-size:15px;">
                            <tr><td style="padding:9px 0;color:#64748b;">Course</td><td align="right" style="padding:9px 0;color:#172033;font-weight:700;">{safeCourseTitle}</td></tr>
                            <tr><td style="padding:9px 0;color:#64748b;">Amount</td><td align="right" style="padding:9px 0;color:#172033;font-weight:700;">{formattedAmount}</td></tr>
                            <tr><td style="padding:9px 0;color:#64748b;">Order ID</td><td align="right" style="padding:9px 0;color:#172033;font-weight:700;word-break:break-all;">{safeOrderId}</td></tr>
                        </table>
                    </div>
                    <div style="margin:26px 0;padding:20px 22px;border:1px solid #dbeafe;border-radius:16px;background:#f8fbff;"><strong>If money was debited</strong><div style="margin-top:7px;color:#64748b;font-size:14px;line-height:1.7;">Please allow the payment provider or bank reversal process to complete before trying again. If the amount is not reversed, contact support with the order ID above.</div></div>
                    <p style="margin:0;color:#64748b;font-size:14px;">You can return to CIIT Stack Lab and retry the payment whenever you are ready.</p>
                </div>
                <div style="padding:20px 34px;background:#f8fafc;border-top:1px solid #e5edf7;color:#64748b;font-size:12px;line-height:1.7;">This is an automated payment notification from CIIT Stack Lab.<br/>Please do not reply directly to this email.</div>
            </div></body></html>
            """
        }.ToMessageBody();

        const int maxAttempts = 3;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var client = new SmtpClient { Timeout = 15000 };
                _logger.LogInformation("Sending payment failure email for order {MerchantOrderId} to {Recipient} via {Host}:{Port}. Attempt {Attempt}/{MaxAttempts}.", merchantOrderId, toEmail, host, port, attempt, maxAttempts);
                await client.ConnectAsync(host, port, secureSocketOptions, CancellationToken.None);
                await client.AuthenticateAsync(senderEmail, password, CancellationToken.None);
                await client.SendAsync(message, CancellationToken.None);
                await client.DisconnectAsync(true, CancellationToken.None);
                _logger.LogInformation("Payment failure email sent successfully for order {MerchantOrderId}.", merchantOrderId);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Payment failure email attempt {Attempt}/{MaxAttempts} failed for order {MerchantOrderId}.", attempt, maxAttempts, merchantOrderId);
                await Task.Delay(TimeSpan.FromSeconds(attempt), CancellationToken.None);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(ex, "Payment failure email failed after {MaxAttempts} attempts for order {MerchantOrderId}.", maxAttempts, merchantOrderId);
            }
        }

        throw new InvalidOperationException("Payment failure email delivery failed after multiple attempts.", lastException);
    }

    private string GetRequiredSetting(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value == "********")
            throw new InvalidOperationException($"{key} is not configured.");
        return value;
    }
}
