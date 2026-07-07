namespace MngDocument.Application.Exceptions;

/// <summary>
/// Document Intelligence iş kuralı hatası — <c>code</c> + lokalize mesaj + HTTP statü.
/// API katmanında <c>DocumentExceptionFilter</c> tarafından response'a çevrilir.
/// </summary>
public class DocumentException : Exception
{
    public DocumentException(string code, string message, string? messageTr = null, int statusCode = 400)
        : base(message)
    {
        Code = code;
        MessageTr = messageTr;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public string? MessageTr { get; }
    public int StatusCode { get; }

    public static DocumentException NotFound(string messageTr = "Kaynak bulunamadı.") =>
        new("RESOURCE_NOT_FOUND", "Resource not found.", messageTr, 404);

    public static DocumentException Validation(string code, string message, string messageTr) =>
        new(code, message, messageTr, 400);

    public static DocumentException Conflict(string code, string message, string messageTr) =>
        new(code, message, messageTr, 409);

    public static DocumentException Forbidden(
        string messageTr = "Bu işlem için yetkiniz yok.",
        string message = "You do not have permission for this action.") =>
        new("FORBIDDEN", message, messageTr, 403);

    public static DocumentException ServiceUnavailable(string code, string message, string messageTr) =>
        new(code, message, messageTr, 503);
}
