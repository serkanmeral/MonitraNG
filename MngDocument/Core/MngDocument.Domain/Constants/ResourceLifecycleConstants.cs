namespace MngDocument.Domain.Constants;

/// <summary>F1-2 ince onay eylemleri (tam CCB yok).</summary>
public static class ResourceLifecycleAction
{
    public const string Submit = "submit";
    public const string Approve = "approve";
    public const string Reject = "reject";
    public const string Revise = "revise";

    public static bool IsValid(string? value) =>
        value is Submit or Approve or Reject or Revise;
}
