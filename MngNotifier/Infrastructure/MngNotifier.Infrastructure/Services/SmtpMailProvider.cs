using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MngNotifier.Application.Configuration;
using MngNotifier.Application.DTOs;
using MngNotifier.Application.Services;
using MimeKit;

namespace MngNotifier.Infrastructure.Services;

public class SmtpMailProvider : IMailProvider
{
    private readonly IDomainMailSettingsResolver _mailSettingsResolver;
    private readonly IMailDeliveryLog _mailDeliveryLog;
    private readonly ILogger<SmtpMailProvider> _logger;

    public SmtpMailProvider(
        IDomainMailSettingsResolver mailSettingsResolver,
        IMailDeliveryLog mailDeliveryLog,
        ILogger<SmtpMailProvider> logger)
    {
        _mailSettingsResolver = mailSettingsResolver;
        _mailDeliveryLog = mailDeliveryLog;
        _logger = logger;
    }

    public async Task SendMailAsync(SendMailRequest request, CancellationToken cancellationToken = default)
    {
        var mailSettings = await _mailSettingsResolver.ResolveAsync(request.DomainName, cancellationToken);

        try
        {
            var fromEmail = request.From?.Email ?? mailSettings.DefaultFrom.Email;
            var fromName = request.From?.Name ?? mailSettings.DefaultFrom.Name;

            var message = new MimeMessage();
            message.From.Add(string.IsNullOrWhiteSpace(fromName)
                ? MailboxAddress.Parse(fromEmail)
                : new MailboxAddress(fromName, fromEmail));
            message.Subject = request.Subject;
            message.Body = new TextPart(request.IsHtml ? "html" : "plain") { Text = request.Body };

            foreach (var to in request.To)
                message.To.Add(MailboxAddress.Parse(to));

            if (request.Cc != null)
            {
                foreach (var cc in request.Cc)
                    message.Cc.Add(MailboxAddress.Parse(cc));
            }

            using var client = new SmtpClient();
            client.Timeout = 30000;

            var socketOptions = ResolveSecureSocketOptions(mailSettings);
            await client.ConnectAsync(mailSettings.Smtp.Host, mailSettings.Smtp.Port, socketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(mailSettings.Smtp.Username)
                && !string.IsNullOrWhiteSpace(mailSettings.Smtp.Password))
            {
                await client.AuthenticateAsync(mailSettings.Smtp.Username, mailSettings.Smtp.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Mail sent successfully. To: {To}, Subject: {Subject}",
                string.Join(", ", request.To), request.Subject);
            await RecordAsync(request, true, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send mail. To: {To}, Subject: {Subject}",
                string.Join(", ", request.To), request.Subject);
            await RecordAsync(request, false, ex.Message, cancellationToken);
            throw;
        }
    }

    private Task RecordAsync(SendMailRequest request, bool success, string? error, CancellationToken cancellationToken)
    {
        return _mailDeliveryLog.RecordAsync(new MailDeliveryLogEntry
        {
            SentAt = DateTime.UtcNow,
            DomainName = request.DomainName,
            Subject = request.Subject ?? string.Empty,
            Recipients = request.To ?? new List<string>(),
            Cc = request.Cc ?? new List<string>(),
            Success = success,
            Error = error
        }, cancellationToken);
    }

    private static SecureSocketOptions ResolveSecureSocketOptions(MailSettings mailSettings)
    {
        var mode = (mailSettings.Smtp.SecureSocketMode ?? "Auto").Trim();

        return mode.ToLowerInvariant() switch
        {
            "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            "none" => SecureSocketOptions.None,
            _ => mailSettings.Smtp.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : mailSettings.Smtp.EnableSsl
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.None
        };
    }
}
