using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.EventLog;
using MngLogs.Agent.LocalUi;
using MngLogs.Agent.Metrics;
using MngLogs.Agent.Transport;

namespace MngLogs.Agent.Cli;

/// <summary>
/// Offline recovery CLI for Local UI PIN / port (and status).
/// Usage: MngLogs.Agent.exe &lt;verb&gt; ...
/// </summary>
public static class AgentCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        var (dataDirOverride, remaining) = ExtractDataDir(args);
        if (remaining.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        var verb = remaining[0].ToLowerInvariant();
        try
        {
            return verb switch
            {
                "help" or "-h" or "--help" => PrintHelpAndExit(),
                "status" => await StatusAsync(dataDirOverride),
                "pin" => await PinAsync(remaining.Skip(1).ToArray(), dataDirOverride),
                "port" => await PortAsync(remaining.Skip(1).ToArray(), dataDirOverride),
                "config" => await ConfigAsync(remaining.Skip(1).ToArray(), dataDirOverride),
                "catalog" => await CatalogAsync(remaining.Skip(1).ToArray(), dataDirOverride),
                _ => Unknown(verb)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Hata: {ex.Message}");
            return 1;
        }
    }

    public static bool IsCliInvocation(string[] args)
    {
        if (args.Length == 0)
            return false;
        var v = args[0].Trim().ToLowerInvariant();
        return v is "help" or "-h" or "--help" or "status" or "pin" or "port" or "config" or "catalog";
    }

    private static int PrintHelpAndExit()
    {
        PrintHelp();
        return 0;
    }

    private static int Unknown(string verb)
    {
        Console.Error.WriteLine($"Bilinmeyen komut: {verb}");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            MngLogs Agent CLI — Local UI kurtarma + kurulum config

            Kullanım:
              MngLogs.Agent.exe status [--data-dir <path>]
              MngLogs.Agent.exe pin status [--data-dir <path>]
              MngLogs.Agent.exe pin reset [--yes] [--data-dir <path>]
              MngLogs.Agent.exe pin set [--pin <pin>] [--data-dir <path>]
              MngLogs.Agent.exe port show [--data-dir <path>]
              MngLogs.Agent.exe port set <port> [--data-dir <path>]
              MngLogs.Agent.exe port check [port] [--data-dir <path>]
              MngLogs.Agent.exe config show [--data-dir <path>]
              MngLogs.Agent.exe config set [options] [--data-dir <path>]
              MngLogs.Agent.exe catalog show [--data-dir <path>]
              MngLogs.Agent.exe catalog sync [--data-dir <path>]

            config set (MSI/GPO sessiz; sadece verilen alanlar değişir):
              --collector <url>   --api-key <key>   --host-id <id>
              --ui-host <host>    --ui-port <port>

            catalog sync: collector'dan event-log paket kataloğunu çeker (yoksa builtin).

            Notlar:
              - Port/PIN/config değişince agent sürecini yeniden başlatın.
              - --data-dir yoksa ayar / env / %ProgramData%\MngLogs\Agent kullanılır.
              - pin set: --pin vermezseniz etkileşimli sorulur (onay ile).
            """);
    }

    private static bool IsHelp(string a) =>
        a is "help" or "-h" or "--help" or "/?";

    private static (string? DataDir, string[] Remaining) ExtractDataDir(string[] args)
    {
        string? dataDir = null;
        var list = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--data-dir" or "-d" && i + 1 < args.Length)
            {
                dataDir = args[++i];
                continue;
            }
            list.Add(args[i]);
        }
        return (dataDir, list.ToArray());
    }

    private static async Task<int> StatusAsync(string? dataDirOverride)
    {
        await using var scope = CreateScope(dataDirOverride);
        var config = scope.Services.GetRequiredService<IAgentConfigStore>();
        var auth = scope.Services.GetRequiredService<ILocalUiPinAuth>();
        var s = config.Current.System;
        var host = string.IsNullOrWhiteSpace(s.LocalUiHost) ? "127.0.0.1" : s.LocalUiHost;
        var port = s.LocalUiPort <= 0 ? 5092 : s.LocalUiPort;
        var pin = auth.GetStatus(null);
        var free = LocalUiPortProbe.IsPortAvailable(host, port, out var detail);
        var hint = LocalUiPortProbe.FindListenerProcessHint(port);

        Console.WriteLine($"Version       : {AgentVersion.Current}");
        Console.WriteLine($"DataDirectory : {config.ResolveDataDirectory()}");
        Console.WriteLine($"HostId        : {config.ResolveHostId()}");
        Console.WriteLine($"Collector     : {s.CollectorBaseUrl}");
        Console.WriteLine($"Local UI      : http://{host}:{port}/");
        Console.WriteLine($"Port durumu   : {(free ? "boş / kullanılabilir" : "DOLU veya bağlanılamıyor")}");
        if (!free && detail != null)
            Console.WriteLine($"  → {detail}");
        if (hint != null)
            Console.WriteLine($"  → {hint}");
        Console.WriteLine($"PIN           : {(pin.Configured ? "tanımlı" : "yok (ilk kurulum)")}");
        try
        {
            var uiPort = s.LocalUiPort <= 0 ? 5092 : s.LocalUiPort;
            var uiHost = string.IsNullOrWhiteSpace(s.LocalUiHost) ? "127.0.0.1" : s.LocalUiHost;
            var inv = HostMetricsCollector.CaptureInventory(new LocalUiBindInfo(uiPort, uiHost));
            Console.WriteLine($"Primary IP    : {inv.PrimaryIp ?? "—"}");
            Console.WriteLine($"Local UI bind : {inv.LocalUiHost}:{inv.LocalUiPort}");
            Console.WriteLine($"Boot (UTC)    : {inv.BootTimeUtc:u}");
            Console.WriteLine($"Uptime        : {FormatDuration(inv.UptimeSeconds)}");
            Console.WriteLine($"Logged on     : {(inv.LoggedOnUsers.Count == 0 ? "—" : string.Join(", ", inv.LoggedOnUsers))}");
            foreach (var sess in inv.Sessions)
            {
                Console.WriteLine(
                    $"  session {sess.SessionId}: {sess.User} [{sess.State}/{sess.ClientProtocol ?? "?"}] " +
                    $"since {sess.LogonAtUtc:u} ({FormatDuration(sess.DurationSeconds)})");
            }
        }
        catch
        {
            // ignore probe errors in CLI
        }
        Console.WriteLine();
        Console.WriteLine("Port doluysa : MngLogs.Agent.exe port set <yeniPort>");
        Console.WriteLine("PIN unutuldu : MngLogs.Agent.exe pin reset --yes");
        return free || !pin.Configured ? 0 : 0;
    }

    private static async Task<int> PinAsync(string[] args, string? dataDirOverride)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("pin alt komutu gerekli: status | reset | set");
            return 2;
        }

        await using var scope = CreateScope(dataDirOverride);
        var auth = scope.Services.GetRequiredService<ILocalUiPinAuth>();
        var config = scope.Services.GetRequiredService<IAgentConfigStore>();
        var sub = args[0].ToLowerInvariant();

        switch (sub)
        {
            case "status":
            {
                var st = auth.GetStatus(null);
                Console.WriteLine($"DataDirectory : {config.ResolveDataDirectory()}");
                Console.WriteLine($"PIN configured: {st.Configured}");
                Console.WriteLine($"Min length    : {st.MinPinLength}");
                return 0;
            }
            case "reset":
            {
                var yes = args.Any(a => a is "--yes" or "-y");
                if (!yes)
                {
                    Console.Write("PIN sıfırlansın mı? (yes/no): ");
                    var ans = Console.ReadLine()?.Trim();
                    if (!string.Equals(ans, "yes", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(ans, "y", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("İptal.");
                        return 0;
                    }
                }

                auth.ResetPin();
                Console.WriteLine("PIN sıfırlandı (ui-auth.json).");
                Console.WriteLine("Agent’ı yeniden başlatın; Politika sayfasında yeni PIN oluşturun.");
                Console.WriteLine($"DataDirectory: {config.ResolveDataDirectory()}");
                return 0;
            }
            case "set":
            {
                var pin = GetOption(args, "--pin");
                string confirm;
                if (string.IsNullOrEmpty(pin))
                {
                    Console.Write("Yeni PIN: ");
                    pin = ReadSecretLine();
                    Console.Write("Yeni PIN (tekrar): ");
                    confirm = ReadSecretLine();
                }
                else
                {
                    confirm = GetOption(args, "--confirm") ?? pin;
                }

                var result = auth.AdminSetPin(pin ?? "", confirm ?? "");
                if (!result.Ok)
                {
                    Console.Error.WriteLine(result.Error ?? "PIN ayarlanamadı.");
                    return 1;
                }

                Console.WriteLine("PIN kaydedildi.");
                Console.WriteLine("Agent çalışıyorsa yeniden başlatın (oturumlar bellek içi).");
                return 0;
            }
            default:
                Console.Error.WriteLine($"Bilinmeyen pin komutu: {sub}");
                return 2;
        }
    }

    private static async Task<int> PortAsync(string[] args, string? dataDirOverride)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("port alt komutu gerekli: show | set | check");
            return 2;
        }

        await using var scope = CreateScope(dataDirOverride);
        var config = scope.Services.GetRequiredService<IAgentConfigStore>();
        var system = config.Current.System;
        var host = string.IsNullOrWhiteSpace(system.LocalUiHost) ? "127.0.0.1" : system.LocalUiHost;
        var currentPort = system.LocalUiPort <= 0 ? 5092 : system.LocalUiPort;
        var sub = args[0].ToLowerInvariant();

        switch (sub)
        {
            case "show":
                Console.WriteLine($"DataDirectory : {config.ResolveDataDirectory()}");
                Console.WriteLine($"LocalUiHost   : {host}");
                Console.WriteLine($"LocalUiPort   : {currentPort}");
                Console.WriteLine($"URL           : http://{host}:{currentPort}/");
                return 0;

            case "check":
            {
                var port = currentPort;
                if (args.Length >= 2 && int.TryParse(args[1], out var p))
                    port = p;
                var free = LocalUiPortProbe.IsPortAvailable(host, port, out var detail);
                var hint = LocalUiPortProbe.FindListenerProcessHint(port);
                Console.WriteLine($"http://{host}:{port}/ → {(free ? "KULLANILABILIR" : "DOLU / HATA")}");
                if (detail != null) Console.WriteLine($"  {detail}");
                if (hint != null) Console.WriteLine($"  {hint}");
                if (!free)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Kurtarma: MngLogs.Agent.exe port set <yeniPort>");
                }
                return free ? 0 : 3;
            }

            case "set":
            {
                if (args.Length < 2 || !int.TryParse(args[1], out var newPort) || newPort is < 1 or > 65535)
                {
                    Console.Error.WriteLine("Kullanım: port set <1-65535>");
                    return 2;
                }

                if (!LocalUiPortProbe.IsPortAvailable(host, newPort, out var detail))
                {
                    Console.Error.WriteLine($"Uyarı: {newPort} şu an kullanılabilir görünmüyor.");
                    if (detail != null) Console.Error.WriteLine($"  {detail}");
                    Console.Write("Yine de kaydedilsin mi? (yes/no): ");
                    var ans = Console.ReadLine()?.Trim();
                    if (!string.Equals(ans, "yes", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(ans, "y", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("İptal.");
                        return 3;
                    }
                }

                system.LocalUiPort = newPort;
                await config.SaveSystemAsync(system);
                Console.WriteLine($"LocalUiPort = {newPort} kaydedildi (system.json).");
                Console.WriteLine($"Yeni URL: http://{host}:{newPort}/");
                Console.WriteLine("Agent’ı yeniden başlatın (çalışan süreç eski portu dinlemeye devam eder).");
                return 0;
            }

            default:
                Console.Error.WriteLine($"Bilinmeyen port komutu: {sub}");
                return 2;
        }
    }

    private static async Task<int> ConfigAsync(string[] args, string? dataDirOverride)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("config alt komutu gerekli: show | set");
            return 2;
        }

        await using var scope = CreateScope(dataDirOverride);
        var config = scope.Services.GetRequiredService<IAgentConfigStore>();
        var system = config.Current.System;
        var sub = args[0].ToLowerInvariant();

        switch (sub)
        {
            case "show":
                Console.WriteLine($"DataDirectory    : {config.ResolveDataDirectory()}");
                Console.WriteLine($"CollectorBaseUrl : {system.CollectorBaseUrl}");
                Console.WriteLine($"ApiKey           : {(string.IsNullOrEmpty(system.ApiKey) ? "(empty)" : "***")}");
                Console.WriteLine($"HostId           : {(string.IsNullOrWhiteSpace(system.HostId) ? "(machine)" : system.HostId)}");
                Console.WriteLine($"LocalUiHost      : {(string.IsNullOrWhiteSpace(system.LocalUiHost) ? "127.0.0.1" : system.LocalUiHost)}");
                Console.WriteLine($"LocalUiPort      : {(system.LocalUiPort <= 0 ? 5092 : system.LocalUiPort)}");
                return 0;

            case "set":
            {
                var collector = GetOption(args, "--collector");
                var apiKey = GetOption(args, "--api-key");
                var hostId = GetOption(args, "--host-id");
                var uiHost = GetOption(args, "--ui-host");
                var uiPortRaw = GetOption(args, "--ui-port");
                var hasApiKeyFlag = HasFlag(args, "--api-key");

                if (collector is null && !hasApiKeyFlag && hostId is null && uiHost is null && uiPortRaw is null)
                {
                    Console.Error.WriteLine("config set: en az bir alan verin (--collector, --api-key, --host-id, --ui-host, --ui-port).");
                    return 2;
                }

                if (collector is not null)
                    system.CollectorBaseUrl = collector.Trim().TrimEnd('/');
                if (hasApiKeyFlag)
                    system.ApiKey = apiKey ?? "";
                if (hostId is not null)
                    system.HostId = hostId.Trim();
                if (uiHost is not null)
                    system.LocalUiHost = uiHost.Trim();
                if (uiPortRaw is not null)
                {
                    if (!int.TryParse(uiPortRaw, out var port) || port is < 1 or > 65535)
                    {
                        Console.Error.WriteLine("--ui-port 1-65535 olmalı.");
                        return 2;
                    }

                    system.LocalUiPort = port;
                }

                if (!string.IsNullOrWhiteSpace(dataDirOverride))
                    system.DataDirectory = dataDirOverride.Trim();
                else if (string.IsNullOrWhiteSpace(system.DataDirectory))
                    system.DataDirectory = config.ResolveDataDirectory();

                await config.SaveSystemAsync(system);
                Console.WriteLine($"system.json kaydedildi: {Path.Combine(config.ResolveDataDirectory(), "system.json")}");
                return 0;
            }

            default:
                Console.Error.WriteLine($"Bilinmeyen config komutu: {sub}");
                return 2;
        }
    }

    private static async Task<int> CatalogAsync(string[] args, string? dataDirOverride)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("catalog alt komutu gerekli: show | sync");
            return 2;
        }

        await using var scope = CreateScope(dataDirOverride, includeCollector: true);
        var catalog = scope.Services.GetRequiredService<IEventLogPackageCatalogStore>();
        var sub = args[0].ToLowerInvariant();

        switch (sub)
        {
            case "show":
                Console.WriteLine($"Source        : {catalog.Source}");
                Console.WriteLine($"Version       : {catalog.Version ?? "(none)"}");
                Console.WriteLine($"LastSyncedUtc : {catalog.LastSyncedUtc?.ToString("o") ?? "(never)"}");
                Console.WriteLine($"Packages      : {catalog.ServerPackages.Count}");
                foreach (var p in catalog.ServerPackages)
                    Console.WriteLine($"  - {p.Name} ({p.Channel}) ids={p.EventIds.Count}");
                Console.WriteLine($"Optional      : {catalog.OptionalPackages.Count}");
                foreach (var p in catalog.OptionalPackages)
                    Console.WriteLine($"  - {p.Name} ({p.Channel}) ids={p.EventIds.Count}");
                return 0;

            case "sync":
                await catalog.RefreshAsync();
                Console.WriteLine($"Catalog sync OK — source={catalog.Source} version={catalog.Version} packages={catalog.ServerPackages.Count}");
                Console.WriteLine($"LastSyncedUtc : {catalog.LastSyncedUtc?.ToString("o")}");
                if (string.Equals(catalog.Source, "builtin", StringComparison.OrdinalIgnoreCase))
                    Console.WriteLine("Uyarı: collector'dan alınamadı; builtin katalog kullanılıyor. config show ile URL/API key kontrol edin.");
                return 0;

            default:
                Console.Error.WriteLine($"Bilinmeyen catalog komutu: {sub}");
                return 2;
        }
    }

    private static bool HasFlag(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static string ReadSecretLine()
    {
        // Console.ReadLine is acceptable for local admin recovery; avoid echoing if possible.
        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                    Console.Write("\b \b");
                }
                continue;
            }
            if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
                Console.Write('*');
            }
        }
        return new string(chars.ToArray());
    }

    private static ServiceProviderScope CreateScope(string? dataDirOverride, bool includeCollector = false)
    {
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        if (!string.IsNullOrWhiteSpace(dataDirOverride))
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MngLogsAgentSettings:System:DataDirectory"] = dataDirOverride
            });
        }

        var configuration = configBuilder.Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<MngLogsAgentSettings>(configuration.GetSection(MngLogsAgentSettings.SectionName));
        services.AddSingleton<IAgentConfigStore, AgentConfigStore>();
        services.AddSingleton<ILocalUiPinAuth, LocalUiPinAuth>();

        if (includeCollector)
        {
            services.AddHttpClient("collector", client => client.Timeout = TimeSpan.FromSeconds(30));
            services.AddSingleton<ICollectorClient, CollectorClient>();
            services.AddSingleton<IEventLogPackageCatalogStore, EventLogPackageCatalogStore>();
        }

        return new ServiceProviderScope(services.BuildServiceProvider());
    }

    private static string FormatDuration(long? seconds)
    {
        if (seconds is null or < 0)
            return "—";
        var ts = TimeSpan.FromSeconds(seconds.Value);
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}g {ts.Hours}s {ts.Minutes}d";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}s {ts.Minutes}d";
        return $"{ts.Minutes}d {ts.Seconds}sn";
    }

    private sealed class ServiceProviderScope(ServiceProvider provider) : IAsyncDisposable
    {
        public IServiceProvider Services => provider;
        public ValueTask DisposeAsync()
        {
            provider.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
