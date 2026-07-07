namespace MngDocument.Application.Configuration;

/// <summary>Döküman editör kilidi — eşzamanlı düzenleme uyarısı ve sert kilit.</summary>
public class EditorLockSettings
{
    /// <summary>Başka kullanıcı düzenlerken açılış öncesi uyarı (UI).</summary>
    public bool WarnOnActiveEditor { get; set; } = true;

    /// <summary>Başka kullanıcı düzenlerken yeni oturum salt okunur açılır (yönetici bypass hariç).</summary>
    public bool EnforceExclusiveLock { get; set; } = true;

    /// <summary>Admin/manager sert kilidi atlayıp düzenleme modunda açabilir (yalnızca başka kullanıcı kilidi).</summary>
    public bool AllowManagerBypass { get; set; } = true;

    /// <summary>Aynı kullanıcının aynı dökümanda ikinci düzenleme oturumunu engelle.</summary>
    public bool BlockSameUserDuplicateSession { get; set; } = true;
}
