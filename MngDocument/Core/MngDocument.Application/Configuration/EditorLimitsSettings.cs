namespace MngDocument.Application.Configuration;

/// <summary>Collabora öncesi WOPI oturum limitleri (home_mode tamponu).</summary>
public class EditorLimitsSettings
{
    /// <summary>Pre-Collabora bağlantı üst sınırı (Collabora home_mode 20'nin altında tampon).</summary>
    public int MaxConcurrentConnections { get; set; } = 18;

    /// <summary>Eşzamanlı benzersiz döküman/şablon/antet sayısı (Collabora 10'un altında tampon).</summary>
    public int MaxConcurrentDocuments { get; set; } = 9;

    /// <summary>Kullanıcı başına eşzamanlı oturum; 0 = sınırsız.</summary>
    public int MaxSessionsPerUser { get; set; } = 3;

    /// <summary>Son WOPI aktivitesinden sonra oturum sayılmaz / geçersiz sayılır (dakika).</summary>
    public int IdleTimeoutMinutes { get; set; } = 30;

    /// <summary>Limit kontrolü açık mı.</summary>
    public bool EnforceLimits { get; set; } = true;

    /// <summary>Collabora home_mode referans — yalnızca stats yanıtında bilgi amaçlı.</summary>
    public int CollaboraMaxConnections { get; set; } = 20;

    /// <summary>Collabora home_mode referans — yalnızca stats yanıtında bilgi amaçlı.</summary>
    public int CollaboraMaxDocuments { get; set; } = 10;
}
