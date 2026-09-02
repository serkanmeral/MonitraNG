namespace MngLogs.OutlookAddin;

public static class AddressUtil
{
    public static string? NormalizeSmtp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var value = raw.Trim();
        const string smtpPrefix = "SMTP:";
        if (value.StartsWith(smtpPrefix, StringComparison.OrdinalIgnoreCase))
            value = value.Substring(smtpPrefix.Length).Trim();
        if (!value.Contains("@"))
            return null;
        return value;
    }
}
