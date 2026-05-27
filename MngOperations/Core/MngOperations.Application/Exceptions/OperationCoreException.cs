namespace MngOperations.Application.Exceptions;

public class OperationCoreException : Exception
{
    public OperationCoreException(string code, string message, string? messageTr = null, int statusCode = 400)
        : base(message)
    {
        Code = code;
        MessageTr = messageTr;
        StatusCode = statusCode;
    }

    public OperationCoreException(
        string code,
        string message,
        string? messageTr,
        int statusCode,
        IReadOnlyDictionary<string, object?>? details)
        : this(code, message, messageTr, statusCode)
    {
        Details = details;
    }

    public string Code { get; }
    public string? MessageTr { get; }
    public int StatusCode { get; }
    public IReadOnlyDictionary<string, object?>? Details { get; }
}
