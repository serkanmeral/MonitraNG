using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Infrastructure.Helpers;

namespace MngDocument.Infrastructure.Services;

/// <summary>Loads the latest cover page design DOCX from DataGateway storage.</summary>
internal static class CoverPageDesignFileLoader
{
    internal static string? ResolveDesignPath(DmCoverPage row)
    {
        var (pathFromField, _) = DgFileFieldReader.Read(row);
        if (!string.IsNullOrWhiteSpace(pathFromField))
            return pathFromField;

        var storagePath = row.designStoragePath?.Trim();
        return string.IsNullOrWhiteSpace(storagePath) ? null : storagePath;
    }

    internal static async Task<byte[]?> DownloadDesignAsync(
        IMngDataGatewayClient dg,
        DmCoverPage row,
        string? token,
        CancellationToken ct)
    {
        var path = ResolveDesignPath(row);
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return await dg.DownloadFileAsync(path, token, ct);
    }
}
