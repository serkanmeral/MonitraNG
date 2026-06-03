using System.Threading.Tasks;
using MngReactor.Application.Abstractions.Crypt;
using MngReactor.Persistence.Services.Crypt;
using MngReactor.Persistence.Settings;
using Microsoft.Extensions.Options;
using Xunit;

namespace MngReactor.Tests.Services.Crypt;

public class CryptProcessingTests
{
    private static ICryptProcessing CreateSut()
    {
        var settings = new MngReactorSettings
        {
            CompressPrk = "0123456789abcdef", // 16 bytes AES key
            CompressPbk = "abcdef0123456789"  // 16 bytes IV
        };
        var options = Options.Create(settings);
        return new CryptProcessing(options);
    }

    [Fact]
    public async Task Compress_DeCompress_RoundTrip_ReturnsSameText()
    {
        var sut = CreateSut();
        var original = "Test metin - config sync payload";

        var compressed = await sut.Compress(original);
        Assert.NotNull(compressed);
        Assert.NotEmpty(compressed);

        var decompressed = await sut.DeCompress(compressed);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public async Task Compress_EmptyString_ReturnsCompressedBytes()
    {
        var sut = CreateSut();
        var compressed = await sut.Compress("");
        Assert.NotNull(compressed);
        Assert.NotEmpty(compressed);

        var decompressed = await sut.DeCompress(compressed);
        Assert.Equal("", decompressed);
    }

    [Fact]
    public async Task Compress_LongText_RoundTrip_Succeeds()
    {
        var sut = CreateSut();
        var original = new string('x', 10000);

        var compressed = await sut.Compress(original);
        var decompressed = await sut.DeCompress(compressed);
        Assert.Equal(original, decompressed);
    }
}
