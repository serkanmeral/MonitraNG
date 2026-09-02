using MngDocument.Application.Contracts.Dlp;

namespace MngDocument.Application.Interfaces;

/// <summary>Writes / reads origin classification on DOCX, XLSX, PPTX, PDF bytes.</summary>
public interface IClassificationStampService
{
    byte[] Apply(byte[] content, string? extensionOrFileName, ClassificationStamp stamp);

    ClassificationStamp? TryRead(byte[] content, string? extensionOrFileName);
}
