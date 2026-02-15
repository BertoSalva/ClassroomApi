using MailKit.Security;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;
using System.Net;

namespace Classroom.Infrastructure.Email;

public sealed class EmailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromName { get; set; } = "Classroom API";
    public string FromAddress { get; set; } = "no-reply@example.com";

    // Template must contain two placeholders: {0} = encodedToken, {1} = encodedEmail
    // Example: "https://app.example.com/reset-password?token={0}&email={1}"
    public string ResetPasswordUrlTemplate { get; set; } = "";
}

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string toEmail, string token, CancellationToken cancellationToken = default);
}

public sealed class EmailService : IEmailService
{
    private readonly EmailOptions _opts;

    public EmailService(IOptions<EmailOptions> opts)
    {
        _opts = opts.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opts.FromName, _opts.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        SecureSocketOptions socket = _opts.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;

        await client.ConnectAsync(_opts.Host, _opts.Port, socket, cancellationToken);
        if (!string.IsNullOrWhiteSpace(_opts.User))
        {
            await client.AuthenticateAsync(_opts.User, _opts.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task SendPasswordResetAsync(string toEmail, string token, CancellationToken cancellationToken = default)
    {
        // Ensure template exists
        if (string.IsNullOrWhiteSpace(_opts.ResetPasswordUrlTemplate))
            throw new InvalidOperationException("ResetPasswordUrlTemplate is not configured in Email options.");

        var encodedToken = WebUtility.UrlEncode(token);
        var encodedEmail = WebUtility.UrlEncode(toEmail);
        var resetUrl = string.Format(_opts.ResetPasswordUrlTemplate, encodedToken, encodedEmail);

        var html = $@"
            <p>Hello,</p>
            <p>We received a request to reset your password. Click the link below to reset it:</p>
            <p><a href=""{resetUrl}"">{resetUrl}</a></p>
            <p>If you did not request a password reset, you can ignore this email.</p>";

        await SendAsync(toEmail, "Reset your password", html, cancellationToken);
    }
}