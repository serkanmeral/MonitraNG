using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;
using MngNotifier.Application.DTOs;
using MngNotifier.Application.Services;
using MimeKit;

namespace MngNotifier.Infrastructure.Services;

public class SmtpMailProvider : IMailProvider
{
    private readonly MailSettings _mailSettings;
    private readonly ILogger<SmtpMailProvider> _logger;

    public SmtpMailProvider(IOptions<MngNotifierSettings> settings, ILogger<SmtpMailProvider> logger)
    {
        _mailSettings = settings.Value.Mail;
        _logger = logger;
    }

    public async Task SendMailAsync(SendMailRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var fromEmail = request.From?.Email ?? _mailSettings.DefaultFrom.Email;
            var fromName = request.From?.Name ?? _mailSettings.DefaultFrom.Name;

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

            var socketOptions = ResolveSecureSocketOptions();
            await client.ConnectAsync(_mailSettings.Smtp.Host, _mailSettings.Smtp.Port, socketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_mailSettings.Smtp.Username)
                && !string.IsNullOrWhiteSpace(_mailSettings.Smtp.Password))
            {
                await client.AuthenticateAsync(_mailSettings.Smtp.Username, _mailSettings.Smtp.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Mail sent successfully. To: {To}, Subject: {Subject}",
                string.Join(", ", request.To), request.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send mail. To: {To}, Subject: {Subject}",
                string.Join(", ", request.To), request.Subject);
            throw;
        }
    }

    private SecureSocketOptions ResolveSecureSocketOptions()
    {
        var mode = (_mailSettings.Smtp.SecureSocketMode ?? "Auto").Trim();

        return mode.ToLowerInvariant() switch
        {
            "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            "none" => SecureSocketOptions.None,
            _ => _mailSettings.Smtp.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : _mailSettings.Smtp.EnableSsl
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.None
        };
    }
}
