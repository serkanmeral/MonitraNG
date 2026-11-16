using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Approvals;
using MngOperations.Application.Interfaces;

namespace MngOperations.Infrastructure.Services;

public sealed class OrgSeatLookup : IOrgSeatLookup
{
    private const string Dataset = "org_mevkiler";

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _context;
    private readonly ILogger<OrgSeatLookup> _logger;
    private IReadOnlyList<OrgSeatRef>? _seats;

    public OrgSeatLookup(
        IMngDataGatewayClient dg,
        IRequestContext context,
        ILogger<OrgSeatLookup> logger)
    {
        _dg = dg;
        _context = context;
        _logger = logger;
    }

    public async Task<string?> ResolveOccupantAsync(string? mevkiId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mevkiId))
            return null;

        _seats ??= await LoadAsync(cancellationToken);
        return OrgSeatApprover.ResolveOccupant(_seats, mevkiId);
    }

    private async Task<IReadOnlyList<OrgSeatRef>> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _dg.GetAsync<OrgSeatRow>(
                Dataset,
                "limit=1000&expand=false",
                _context.BearerToken,
                cancellationToken);

            return rows
                .Select(row => new OrgSeatRef(
                    row.DataId?.Trim() ?? string.Empty,
                    ReadId(row.Parent),
                    ReadId(row.Occupant)))
                .Where(seat => !string.IsNullOrWhiteSpace(seat.Id))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "org_mevkiler okunamadı. Onay grup listesine düşer.");
            return Array.Empty<OrgSeatRef>();
        }
    }

    private static string? ReadId(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var text = element.GetString();
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            case JsonValueKind.Object:
                foreach (var name in new[] { "__dataId", "dataId", "id", "userId" })
                {
                    if (element.TryGetProperty(name, out var id)
                        && id.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(id.GetString()))
                    {
                        return id.GetString()!.Trim();
                    }
                }
                return null;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var id = ReadId(item);
                    if (!string.IsNullOrWhiteSpace(id))
                        return id;
                }
                return null;
            default:
                return null;
        }
    }

    private sealed class OrgSeatRow
    {
        [JsonPropertyName("__dataId")]
        public string? DataId { get; set; }

        [JsonPropertyName("ust")]
        public JsonElement Parent { get; set; }

        [JsonPropertyName("oturan")]
        public JsonElement Occupant { get; set; }
    }
}
