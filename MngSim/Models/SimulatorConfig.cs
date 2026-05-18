namespace MngSim.Models;

/// <summary>
/// MngSim konfigürasyonu — sanal cihaz listesi ve port/broker ayarları. Keeper/Reactor bağımlılığı yok.
/// </summary>
public class SimulatorConfig
{
    /// <summary>HTTP cihaz portları: base + 1 + index. Varsayılan 19000.</summary>
    public int HttpBasePort { get; set; } = 19000;

    /// <summary>SNMP cihaz portları: base + index. Varsayılan 11161.</summary>
    public int SnmpBasePort { get; set; } = 11161;

    /// <summary>MQTT broker URL (örn. tcp://mosquitto:1883). Opsiyonel; MQTT cihaz varsa gerekli.</summary>
    public string? MqttBrokerUrl { get; set; }

    /// <summary>Sanal cihaz listesi.</summary>
    public List<VirtualDevice> Devices { get; set; } = new();
}

/// <summary>
/// Tek bir sanal cihaz — HTTP, SNMP veya MQTT ile veri sunar.
/// </summary>
public class VirtualDevice
{
    /// <summary>Benzersiz id (örn. loc1, pdu-ankara).</summary>
    public string Id { get; set; } = "";

    /// <summary>Görünen ad.</summary>
    public string Name { get; set; } = "";

    /// <summary>Lokasyon bilgisi (opsiyonel).</summary>
    public string? Location { get; set; }

    /// <summary>Protokol: Http, Snmp, Mqtt.</summary>
    public string Protocol { get; set; } = "Http";

    /// <summary>SNMP cihazlarda template: Pdu (güç dağıtım) veya Router (ağ cihazı).</summary>
    public string? SnmpTemplate { get; set; } = "Pdu";

    /// <summary>MQTT için room/topic id (topic: mngsim/devices/{roomId}/metrics).</summary>
    public string? RoomId { get; set; }

    /// <summary>Cihaz etkin mi? false ise dinleyici başlatılmaz (erişilemez cihaz simülasyonu). null/eski config = true.</summary>
    public bool? IsEnabled { get; set; } = true;
}
