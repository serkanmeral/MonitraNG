namespace MngDocument.Application.Interfaces;

/// <summary>
/// Desteklenen bir belge kaydı oluşunca veya içeriği değişince indeksi kuyruğa alır.
/// HTTP yanıtını beklemez.
/// </summary>
public interface IDocumentAutoIndex
{
    void Schedule(string resourceId, string? bearerToken = null);
}
