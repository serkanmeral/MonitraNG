using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;
using MngNotifier.Application.DTOs;
using MngNotifier.Application.Services;

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
            using var client = new SmtpClient(_mailSettings.Smtp.Host, _mailSettings.Smtp.Port);
            client.EnableSsl = _mailSettings.Smtp.EnableSsl;

            // Authentication (if username/password provided)
            if (!string.IsNullOrWhiteSpace(_mailSettings.Smtp.Username) && !string.IsNullOrWhiteSpace(_mailSettings.Smtp.Password))
            {
                client.Credentials = new NetworkCredential(_mailSettings.Smtp.Username, _mailSettings.Smtp.Password);
            }

            // Determine "from" address
            var fromEmail = request.From?.Email ?? _mailSettings.DefaultFrom.Email;
            var fromName = request.From?.Name ?? _mailSettings.DefaultFrom.Name;
            var fromAddress = string.IsNullOrWhiteSpace(fromName) 
                ? new MailAddress(fromEmail) 
                : new MailAddress(fromEmail, fromName);

            // Create mail message
            using var message = new MailMessage
            {
                From = fromAddress,
                Subject = request.Subject,
                Body = request.Body,
                IsBodyHtml = request.IsHtml
            };

            // Add recipients
            foreach (var to in request.To)
            {
                message.To.Add(new MailAddress(to));
            }

            if (request.Cc != null)
            {
                foreach (var cc in request.Cc)
                {
                    message.CC.Add(new MailAddress(cc));
                }
            }

            // Send mail
            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Mail sent successfully. To: {To}, Subject: {Subject}", string.Join(", ", request.To), request.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send mail. To: {To}, Subject: {Subject}", string.Join(", ", request.To), request.Subject);
            throw;
        }
    }
}
