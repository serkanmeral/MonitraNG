namespace MngOperations.Application.Contracts.Notifications;

public sealed class SendMailRequest
{
    public List<string> To { get; init; } = new();
    public List<string>? Cc { get; init; }
    public MailAddressDto? From { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public bool IsHtml { get; init; } = true;
}

public sealed class MailAddressDto
{
    public required string Email { get; init; }
    public string? Name { get; init; }
}

public sealed class SendMailResult
{
    public bool Success { get; init; }
    public string? NotificationId { get; init; }
    public string? ErrorMessage { get; init; }
}
