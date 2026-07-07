namespace MngDocument.Application.Configuration;

/// <summary>Collabora Online (CODE) — tarayıcı DOCX editörü.</summary>
public class CollaboraSettings
{
    public bool Enabled { get; set; }

    /// <summary>Tarayıcının iframe yükleyeceği taban URL (örn. http://192.168.20.8:9980).</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:9980";

    /// <summary>cool.html yolu (CODE sürümüne göre).</summary>
    public string EditorPath { get; set; } = "/browser/dist/cool.html";

    /// <summary>
    /// Host uygulama origin(leri) — Collabora PostMessage API (Doc_ModifiedStatus, Action_Save).
    /// Boşlukla ayrılmış birden fazla origin (örn. http://localhost:3000 http://127.0.0.1:3000).
    /// </summary>
    public string? PostMessageOrigin { get; set; }
}

public class WopiSettings
{
    /// <summary>Collabora konteynerinin erişeceği WOPI host (örn. http://mngdocument:5095).</summary>
    public string HostBaseUrl { get; set; } = "http://mngdocument:5095";

    /// <summary>WOPI oturum süresi (dakika).</summary>
    public int SessionMinutes { get; set; } = 480;
}
