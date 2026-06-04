using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

internal sealed record SecEventFlowBaselineEnrichmentResult(
    ParsedSecEvent Parsed,
    bool EmitNewFlowObservation);

internal static class SecEventFlowBaselineEnricher
{
    public static async Task<SecEventFlowBaselineEnrichmentResult> EnrichAsync(
        ParsedSecEvent parsed,
        string domain,
        ISecEventFlowBaselineStore baselineStore,
        CancellationToken cancellationToken)
    {
        if (!SecEventFlowBaselineRules.IsFlowAction(parsed.EventAction))
            return new SecEventFlowBaselineEnrichmentResult(parsed, false);

        if (string.IsNullOrWhiteSpace(parsed.NetworkSrcIp) || string.IsNullOrWhiteSpace(parsed.NetworkDstIp))
            return new SecEventFlowBaselineEnrichmentResult(parsed, false);

        var result = await baselineStore.ApplyFlowPairAsync(
            domain,
            parsed.NetworkSrcIp,
            parsed.NetworkDstIp,
            parsed.EventAction,
            cancellationToken);

        return new SecEventFlowBaselineEnrichmentResult(parsed, result.IsNewPair);
    }
}
