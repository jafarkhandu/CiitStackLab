using System.Text;
using CIITStackLab.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace CIITStackLab.Infrastructure.Services;

public sealed class EmailService : IEmailService
{
    private const string PrimaryBlue = "#1769ff";
    private const string DarkText = "#172033";
    private const string MutedText = "#64748b";

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
        var html = BuildEmailTemplate(
            "Welcome to CIIT Stack Lab",
            "Your learning journey starts here.",
            $"""
            <p style="margin:0 0 18px;">Hello <strong>{safeUserName}</strong>,</p>
            <p style="margin:0 0 18px;">Your CIIT Stack Lab account has been created successfully.</p>
            <div style="margin:24px 0;padding:18px 20px;border:1px solid #dbeafe;border-radius:16px;background:#f8fbff;">
                <div style="font-size:13px;font-weight:700;color:{PrimaryBlue};text-transform:uppercase;letter-spacing:.08em;">You are all set</div>
                <div style="margin-top:7px;color:{DarkText};font-size:15px;line-height:1.6;">Sign in to explore courses, learning resources, and your student dashboard.</div>
            </div>
            """);

        return SendEmailAsync(
            toEmail,
            "Welcome to CIIT Stack Lab",
            "Your CIIT Stack Lab account has been created successfully. You can now sign in and start learning.",
            html,
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
        var formattedAmount = $"₹{amount:0.00}";
        var formattedDate = purchasedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
        var invoiceHtml = BuildInvoiceHtml(
            safeUserName,
            safeCourseTitle,
            formattedAmount,
            purchaseId,
            formattedDate);
        var html = BuildEmailTemplate(
            "Purchase Confirmed",
            "Thank you for learning with CIIT Stack Lab.",
            $"""
            <p style="margin:0 0 18px;">Hello <strong>{safeUserName}</strong>,</p>
            <p style="margin:0 0 20px;">Your course purchase has been recorded successfully.</p>
            <div style="margin:20px 0;border:1px solid #dbeafe;border-radius:16px;overflow:hidden;background:#f8fbff;">
                <div style="padding:14px 18px;background:linear-gradient(135deg,#1769ff,#06b6d4);color:#fff;font-weight:700;">Purchase Summary</div>
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;">
                    <tr><td style="padding:13px 18px;color:{MutedText};border-bottom:1px solid #e5edf7;">Purchase ID</td><td align="right" style="padding:13px 18px;color:{DarkText};font-weight:700;border-bottom:1px solid #e5edf7;">{purchaseId}</td></tr>
                    <tr><td style="padding:13px 18px;color:{MutedText};border-bottom:1px solid #e5edf7;">Course</td><td align="right" style="padding:13px 18px;color:{DarkText};font-weight:700;border-bottom:1px solid #e5edf7;">{safeCourseTitle}</td></tr>
                    <tr><td style="padding:13px 18px;color:{MutedText};border-bottom:1px solid #e5edf7;">Purchased</td><td align="right" style="padding:13px 18px;color:{DarkText};font-weight:700;border-bottom:1px solid #e5edf7;">{formattedDate}</td></tr>
                    <tr><td style="padding:15px 18px;color:{DarkText};font-weight:800;">Total</td><td align="right" style="padding:15px 18px;color:{PrimaryBlue};font-size:18px;font-weight:800;">{formattedAmount}</td></tr>
                </table>
            </div>
            <div style="margin:20px 0;padding:16px 18px;border:1px dashed #b7c9e8;border-radius:14px;background:#fff;">
                <strong style="color:{DarkText};">Invoice attached</strong>
                <div style="margin-top:5px;color:{MutedText};font-size:13px;line-height:1.6;">Download the attached invoice file for your records.</div>
            </div>
            """);

        return SendEmailAsync(
            toEmail,
            "CIIT Stack Lab Purchase Confirmation",
            $"Purchase confirmed. Course: {courseTitle}. Amount: {formattedAmount}. Purchase ID: {purchaseId}.",
            html,
            cancellationToken,
            Encoding.UTF8.GetBytes(invoiceHtml),
            $"CIIT-Stack-Lab-Invoice-{purchaseId}.html");
    }

    public Task SendPasswordResetOtpAsync(
        string toEmail,
        string otp,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var formattedExpiry = expiresAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
        var safeOtp = HtmlEncode(otp);
        var html = BuildEmailTemplate(
            "Reset your password",
            "Secure access to your CIIT Stack Lab account.",
            $"""
            <p style="margin:0 0 18px;">We received a request to reset your password.</p>
            <div style="margin:24px 0;padding:22px;text-align:center;border:1px solid #dbeafe;border-radius:18px;background:linear-gradient(135deg,#f4f8ff,#f0fdff);">
                <div style="font-size:12px;font-weight:800;color:{MutedText};text-transform:uppercase;letter-spacing:.12em;">One-time password</div>
                <div style="margin-top:10px;font-size:32px;font-weight:800;letter-spacing:9px;color:{PrimaryBlue};">{safeOtp}</div>
                <div style="margin-top:9px;font-size:13px;color:{MutedText};">Expires {formattedExpiry}</div>
            </div>
            <p style="margin:0 0 10px;color:{MutedText};font-size:13px;line-height:1.6;">For your security, never share this OTP with anyone.</p>
            <p style="margin:0;color:{MutedText};font-size:13px;line-height:1.6;">If you did not request a password reset, you can safely ignore this email.</p>
            """);

        return SendEmailAsync(
            toEmail,
            "CIIT Stack Lab Password Reset OTP",
            $"Your CIIT Stack Lab password reset OTP is {otp}. It expires at {formattedExpiry}.",
            html,
            cancellationToken);
    }

