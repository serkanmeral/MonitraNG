using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;
using MngDocument.Infrastructure.Helpers;

namespace MngDocument.Infrastructure.Services;

internal static class TemplateDocxUpdater
{
    public static async Task<DmDocumentTemplate> ReplaceDocxAsync(
        IMngDataGatewayClient dg,
        DmDocumentTemplate template,
        string templateId,
        byte[] docxBytes,
        string fileName,
        string? username,
        string token,
        CancellationToken ct)
    {
        var referenceFile = new Dictionary<string, object?>
        {
            ["content"] = Convert.ToBase64String(docxBytes),
            ["originalFileName"] = fileName
        };

        var payload = new Dictionary<string, object?>
        {
            ["categoryId"] = template.categoryId,
            ["name"] = template.name,
            ["code"] = template.code,
            ["description"] = template.description,
            ["sourceResourceId"] = template.sourceResourceId,
            ["sourceFileName"] = fileName,
            ["creationMode"] = template.creationMode ?? TemplateCreationMode.Blank,
            ["status"] = template.status ?? TemplateStatus.Draft,
            ["modelJson"] = template.modelJson,
            ["referenceFile"] = referenceFile,
            ["updatedBy"] = username,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await dg.UpdateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            templateId,
            payload,
            token,
            ct);

        var (path, storedName) = DgFileFieldReader.Read(updated);
        if (!string.IsNullOrWhiteSpace(path))
        {
            var patch = new Dictionary<string, object?>
            {
                ["sourceStoragePath"] = path,
                ["sourceFileName"] = storedName ?? fileName,
                ["updatedBy"] = username,
                ["updatedAt"] = DateTime.UtcNow
            };
            updated = await dg.UpdateAsync<DmDocumentTemplate>(
                DmDatasets.DocumentTemplates,
                templateId,
                patch,
                token,
                ct);
        }

        return updated;
    }

    public static async Task<byte[]> LoadDocxAsync(
        IMngDataGatewayClient dg,
        DmDocumentTemplate template,
        string token,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(template.sourceStoragePath))
            return await dg.DownloadFileAsync(template.sourceStoragePath, token, ct);

        var (path, _) = DgFileFieldReader.Read(template);
        if (!string.IsNullOrWhiteSpace(path))
            return await dg.DownloadFileAsync(path!, token, ct);

        return MinimalDocxFactory.CreateBlank();
    }
}
