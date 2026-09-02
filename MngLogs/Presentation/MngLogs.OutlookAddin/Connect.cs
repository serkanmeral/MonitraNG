using System.Runtime.InteropServices;
using System.Windows.Forms;
using Extensibility;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace MngLogs.OutlookAddin;

[ComVisible(true)]
[Guid(Connect.Clsid)]
[ProgId(Connect.ProgIdValue)]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IDTExtensibility2))]
public class Connect : IDTExtensibility2
{
    public const string Clsid = "E7B2C4A1-9F18-4D6E-8A3B-1C5E9D0F2B44";
    public const string ProgIdValue = "MngLogs.OutlookAddin";

    static Connect()
    {
        AddinLog.Write("assembly loaded");
    }

    private Outlook.Application? _app;
    private Outlook.ApplicationEvents_11_ItemSendEventHandler? _itemSend;

    public void OnConnection(object Application, ext_ConnectMode ConnectMode, object AddInInst, ref Array custom)
    {
        try
        {
            _app = (Outlook.Application)Application;
            _itemSend = OnItemSend;
            _app.ItemSend += _itemSend;
            AddinLog.Write("connected " + DlpSendGate.ClientVersion);
        }
        catch (Exception ex)
        {
            AddinLog.Write("OnConnection: " + ex);
        }
    }

    public void OnDisconnection(ext_DisconnectMode RemoveMode, ref Array custom)
    {
        try
        {
            if (_app is not null && _itemSend is not null)
                _app.ItemSend -= _itemSend;
        }
        catch
        {
            // shutting down
        }

        _itemSend = null;
        _app = null;
        AddinLog.Write("disconnected");
    }

    public void OnAddInsUpdate(ref Array custom)
    {
    }

    public void OnStartupComplete(ref Array custom)
    {
        AddinLog.Write("startup complete");
    }

    public void OnBeginShutdown(ref Array custom)
    {
    }

    private void OnItemSend(object item, ref bool cancel)
    {
        var mail = item as Outlook.MailItem;
        if (mail is null)
            return;

        string? tempRoot = null;
        try
        {
            var endpoints = DlpEvaluateClient.ReadAgentEndpoints();
            var key = DlpEvaluateClient.ReadApiKey(endpoints.DataDirectory);
            tempRoot = Path.Combine(Path.GetTempPath(), "MngLogsDlp", Guid.NewGuid().ToString("N"));
            var paths = OutlookMailCapture.SaveAttachments(mail, tempRoot);
            var recipients = OutlookMailCapture.Recipients(mail);
            var body = new
            {
                action = "email.send",
                windowsUser = DlpSendGate.WindowsUser(),
                recipients,
                attachments = paths.Select(p => new { path = p }).ToList(),
                client = new { kind = DlpSendGate.ClientKind, version = DlpSendGate.ClientVersion }
            };

            var response = DlpEvaluateClient.Evaluate(
                endpoints.BaseUrl,
                key,
                body,
                out var transportFailed,
                out var error);
            var decision = DlpSendGate.FromEvaluate(response, transportFailed, error);
            AddinLog.Write(
                "evaluate failOpen=" + decision.FailOpen +
                " cancel=" + decision.CancelSend +
                " wouldBlock=" + (response?.WouldBlock ?? false) +
                " rule=" + (response?.MatchedRuleId ?? "") +
                " msg=" + (decision.UserMessage ?? ""));

            if (decision.CancelSend)
            {
                cancel = true;
                ShowNotice(decision.UserMessage ?? "DLP blocked this send.");
                return;
            }

            if (decision.FailOpen || decision.ShowAuditHint)
                ShowNoticeAsync(decision.UserMessage);
        }
        catch (Exception ex)
        {
            AddinLog.Write("OnItemSend fail-open: " + ex);
            ShowNoticeAsync("DLP evaluate failed: " + ex.Message + " Send allowed (Dilim 1 fail-open).");
        }
        finally
        {
            TryDelete(tempRoot);
        }
    }

    private static void ShowNotice(string text)
    {
        try
        {
            MessageBox.Show(text, "MngLogs DLP", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch
        {
            AddinLog.Write("MessageBox failed: " + text);
        }
    }

    private static void ShowNoticeAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        var copy = text!;
        ThreadPool.QueueUserWorkItem(_ => ShowNotice(copy));
    }

    private static void TryDelete(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return;
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // temp cleanup is best-effort
        }
    }
}
