using System.Collections.Generic;

namespace MngSim.Services;

/// <summary>
/// SNMP template kayıt defteri — isme göre template döndürür.
/// </summary>
public class SnmpTemplateRegistry
{
    private readonly Dictionary<string, ISnmpTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);

    public SnmpTemplateRegistry(IPduMetricGenerator pduGenerator)
    {
        _templates["Pdu"] = new PduSnmpTemplate(pduGenerator);
        _templates["Router"] = new RouterSnmpTemplate();
    }

    public ISnmpTemplate? GetTemplate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return _templates.GetValueOrDefault("Pdu");
        return _templates.GetValueOrDefault(name) ?? _templates.GetValueOrDefault("Pdu");
    }

    public IReadOnlyList<string> GetTemplateNames() => new[] { "Pdu", "Router" };
}
