using MngDocument.Application.Contracts.Letterheads;

namespace MngDocument.Infrastructure.Services.Generation;

internal static class LetterheadBrandingResolver
{
    public static (TemplateFooterModel? Footer, TemplatePageLayoutModel PageLayout) Resolve(
        LetterheadResolveResult letterheadResolve,
        TemplateModelDocument templateModel)
    {
        if (!string.IsNullOrWhiteSpace(letterheadResolve.LetterheadId))
        {
            var layout = TemplateModelSerializer.ToPageLayoutModel(letterheadResolve.PageLayout)
                         ?? TemplatePageLayoutModel.CreateDefault();
            // Footer comes from letterhead design DOCX merge at generation time.
            return (null, layout);
        }

        var templateFooter = templateModel.Footer is { Enabled: true } ? templateModel.Footer : null;
        var templateLayout = templateModel.PageLayout ?? TemplatePageLayoutModel.CreateDefault();
        return (templateFooter, templateLayout);
    }
}
