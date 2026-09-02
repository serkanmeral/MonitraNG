using Outlook = Microsoft.Office.Interop.Outlook;

namespace MngLogs.OutlookAddin;

public static class OutlookMailCapture
{
    private const string PrSmtpAddress = "http://schemas.microsoft.com/mapi/proptag/0x39FE001E";

    public static List<string> Recipients(Outlook.MailItem mail)
    {
        var list = new List<string>();
        if (mail?.Recipients is null)
            return list;

        foreach (Outlook.Recipient recipient in mail.Recipients)
        {
            var smtp = TrySmtp(recipient);
            if (!string.IsNullOrWhiteSpace(smtp) &&
                !list.Any(x => string.Equals(x, smtp, StringComparison.OrdinalIgnoreCase)))
                list.Add(smtp!);
        }

        return list;
    }

    public static List<string> SaveAttachments(Outlook.MailItem mail, string tempRoot)
    {
        var paths = new List<string>();
        if (mail?.Attachments is null || mail.Attachments.Count == 0)
            return paths;

        Directory.CreateDirectory(tempRoot);
        for (var i = 1; i <= mail.Attachments.Count; i++)
        {
            Outlook.Attachment att = mail.Attachments[i];
            var name = SafeFileName(att.FileName);
            if (string.IsNullOrWhiteSpace(name))
                name = "attachment-" + i + ".bin";
            var path = Path.Combine(tempRoot, name);
            try
            {
                att.SaveAsFile(path);
                paths.Add(path);
            }
            catch (Exception ex)
            {
                AddinLog.Write("attachment save failed: " + name + " " + ex.Message);
            }
        }

        return paths;
    }

    public static string? TrySmtp(Outlook.Recipient recipient)
    {
        try
        {
            var viaProp = recipient.PropertyAccessor.GetProperty(PrSmtpAddress) as string;
            var normalized = AddressUtil.NormalizeSmtp(viaProp);
            if (normalized is not null)
                return normalized;
        }
        catch
        {
            // POP/IMAP accounts often have SMTP on Address already
        }

        return AddressUtil.NormalizeSmtp(recipient.Address);
    }

    private static string SafeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var cleaned = new string(chars);
        return cleaned.Length > 180 ? cleaned.Substring(0, 180) : cleaned;
    }
}
