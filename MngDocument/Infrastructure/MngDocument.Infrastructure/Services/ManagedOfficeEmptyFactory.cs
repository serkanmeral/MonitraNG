using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

/// <summary>Native managed office boş dosya üretimi.</summary>
public static class ManagedOfficeEmptyFactory
{
    public static byte[] CreateBlank(ManagedOfficeKind kind) => kind switch
    {
        ManagedOfficeKind.Document => MinimalDocxFactory.CreateBlank(),
        ManagedOfficeKind.Sheet => MinimalXlsxFactory.CreateBlank(),
        ManagedOfficeKind.Presentation => MinimalPptxFactory.CreateBlank(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
