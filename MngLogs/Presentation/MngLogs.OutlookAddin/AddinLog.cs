namespace MngLogs.OutlookAddin;

internal static class AddinLog
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MngLogs",
                "OutlookAddin");
            Directory.CreateDirectory(dir);
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine;
            lock (Gate)
            {
                File.AppendAllText(Path.Combine(dir, "addin.log"), line);
            }
        }
        catch
        {
            // never throw from ItemSend logging
        }
    }
}
