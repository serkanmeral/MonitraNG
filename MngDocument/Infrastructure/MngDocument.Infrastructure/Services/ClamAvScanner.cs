using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using MngDocument.Application.Configuration;

namespace MngDocument.Infrastructure.Services;

public enum ClamScanKind
{
    Clean,
    Threat,
    Failed
}

public sealed record ClamScanResult(ClamScanKind Kind, string? Detail);

/// <summary>clamd INSTREAM. Şifreli arşiv ve sınır aşımı tehdit değil, taranamadı sayılır.</summary>
public sealed class ClamAvScanner
{
    public async Task<ClamScanResult> ScanAsync(byte[] content, AntivirusSettings settings, CancellationToken ct)
    {
        var host = string.IsNullOrWhiteSpace(settings.Host) ? "clamav" : settings.Host.Trim();
        var port = settings.Port <= 0 ? 3310 : settings.Port;
        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 15, 180));
        var deadline = DateTime.UtcNow.AddMinutes(4);
        Exception? last = null;

        while (true)
        {
            try
            {
                return await ScanOnceAsync(content, host, port, timeout, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                last = ex;
                if (DateTime.UtcNow >= deadline)
                    break;
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
        }

        return new ClamScanResult(ClamScanKind.Failed, last?.Message ?? "Tarama motoru yanıt vermedi.");
    }

    private static async Task<ClamScanResult> ScanOnceAsync(
        byte[] content,
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var client = new TcpClient();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(TimeSpan.FromSeconds(10));
        await client.ConnectAsync(host, port, connectCts.Token).ConfigureAwait(false);
        await using var stream = client.GetStream();
        using var scanCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        scanCts.CancelAfter(timeout);
        var scanCt = scanCts.Token;

        await stream.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), scanCt).ConfigureAwait(false);
        var offset = 0;
        while (offset < content.Length)
        {
            var count = Math.Min(8192, content.Length - offset);
            var length = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)count);
            await stream.WriteAsync(length, scanCt).ConfigureAwait(false);
            await stream.WriteAsync(content.AsMemory(offset, count), scanCt).ConfigureAwait(false);
            offset += count;
        }

        await stream.WriteAsync(new byte[4], scanCt).ConfigureAwait(false);

        using var response = new MemoryStream();
        var buffer = new byte[256];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, scanCt).ConfigureAwait(false);
            if (read <= 0)
                break;
            var end = Array.IndexOf(buffer, (byte)0, 0, read);
            if (end >= 0)
            {
                response.Write(buffer, 0, end);
                break;
            }

            response.Write(buffer, 0, read);
            if (response.Length > 1024)
                break;
        }

        return Parse(Encoding.ASCII.GetString(response.ToArray()));
    }

    private static ClamScanResult Parse(string raw)
    {
        var line = raw.Trim();
        if (line.EndsWith("OK", StringComparison.OrdinalIgnoreCase)
            && line.IndexOf("FOUND", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return new ClamScanResult(ClamScanKind.Clean, null);
        }

        var foundAt = line.IndexOf(" FOUND", StringComparison.OrdinalIgnoreCase);
        if (foundAt > 0)
        {
            var name = line[..foundAt];
            var colon = name.IndexOf(':');
            if (colon >= 0)
                name = name[(colon + 1)..].Trim();
            if (IsUnscannable(name))
                return new ClamScanResult(ClamScanKind.Failed, name);
            return new ClamScanResult(ClamScanKind.Threat, string.IsNullOrWhiteSpace(name) ? "Tehdit" : name);
        }

        if (line.IndexOf("size limit exceeded", StringComparison.OrdinalIgnoreCase) >= 0
            || line.IndexOf("Encrypted", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new ClamScanResult(ClamScanKind.Failed, line);
        }

        return new ClamScanResult(
            ClamScanKind.Failed,
            string.IsNullOrWhiteSpace(line) ? "Tarama tamamlanamadı." : line);
    }

    private static bool IsUnscannable(string signature) =>
        signature.Contains("Encrypted", StringComparison.OrdinalIgnoreCase)
        || signature.Contains("Limits.Exceeded", StringComparison.OrdinalIgnoreCase)
        || signature.Contains("Heuristics.Limits", StringComparison.OrdinalIgnoreCase);
}
