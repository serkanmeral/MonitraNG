using MngDocument.Application.Contracts.Generation;

namespace MngDocument.Infrastructure.Services;

/// <summary>Resolves <c>kind=image</c> + <c>regionKind=xlsxLogo</c> parameters into XLSX drawings.</summary>
public static class XlsxImageParameterApplicator
{
    public static byte[] Apply(byte[] xlsxBytes, TemplateModelDocument model, ParameterResolutionResult resolved)
    {
        var output = xlsxBytes;
        foreach (var param in model.Parameters)
        {
            if (!TemplateModelSerializer.IsXlsxLogoParameter(param))
                continue;

            if (!resolved.Images.TryGetValue(param.Key, out var image) || image.Bytes is not { Length: > 0 })
                continue;

            output = XlsxDomainLogoInjector.TryInject(output, image.Bytes, image.Extension);
        }

        return output;
    }
}
