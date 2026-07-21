namespace MngLLM.Application.Services;

public interface IPdfTextExtractor
{
    /// <summary>Extract plain text from a PDF. Returns empty if no text layer.</summary>
    string ExtractText(byte[] pdfBytes);
}
