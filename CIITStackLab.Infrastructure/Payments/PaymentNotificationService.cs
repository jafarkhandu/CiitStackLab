using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace CIITStackLab.Infrastructure.Payments;

public sealed class PaymentNotificationService
{
    private readonly IConfiguration _configuration;

    public PaymentNotificationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendPaymentFailureEmailAsync(
        string toEmail,
        string userName,
        string courseTitle,
        decimal amount,
        string merchantOrderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return;
        }

        var senderEmail = GetRequiredSetting("EmailSettings:EmailId");
        var password = GetRequiredSetting("EmailSettings:Password");
        var host = GetRequiredSetting("EmailSettings:Host");
        var portValue = GetRequiredSetting("EmailSettings:Port");

        if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException("EmailSettings:Port must be a valid TCP port.");
        }

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
        message.Subject = "CIIT Stack Lab Payment Update";

        var body = new BodyBuilder
        {
            TextBody = $"Hello {userName}, your payment for {courseTitle} ({formattedAmount}) was not completed. Merchant order: {merchantOrderId}. No course access was granted. If money was debited, please wait for the gateway/bank reversal or contact support.",
            HtmlBody = $"""
            <!doctype html>
            <html>
            <body style="margin:0;padding:24px;background:#f8fafc;font-family:Arial,Helvetica,sans-serif;color:#172033;">
                <div style="max-width:620px;margin:0 auto;background:#fff;border:1px solid #e2e8f0;border-radius:18px;overflow:hidden;">
                    <div style="padding:22px 24px;background:#dc3545;color:#fff;">
                        <div style="font-size:20px;font-weight:800;">CIIT Stack Lab</div>
                        <div style="margin-top:5px;font-size:13px;opacity:.92;">Payment status update</div>
                    </div>
                    <div style="padding:26px 24px;line-height:1.6;">
                        <p>Hello <strong>{safeUserName}</strong>,</p>
                        <p>Your payment could not be completed. <strong>No course access was granted.</strong></p>
                        <div style="margin:20px 0;padding:16px;border:1px solid #fecaca;border-radius:12px;background:#fff7f7;">
                            <div><strong>Course:</strong> {safeCourseTitle}</div>
                            <div><strong>Amount:</strong> {formattedAmount}</div>
                            <div><strong>Order:</strong> {safeOrderId}</div>
                        </div>
                        <p>If your bank or payment provider shows a debit despite the failed status, please allow the provider's reversal process to complete before retrying. Contact support if the amount is not reversed.</p>
                    </div>
                </div>
            </body>
            </html>
            """
        };

        message.Body = body.ToMessageBody();

        // Payment failure notifications are a durable side effect. Do not cancel SMTP delivery
        // merely because the browser verification request was closed or timed out.
        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, secureSocketOptions, CancellationToken.None);
        await client.AuthenticateAsync(senderEmail, password, CancellationToken.None);
        await client.SendAsync(message, CancellationToken.None);
        await client.DisconnectAsync(true, CancellationToken.None);
    }

    private string GetRequiredSetting(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value == "********")
        {
            throw new InvalidOperationException($"{key} is not configured.");
        }

        return value;
    }
}
