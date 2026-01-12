namespace MngNotifier.Application.DTOs;

public class SendMailRequest
{
    public List<string> To { get; set; } = new();
    public List<string>? Cc { get; set; }
    public MailAddressDto? From { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsHtml { get; set; } = true;
}

public class MailAddressDto
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
}
