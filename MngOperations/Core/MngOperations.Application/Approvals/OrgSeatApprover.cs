namespace MngOperations.Application.Approvals;

public sealed record OrgSeatRef(string Id, string? ParentId, string? OccupantId);

/// <summary>
/// Mevki karar anında çözülür. Boş koltuk üste çıkar. Döngü ve kopuk zincir oturan döndürmez.
/// </summary>
public static class OrgSeatApprover
{
    public const int MaxDepth = 64;

    public static string? ResolveOccupant(IEnumerable<OrgSeatRef>? seats, string? mevkiId)
    {
        if (string.IsNullOrWhiteSpace(mevkiId) || seats == null)
            return null;

        var byId = new Dictionary<string, OrgSeatRef>(StringComparer.Ordinal);
        foreach (var seat in seats)
        {
            if (seat == null || string.IsNullOrWhiteSpace(seat.Id))
                continue;
            byId[seat.Id.Trim()] = seat;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cursor = mevkiId.Trim();
        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (!seen.Add(cursor))
                return null;
            if (!byId.TryGetValue(cursor, out var seat))
                return null;
            if (!string.IsNullOrWhiteSpace(seat.OccupantId))
                return seat.OccupantId.Trim();
            if (string.IsNullOrWhiteSpace(seat.ParentId))
                return null;
            cursor = seat.ParentId.Trim();
        }

        return null;
    }
}
