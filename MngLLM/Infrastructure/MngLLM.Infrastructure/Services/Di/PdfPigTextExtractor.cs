using System.Text;
using MngLLM.Application.Services;
using MngLLM.Domain.Exceptions;
using UglyToad.PdfPig;

namespace MngLLM.Infrastructure.Services.Di;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public string ExtractText(byte[] pdfBytes)
    {
        if (pdfBytes is null || pdfBytes.Length == 0)
            throw new DiExtractException("PDF content is empty.", 422);

        try
        {
            using var stream = new MemoryStream(pdfBytes);
            using var document = PdfDocument.Open(stream);
            var sb = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                var pageText = page.Text;
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.AppendLine(pageText);
                }
            }

            return sb.ToString().Trim();
        }
        catch (DiExtractException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DiExtractException("Failed to read PDF content.", 422, ex);
        }
    }
}
