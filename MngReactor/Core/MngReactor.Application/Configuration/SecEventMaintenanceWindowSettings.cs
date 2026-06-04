namespace MngReactor.Application.Configuration;

/// <summary>U3: bakım penceresi dışı ayrıcalıklı oturum tespiti (UTC saat aralığı).</summary>
public class SecEventMaintenanceWindowSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>İzin verilen aralık başlangıcı (UTC saat, dahil).</summary>
    public int AllowedStartHourUtc { get; set; } = 8;

    /// <summary>İzin verilen aralık bitişi (UTC saat, hariç).</summary>
    public int AllowedEndHourUtc { get; set; } = 20;
}
