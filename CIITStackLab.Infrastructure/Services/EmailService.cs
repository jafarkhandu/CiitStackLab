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

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException("Recipient email is required.", nameof(toEmail));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Email subject is required.", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(htmlBody))
        {
            throw new ArgumentException("Email body is required.", nameof(htmlBody));
        }

        var email = GetRequiredSetting("EmailSettings:EmailId");
        var password = GetRequiredSetting("EmailSettings:Password");
        var host = GetRequiredSetting("EmailSettings:Host");
        var portValue = GetRequiredSetting("EmailSettings:Port");

        if (!int.TryParse(portValue, out var port) || port <= 0 || port > 65535)
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
        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody
        }.ToMessageBody();

        using var client = new SmtpClient();

        await client.ConnectAsync(
            host,
            port,
            secureSocketOptions,
            cancellationToken);

        await client.AuthenticateAsync(
            email,
            password,
            cancellationToken);

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
}