    private async Task SendEmailAsync(
        string toEmail,
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken cancellationToken,
        byte[]? attachmentBytes = null,
        string? attachmentFileName = null)
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

        var bodyBuilder = new BodyBuilder
        {
            TextBody = textBody,
            HtmlBody = htmlBody
        };

        if (attachmentBytes is not null && !string.IsNullOrWhiteSpace(attachmentFileName))
        {
            bodyBuilder.Attachments.Add(attachmentFileName, attachmentBytes);
        }

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, secureSocketOptions, cancellationToken);
        await client.AuthenticateAsync(email, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static string BuildEmailTemplate(
        string title,
        string subtitle,
        string content)
    {
        return $"""
        <!doctype html>
        <html>
        <body style="margin:0;padding:0;background:#eef6ff;font-family:Arial,Helvetica,sans-serif;color:{DarkText};">
            <div style="padding:28px 12px;">
                <div style="max-width:640px;margin:0 auto;border:1px solid rgba(255,255,255,.8);border-radius:24px;background:rgba(255,255,255,.94);box-shadow:0 18px 45px rgba(23,105,255,.14);overflow:hidden;">
                    <div style="padding:26px 28px;background:linear-gradient(135deg,#1769ff 0%,#06b6d4 100%);color:#fff;">
                        <div style="display:inline-block;padding:7px 10px;border:1px solid rgba(255,255,255,.45);border-radius:10px;background:rgba(255,255,255,.15);font-size:20px;font-weight:800;">CIIT</div>
                        <div style="margin-top:9px;font-size:12px;font-weight:700;letter-spacing:.14em;opacity:.9;">STACK LAB</div>
                        <div style="margin-top:24px;font-size:28px;font-weight:800;line-height:1.2;">{HtmlEncode(title)}</div>
                        <div style="margin-top:7px;font-size:14px;line-height:1.5;opacity:.9;">{HtmlEncode(subtitle)}</div>
                    </div>
                    <div style="padding:30px 28px;background:rgba(255,255,255,.96);font-size:15px;line-height:1.65;">{content}</div>
                    <div style="padding:18px 28px;background:#f8fafc;border-top:1px solid #e5edf7;color:{MutedText};font-size:12px;line-height:1.6;">This is an automated message from CIIT Stack Lab.<br />© {DateTime.UtcNow.Year} CIIT Stack Lab</div>
                </div>
            </div>
        </body>
        </html>
        """;
    }

    private static string BuildInvoiceHtml(
        string userName,
        string courseTitle,
        string amount,
        int purchaseId,
        string purchasedAt)
    {
        return $"""
        <!doctype html>
        <html>
        <head><meta charset="utf-8"><title>CIIT Stack Lab Invoice</title></head>
        <body style="margin:0;padding:32px;background:#eef6ff;font-family:Arial,Helvetica,sans-serif;color:{DarkText};">
            <div style="max-width:720px;margin:auto;background:#fff;border:1px solid #dbeafe;border-radius:22px;overflow:hidden;box-shadow:0 18px 45px rgba(23,105,255,.12);">
                <div style="padding:28px;background:linear-gradient(135deg,#1769ff,#06b6d4);color:#fff;">
                    <div style="display:inline-block;padding:7px 10px;border:1px solid rgba(255,255,255,.45);border-radius:10px;background:rgba(255,255,255,.15);font-size:20px;font-weight:800;">CIIT</div>
                    <div style="margin-top:8px;font-size:12px;font-weight:700;letter-spacing:.14em;">STACK LAB</div>
                    <h1 style="margin:22px 0 0;font-size:30px;">Purchase Invoice</h1>
                </div>
                <div style="padding:30px;">
                    <p style="margin-top:0;">Issued to <strong>{userName}</strong></p>
                    <table width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;margin-top:22px;">
                        <tr><td style="padding:14px;border-bottom:1px solid #e5edf7;color:{MutedText};">Purchase ID</td><td style="padding:14px;border-bottom:1px solid #e5edf7;text-align:right;font-weight:700;">{purchaseId}</td></tr>
                        <tr><td style="padding:14px;border-bottom:1px solid #e5edf7;color:{MutedText};">Course</td><td style="padding:14px;border-bottom:1px solid #e5edf7;text-align:right;font-weight:700;">{courseTitle}</td></tr>
                        <tr><td style="padding:14px;border-bottom:1px solid #e5edf7;color:{MutedText};">Purchase date</td><td style="padding:14px;border-bottom:1px solid #e5edf7;text-align:right;font-weight:700;">{purchasedAt}</td></tr>
                        <tr><td style="padding:16px 14px;font-size:18px;font-weight:800;">Total paid</td><td style="padding:16px 14px;text-align:right;font-size:20px;font-weight:800;color:{PrimaryBlue};">{amount}</td></tr>
                    </table>
                    <p style="margin:26px 0 0;color:{MutedText};font-size:13px;">Thank you for choosing CIIT Stack Lab. Please keep this invoice for your records.</p>
                </div>
            </div>
        </body>
        </html>
        """;
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
