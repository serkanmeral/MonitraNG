using System.Text.RegularExpressions;

namespace MngOperations.Application.Utilities;

public static class WorkItemKeyFormat
{
    public const string DefaultFormat = "{PREFIX}-{SEQ:D4}";

    public static string Apply(string format, string prefix, int sequence)
    {
        var result = format.Replace("{PREFIX}", prefix, StringComparison.OrdinalIgnoreCase);

        var match = Regex.Match(format, @"\{SEQ:D(\d+)\}", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var digits))
        {
            return Regex.Replace(
                result,
                @"\{SEQ:D\d+\}",
                sequence.ToString($"D{digits}"),
                RegexOptions.IgnoreCase);
        }

        return result.Replace("{SEQ}", sequence.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public static int ParseSequence(string key, string prefix)
    {
        var prefixPart = prefix + "-";
        if (!key.StartsWith(prefixPart, StringComparison.OrdinalIgnoreCase))
            return 0;

        var suffix = key[prefixPart.Length..];
        return int.TryParse(suffix, out var n) ? n : 0;
    }
}
