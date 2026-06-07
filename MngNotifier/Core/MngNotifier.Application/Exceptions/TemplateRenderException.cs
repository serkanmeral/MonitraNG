namespace MngNotifier.Application.Exceptions;

public class TemplateRenderException : Exception
{
    public int StatusCode { get; }

    public TemplateRenderException(string message, int statusCode = 400)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
