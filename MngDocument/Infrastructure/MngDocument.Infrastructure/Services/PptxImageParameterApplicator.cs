using MngDocument.Application.Contracts.Generation;

namespace MngDocument.Infrastructure.Services;

/// <summary>Resolves <c>kind=image</c> + <c>regionKind=pptxLogo</c> parameters into PPTX slides.</summary>
public static class PptxImageParameterApplicator
{
    public static byte[] Apply(byte[] pptxBytes, TemplateModelDocument model, ParameterResolutionResult resolved)
    {
        var output = pptxBytes;
        foreach (var param in model.Parameters)
        {
            if (!TemplateModelSerializer.IsPptxLogoParameter(param))
                continue;

            if (!resolved.Images.TryGetValue(param.Key, out var image) || image.Bytes is not { Length: > 0 })
                continue;

            output = PptxDomainLogoInjector.TryInject(output, image.Bytes, image.Extension);
        }

        return output;
    }
}
