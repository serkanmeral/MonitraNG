using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MngLogs.Agent.Metrics;

public sealed class LoggedOnUserInfo
{
    public string UserName { get; init; } = string.Empty;
    public string? Domain { get; init; }
    public int SessionId { get; init; }
    public string State { get; init; } = string.Empty;
    public string? StationName { get; init; }
    public string? ClientProtocol { get; init; }
    public DateTime? LogonAtUtc { get; init; }
    public long? DurationSeconds { get; init; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Domain) ? UserName : $"{Domain}\\{UserName}";
}

/// <summary>Interactive Windows session users via WTS API (LocalSystem-friendly).</summary>
[SupportedOSPlatform("windows")]
public static class WindowsLoggedOnUsers
{
    private const int WTS_CURRENT_SERVER_HANDLE = 0;
    private const int WTSUserName = 5;
    private const int WTSDomainName = 7;
    private const int WTSClientProtocolType = 16;
    private const int WTSSessionInfo = 24;

    private const int WinstationNameLength = 32;
    private const int DomainLength = 17;
    private const int UsernameLength = 20;

    public static IReadOnlyList<LoggedOnUserInfo> Collect()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        var list = new List<LoggedOnUserInfo>();
        if (!WTSEnumerateSessions(WTS_CURRENT_SERVER_HANDLE, 0, 1, out var sessionPtr, out var count) ||
            sessionPtr == IntPtr.Zero ||
            count <= 0)
            return list;

        try
        {
            var structSize = Marshal.SizeOf<WtsSessionInfo>();
            var nowUtc = DateTime.UtcNow;
            for (var i = 0; i < count; i++)
            {
                var sid = Marshal.PtrToStructure<WtsSessionInfo>(sessionPtr + (i * structSize));
                if (sid.State is not (WtsConnectState.Active or WtsConnectState.Connected or WtsConnectState.Disconnected))
                    continue;
                if (sid.SessionId == 0)
                    continue;

                var user = QuerySessionString(sid.SessionId, WTSUserName);
                if (string.IsNullOrWhiteSpace(user))
                    continue;

                var domain = QuerySessionString(sid.SessionId, WTSDomainName);
                var station = sid.WinStationName != IntPtr.Zero
                    ? Marshal.PtrToStringUni(sid.WinStationName)
                    : null;

                DateTime? logonAt = null;
                long? durationSec = null;
                if (TryQuerySessionInfo(sid.SessionId, out var info))
                {
                    if (info.LogonTime > 0)
                    {
                        try
                        {
                            logonAt = DateTime.FromFileTimeUtc(info.LogonTime);
                            if (logonAt > nowUtc.AddMinutes(5) || logonAt < nowUtc.AddYears(-5))
                                logonAt = null;
                            else
                                durationSec = Math.Max(0, (long)(nowUtc - logonAt.Value).TotalSeconds);
                        }
                        catch
                        {
                            logonAt = null;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(station) && !string.IsNullOrWhiteSpace(info.WinStationName))
                        station = info.WinStationName.TrimEnd('\0').Trim();
                }

                list.Add(new LoggedOnUserInfo
                {
                    UserName = user.Trim(),
                    Domain = string.IsNullOrWhiteSpace(domain) ? null : domain.Trim(),
                    SessionId = sid.SessionId,
                    State = sid.State.ToString(),
                    StationName = string.IsNullOrWhiteSpace(station) ? null : station.Trim(),
                    ClientProtocol = ResolveProtocol(sid.SessionId, station),
                    LogonAtUtc = logonAt,
                    DurationSeconds = durationSec
                });
            }
        }
        finally
        {
            WTSFreeMemory(sessionPtr);
        }

        return list
            .GroupBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.State == "Active").ThenByDescending(x => x.DurationSeconds ?? 0).First())
            .OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveProtocol(int sessionId, string? station)
    {
        var proto = QueryClientProtocol(sessionId);
        return proto switch
        {
            2 => "RDP",
            0 => "Console",
            1 => "Legacy",
            _ when !string.IsNullOrWhiteSpace(station) &&
                   station.StartsWith("RDP", StringComparison.OrdinalIgnoreCase) => "RDP",
            _ when !string.IsNullOrWhiteSpace(station) &&
                   station.Contains("Console", StringComparison.OrdinalIgnoreCase) => "Console",
            _ when proto >= 0 => $"Other({proto})",
            _ => "Unknown"
        };
    }

    private static int QueryClientProtocol(int sessionId)
    {
        if (!WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId, WTSClientProtocolType, out var buffer, out var bytes) ||
            buffer == IntPtr.Zero ||
            bytes < 2)
            return -1;
        try
        {
            return Marshal.ReadInt16(buffer) & 0xFFFF;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private static bool TryQuerySessionInfo(int sessionId, out WtsInfo info)
    {
        info = default;
        if (!WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId, WTSSessionInfo, out var buffer, out var bytes) ||
            buffer == IntPtr.Zero ||
            bytes < Marshal.SizeOf<WtsInfo>())
            return false;
        try
        {
            info = Marshal.PtrToStructure<WtsInfo>(buffer);
            return true;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private static string? QuerySessionString(int sessionId, int infoClass)
    {
        if (!WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId, infoClass, out var buffer, out _))
            return null;
        try
        {
            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private enum WtsConnectState
    {
        Active = 0,
        Connected = 1,
        ConnectQuery = 2,
        Shadow = 3,
        Disconnected = 4,
        Idle = 5,
        Listen = 6,
        Reset = 7,
        Down = 8,
        Init = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WtsSessionInfo
    {
        public int SessionId;
        public IntPtr WinStationName;
        public WtsConnectState State;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WtsInfo
    {
        public WtsConnectState State;
        public int SessionId;
        public int IncomingBytes;
        public int OutgoingBytes;
        public int IncomingFrames;
        public int OutgoingFrames;
        public int IncomingCompressedBytes;
        public int OutgoingCompressedBytes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WinstationNameLength)]
        public string WinStationName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DomainLength)]
        public string Domain;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = UsernameLength + 1)]
        public string UserName;
        public long ConnectTime;
        public long DisconnectTime;
        public long LastInputTime;
        public long LogonTime;
        public long CurrentTime;
    }

    [DllImport("wtsapi32.dll", EntryPoint = "WTSEnumerateSessionsW", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(
        IntPtr hServer,
        int reserved,
        int version,
        out IntPtr ppSessionInfo,
        out int pCount);

    [DllImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    private static extern bool WTSQuerySessionInformation(
        IntPtr hServer,
        int sessionId,
        int wtsInfoClass,
        out IntPtr ppBuffer,
        out int pBytesReturned);

    [DllImport("wtsapi32.dll", EntryPoint = "WTSFreeMemory")]
    private static extern void WTSFreeMemory(IntPtr memory);
}
