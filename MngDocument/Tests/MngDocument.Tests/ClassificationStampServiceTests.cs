using MngDocument.Application.Contracts.Dlp;
using MngDocument.Infrastructure.Services;

namespace MngDocument.Tests;

public sealed class ClassificationStampServiceTests
{
    private readonly ClassificationStampService _sut = new();
    private static readonly ClassificationStamp Sample = new("cl-gizli", "gizli", 3, 1);

    [Fact]
    public void Docx_roundtrip_reads_custom_properties()
    {
        var original = MinimalDocxFactory.CreateBlank();
        var stamped = _sut.Apply(original, ".docx", Sample);
        var read = _sut.TryRead(stamped, ".docx");

        Assert.NotNull(read);
        Assert.Equal(Sample.ClassificationId, read!.ClassificationId);
        Assert.Equal(Sample.ClassificationName, read.ClassificationName);
        Assert.Equal(Sample.Sensitivity, read.Sensitivity);
        Assert.Equal(1, read.SchemaVersion);
    }

    [Fact]
    public void Pdf_roundtrip_reads_keywords_comment()
    {
        var original = "%PDF-1.1\n1 0 obj<<>>endobj\ntrailer<<>>\nstartxref\n0\n%%EOF\n"u8.ToArray();
        var stamped = _sut.Apply(original, ".pdf", Sample);
        var read = _sut.TryRead(stamped, ".pdf");

        Assert.NotNull(read);
        Assert.Equal("cl-gizli", read!.ClassificationId);
        Assert.Equal("gizli", read.ClassificationName);
        Assert.Equal(3, read.Sensitivity);
    }
}
