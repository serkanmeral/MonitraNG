namespace MngDocument.Application.Interfaces;

/// <summary>
/// Yüklenen dosyayı ClamAV kuyruğuna alır. HTTP yanıtını beklemez.
/// Temiz sonuçtan sonra indeks ayrıca kuyruğa girer.
/// </summary>
public interface IDocumentAutoScan
{
    void Schedule(string resourceId, string? bearerToken = null);
}
