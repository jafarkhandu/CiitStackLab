using CIITStackLab.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace CIITStackLab.Infrastructure.Services;

public sealed class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task SendRegistrationEmailAsync(
        string toEmail,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var safeUserName = HtmlEncode(userName);

        return SendEmailAsync(
            toEmail,
            "Welcome to CIIT Stack Lab",
            $"""
            <h2>Welcome to CIIT Stack Lab</h2>
            <p>Hello {safeUserName},</p>
            <p>Your CIIT Stack Lab account has been created successfully.</p>
            <p>You can now sign in and start exploring your courses and learning resources.</p>
            <p>Regards,<br />CIIT Stack Lab</p>
            """,
            cancellationToken);
    }

    public Task SendPurchaseInvoiceEmailAsync(
        string toEmail,
        string userName,
        string courseTitle,
        decimal amount,
        int purchaseId,
        DateTime purchasedAt,
        CancellationToken cancellationToken = default)
    {
        var safeUserName = HtmlEncode(userName);
        var safeCourseTitle = HtmlEncode(courseTitle);
        var formattedAmount = amount.ToString("C", System.Globalization.CultureInfo.InvariantCulture);
        var formattedDate = purchasedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");

        return SendEmailAsync(
            toEmail,
            "CIIT Stack Lab Purchase Confirmation",
            $"""
            <h2>Purchase Confirmed</h2>
            <p>Hello {safeUserName},</p>
            <p>Your course purchase has been recorded successfully.</p>
            <table cellpadding="8" cellspacing="0" border="1" style="border-collapse:collapse;">
                <tr><td><strong>Purchase ID</strong></td><td>{purchaseId}</td></tr>
                <tr><td><strong>Course</strong></td><td>{safeCourseTitle}</td></tr>
                <tr><td><strong>Amount</strong></td><td>{formattedAmount}</td></tr>
                <tr><td><strong>Purchased</strong></td><td>{formattedDate}</td></tr>
            </table>
            <p>Keep this email for your purchase reference.</p>
            <p>Regards,<br />CIIT Stack Lab</p>
            """,
            cancellationToken);
    }

    public Task SendPasswordResetOtpAsync(
        string toEmail,
        string otp,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var formattedExpiry = expiresAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");

        return SendEmailAsync(
            toEmail,
            "CIIT Stack Lab Password Reset OTP",
            $"""
            <h2>Password Reset Request</h2>
            <p>Use the following one-time password to reset your CIIT Stack Lab password:</p>
            <p style="font-size:28px;font-weight:bold;letter-spacing:6px;">{HtmlEncode(otp)}</p>
            <p>This OTP expires at <strong>{formattedExpiry}</strong>.</p>
            <p>If you did not request a password reset, you can safely ignore this email.</p>
            <p>Regards,<br />CIIT Stack Lab</p>
            """,
            cancellationToken);
    }

    private async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException("Recipient email is required.", nameof(toEmail));
        }

        var email = GetRequiredSetting("EmailSettings:EmailId");
        var password = GetRequiredSetting("EmailSettings:Password");
        var host = GetRequiredSetting("EmailSettings:Host");
        var portValue = GetRequiredSetting("EmailSettings:Port");

        if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException("EmailSettings:Port must be a valid TCP port.");
        }

        var senderName = _configuration["EmailSettings:Name"] ?? string.Empty;
        var useSsl = bool.TryParse(
            _configuration["EmailSettings:UseSSL"],
            out var configuredUseSsl)
            ? configuredUseSsl
            : port == 465;

        var secureSocketOptions = useSsl
            ? port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, email));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();

        await client.ConnectAsync(host, port, secureSocketOptions, cancellationToken);
        await client.AuthenticateAsync(email, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private string GetRequiredSetting(string key)
    {
        var value = _configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is not configured.");
        }

        return value;
    }

    private static string HtmlEncode(string value)
    {
        return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
