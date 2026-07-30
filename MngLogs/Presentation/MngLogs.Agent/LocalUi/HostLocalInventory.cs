using System.ServiceProcess;
using System.Windows.Forms;

namespace MngLogs.Agent.LocalUi;

/// <summary>Host OS helpers for Local UI pickers (services list, exe browse).</summary>
internal static class HostLocalInventory
{
    public sealed record HostServiceItem(string Name, string DisplayName, string Status);

    public static IReadOnlyList<HostServiceItem> ListWindowsServices()
    {
        var controllers = ServiceController.GetServices();
        try
        {
            return controllers
                .Select(s =>
                {
                    string status;
                    try { status = s.Status.ToString(); }
                    catch { status = "Unknown"; }

                    var display = string.IsNullOrWhiteSpace(s.DisplayName) ? s.ServiceName : s.DisplayName;
                    return new HostServiceItem(s.ServiceName, display, status);
                })
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            foreach (var c in controllers)
                c.Dispose();
        }
    }

    /// <summary>
    /// Shows a native OpenFileDialog on an STA thread (required by Win32 common dialogs).
    /// Returns null when the user cancels.
    /// </summary>
    public static string? BrowseExecutable(CancellationToken cancellationToken = default)
    {
        if (!Environment.UserInteractive)
        {
            throw new InvalidOperationException(
                "Dosya seçici yalnızca etkileşimli oturumda açılabilir (Windows Service oturumunda çalışmaz).");
        }

        string? path = null;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "Çalıştırılabilir dosya seçin",
                    Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*",
                    FilterIndex = 1,
                    CheckFileExists = true,
                    CheckPathExists = true,
                    Multiselect = false,
                    RestoreDirectory = true,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                };

                path = dialog.ShowDialog() == DialogResult.OK
                    ? dialog.FileName
                    : null;
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        while (!thread.Join(200))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (error is not null)
            throw error;

        return path;
    }
}
