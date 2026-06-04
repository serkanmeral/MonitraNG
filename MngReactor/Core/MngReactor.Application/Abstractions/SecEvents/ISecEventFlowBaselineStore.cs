namespace MngReactor.Application.Abstractions.SecEvents;

/// <summary>
/// U7: src→dst akış çiftleri için baseline öğrenme ve yeni akış tespiti.
/// </summary>
public interface ISecEventFlowBaselineStore
{
    /// <summary>
    /// Çifti baseline'a işler. Öğrenme tamamlandıktan sonra ilk kez görülen src→dst için <see cref="SecEventFlowBaselineApplyResult.IsNewPair"/> true döner.
    /// Orijinal <paramref name="originalEventAction"/> (denied_flow / allowed_flow) korunur.
    /// </summary>
    Task<SecEventFlowBaselineApplyResult> ApplyFlowPairAsync(
        string domain,
        string srcIp,
        string dstIp,
        string originalEventAction,
        CancellationToken cancellationToken = default);
}

public sealed record SecEventFlowBaselineApplyResult(bool IsNewPair);
