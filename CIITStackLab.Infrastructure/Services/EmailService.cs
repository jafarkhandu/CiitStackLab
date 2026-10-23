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
        var formattedAmount = $"INR {amount:0.00}";
        var formattedDate = purchasedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
        var invoicePdf = BuildInvoicePdf(
            userName,
            courseTitle,
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
                <strong style="color:{DarkText};">PDF invoice attached</strong>
                <div style="margin-top:5px;color:{MutedText};font-size:13px;line-height:1.6;">Download the attached PDF invoice for your records.</div>
            </div>
            """);

        return SendEmailAsync(
            toEmail,
            "CIIT Stack Lab Purchase Confirmation",
            $"Purchase confirmed. Course: {courseTitle}. Amount: {formattedAmount}. Purchase ID: {purchaseId}. A PDF invoice is attached.",
            html,
            cancellationToken,
            invoicePdf,
            $"CIIT-Stack-Lab-Invoice-{purchaseId}.pdf",
            new ContentType("application", "pdf"));
    }

    public Task SendExamLifecycleEmailAsync(
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
        CancellationToken cancellationToken = default)
    {
        var isStarted = string.Equals(eventType, "Started", StringComparison.OrdinalIgnoreCase);
        var safeUserName = HtmlEncode(userName);
        var safeCourseTitle = HtmlEncode(courseTitle);
        var safeTopicTitle = HtmlEncode(topicTitle);
        var safePaymentStatus = HtmlEncode(paymentStatus);
        var safeOrderId = HtmlEncode(merchantOrderId ?? "Not applicable");
        var scheduledTime = DateTime.SpecifyKind(scheduledAtUtc, DateTimeKind.Utc)
            .ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
        var paymentText = paymentAmount.HasValue
            ? $"INR {paymentAmount.Value:0.00}"
            : "Free attempt";
        var safePaymentText = HtmlEncode(paymentText);
        var title = isStarted ? "Your examination has started" : "Examination scheduled successfully";
        var subtitle = isStarted
            ? "All the best with your CIIT Stack Lab examination."
            : "Your examination booking details are confirmed.";
        var html = BuildEmailTemplate(
            title,
            subtitle,
            $"""
            <p style="margin:0 0 18px;">Hello <strong>{safeUserName}</strong>,</p>
            <p style="margin:0 0 20px;">{(isStarted ? "Your examination is now in progress. Stay focused and do your best." : "Your examination has been scheduled. Please join at the date and time below.")}</p>
            <div style="margin:20px 0;border:1px solid #dbeafe;border-radius:16px;overflow:hidden;background:#f8fbff;">
                <div style="padding:14px 18px;background:linear-gradient(135deg,#1769ff,#06b6d4);color:#fff;font-weight:700;">Examination Details</div>
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;">
                    <tr><td style="padding:12px 18px;color:{MutedText};border-bottom:1px solid #e5edf7;">Course</td><td align="right" style="padding:12px 18px;color:{DarkText};font-weight:700;border-bottom:1px solid #e5edf7;">{safeCourseTitle}</td></tr>
                    <tr><td style="padding:12px 18px;color:{MutedText};border-bottom:1px solid #e5edf7;">Topic</td><td align="right" style="padding:12px 18px;color:{DarkText};font-weight:700;border-bottom:1px solid #e5edf7;">{safeTopicTitle}</td></tr>
                    <tr><td style="padding:12px 18px;color:{MutedText};border-bottom:1px solid #e5edf7;">Attempt</td><td align="right" style="padding:12px 18px;color:{DarkText};font-weight:700;border-bottom:1px solid #e5edf7;">#{attemptNumber}</td></tr>
                    <tr><td style="padding:12px 18px;color:{MutedText};border-bottom:1px solid #e5edf7;">Scheduled time</td><td align="right" style="padding:12px 18px;color:{DarkText};font-weight:700;border-bottom:1px solid #e5edf7;">{HtmlEncode(scheduledTime)}</td></tr>
                    <tr><td style="padding:12px 18px;color:{MutedText};border-bottom:1px solid #e5edf7;">Payment amount</td><td align="right" style="padding:12px 18px;color:{DarkText};font-weight:700;border-bottom:1px solid #e5edf7;">{safePaymentText}</td></tr>
                    <tr><td style="padding:12px 18px;color:{MutedText};border-bottom:1px solid #e5edf7;">Payment status</td><td align="right" style="padding:12px 18px;color:{DarkText};font-weight:700;border-bottom:1px solid #e5edf7;">{safePaymentStatus}</td></tr>
                    <tr><td style="padding:12px 18px;color:{MutedText};">Payment order</td><td align="right" style="padding:12px 18px;color:{DarkText};font-weight:700;">{safeOrderId}</td></tr>
                </table>
            </div>
            <div style="margin:20px 0;padding:18px;border-radius:14px;background:#eff6ff;color:{PrimaryBlue};font-size:18px;font-weight:800;text-align:center;">Best of luck! Give it your best.</div>
            """);

        var textBody = $"Hello {userName}. {title}. Course: {courseTitle}. Topic: {topicTitle}. Attempt: #{attemptNumber}. Scheduled: {scheduledTime}. Payment: {paymentText}; status: {paymentStatus}; order: {merchantOrderId ?? "Not applicable"}. Best of luck!";
        return SendEmailAsync(
            toEmail,
            $"CIIT Stack Lab - {title}",
            textBody,
            html,
            cancellationToken);
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
        string? attachmentFileName = null,
        ContentType? attachmentContentType = null)
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
            bodyBuilder.Attachments.Add(
                attachmentFileName,
                attachmentBytes,
                attachmentContentType ?? new ContentType("application", "octet-stream"));
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

    private static byte[] BuildInvoicePdf(
        string userName,
        string courseTitle,
        string amount,
        int purchaseId,
        string purchasedAt)
    {
        var content = new StringBuilder();
        content.AppendLine("q");
        content.AppendLine("0.09 0.41 1 rg");
        content.AppendLine("0 692 612 100 re f");
        content.AppendLine("Q");
        AppendPdfText(content, 40, 748, 24, "CIIT STACK LAB", true, "1 1 1");
        AppendPdfText(content, 40, 720, 11, "PURCHASE INVOICE", false, "1 1 1");
        AppendPdfText(content, 40, 650, 11, "Invoice / Purchase ID", false, "0.39 0.45 0.55");
        AppendPdfText(content, 430, 650, 11, purchaseId.ToString(), true, "0.09 0.41 1");
        AppendPdfText(content, 40, 612, 11, "Issued to", false, "0.39 0.45 0.55");
        AppendPdfText(content, 40, 588, 14, SafePdfText(userName), true, "0.09 0.13 0.20");
        AppendPdfText(content, 40, 540, 11, "Course", false, "0.39 0.45 0.55");
        AppendPdfText(content, 40, 516, 14, SafePdfText(courseTitle), true, "0.09 0.13 0.20");
        AppendPdfText(content, 40, 468, 11, "Purchase date", false, "0.39 0.45 0.55");
        AppendPdfText(content, 40, 444, 13, SafePdfText(purchasedAt), false, "0.09 0.13 0.20");
        content.AppendLine("0.86 0.90 0.96 RG");
        content.AppendLine("40 400 m 572 400 l S");
        AppendPdfText(content, 40, 360, 15, "Total paid", true, "0.09 0.13 0.20");
        AppendPdfText(content, 430, 360, 17, SafePdfText(amount), true, "0.09 0.41 1");
        AppendPdfText(content, 40, 300, 10, "Thank you for choosing CIIT Stack Lab. Please keep this invoice for your records.", false, "0.39 0.45 0.55");

        var contentBytes = Encoding.ASCII.GetBytes(content.ToString());
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
            $"<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream",
            "<< /Title (CIIT Stack Lab Purchase Invoice) /Producer (CIIT Stack Lab) >>"
        };

        using var stream = new MemoryStream();
        WriteAscii(stream, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };

        foreach (var pdfObject in objects)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{offsets.Count - 1} 0 obj\n{pdfObject}\nendobj\n");
        }

        var xrefOffset = stream.Position;
        WriteAscii(stream, $"xref\n0 {objects.Count + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++)
        {
            WriteAscii(stream, $"{offsets[index]:D10} 00000 n \n");
        }

        WriteAscii(stream, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info 7 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return stream.ToArray();
    }

    private static void AppendPdfText(
        StringBuilder builder,
        int x,
        int y,
        int fontSize,
        string text,
        bool bold,
        string rgb)
    {
        var font = bold ? "/F2" : "/F1";
        builder.AppendLine($"BT {rgb} rg {font} {fontSize} Tf {x} {y} Td ({EscapePdfText(text)}) Tj ET");
    }

    private static string SafePdfText(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value ?? string.Empty)
        {
            builder.Append(character is >= ' ' and <= '~' ? character : '?');
        }

        return builder.ToString();
    }

    private static string EscapePdfText(string value)
    {
        return SafePdfText(value)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
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
