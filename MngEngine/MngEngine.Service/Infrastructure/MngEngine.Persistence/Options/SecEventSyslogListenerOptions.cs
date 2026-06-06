namespace MngEngine.Persistence.Options;

/// <summary>Syslog UDP dinleyici — port başına kaynak ipucu (IT merkezi port profili).</summary>
public sealed class SecEventSyslogListenerOptions
{
    public int UdpPort { get; set; }

    public string SourceType { get; set; } = "firewall";

    public string SourceProduct { get; set; } = "generic-syslog";
}
