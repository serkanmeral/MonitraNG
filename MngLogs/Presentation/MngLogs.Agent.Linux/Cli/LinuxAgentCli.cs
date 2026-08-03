using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MngLogs.Agent;
using MngLogs.Agent.Cli;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.LocalUi;
using MngLogs.Agent.Transport;

namespace MngLogs.Agent.Linux.Cli;

/// <summary>Offline recovery CLI for Linux agent (status / pin / port / config).</summary>
public static class LinuxAgentCli
{
    public static bool IsCliInvocation(string[] args)
    {
        if (args.Length == 0)
            return false;
        var v = args[0].Trim().ToLowerInvariant();
        return v is "help" or "-h" or "--help" or "status" or "pin" or "port" or "config";
    }

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        var (dataDirOverride, configDirOverride, remaining) = ExtractDirOverrides(args);
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
                "status" => await StatusAsync(dataDirOverride, configDirOverride),
                "pin" => await PinAsync(remaining.Skip(1).ToArray(), dataDirOverride, configDirOverride),
                "port" => await PortAsync(remaining.Skip(1).ToArray(), dataDirOverride, configDirOverride),
                "config" => await ConfigAsync(remaining.Skip(1).ToArray(), dataDirOverride, configDirOverride),
                _ => Unknown(verb)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Hata: {ex.Message}");
            return 1;
        }
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
            MngLogs Agent (Linux) CLI

            Kullanım:
              MngLogs.Agent status [--data-dir <path>] [--config-dir <path>]
              MngLogs.Agent pin status|reset|set [--data-dir ...] [--config-dir ...]
              MngLogs.Agent port show|check|set <port> [--data-dir ...] [--config-dir ...]
              MngLogs.Agent config show|set [options] [--data-dir ...] [--config-dir ...]

            config set:
              --collector <url>   --api-key <key>   --host-id <id>
              --ui-host <host>    --ui-port <port>

            Varsayılan yollar (Linux):
              config: /etc/mnglogs/agent
              data:   /var/lib/mnglogs/agent
            """);
    }

    private static bool IsHelp(string a) =>
        a is "help" or "-h" or "--help";

    private static (string? DataDir, string? ConfigDir, string[] Remaining) ExtractDirOverrides(string[] args)
    {
        string? data = null;
        string? config = null;
        var list = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--data-dir" && i + 1 < args.Length)
            {
                data = args[++i];
                continue;
            }

            if (args[i] is "--config-dir" && i + 1 < args.Length)
            {
                config = args[++i];
                continue;
            }

            list.Add(args[i]);
        }

        return (data, config, list.ToArray());
    }

    private static ServiceProvider BuildServices(string? dataDir, string? configDir)
    {
        var settings = new MngLogsAgentSettings();
        if (!string.IsNullOrWhiteSpace(dataDir))
            settings.System.DataDirectory = dataDir.Trim();
        if (!string.IsNullOrWhiteSpace(configDir))
            settings.System.ConfigDirectory = configDir.Trim();
        else if (OperatingSystem.IsLinux() && string.IsNullOrWhiteSpace(settings.System.ConfigDirectory))
            settings.System.ConfigDirectory = PlatformPaths.LinuxConfigDirectory;
        if (OperatingSystem.IsLinux() && string.IsNullOrWhiteSpace(settings.System.DataDirectory))
            settings.System.DataDirectory = PlatformPaths.LinuxDataDirectory;

        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(settings));
        services.AddSingleton<IAgentConfigStore, AgentConfigStore>();
        services.AddSingleton<ILocalUiPinAuth, LocalUiPinAuth>();
        services.AddHttpClient("collector");
        services.AddSingleton<ICollectorClient, CollectorClient>();
        return services.BuildServiceProvider();
    }

    private static async Task<int> StatusAsync(string? dataDir, string? configDir)
    {
        await using var sp = BuildServices(dataDir, configDir);
        var config = sp.GetRequiredService<IAgentConfigStore>();
        var sys = config.Current.System;
        var healthy = await sp.GetRequiredService<ICollectorClient>().HealthAsync();

        Console.WriteLine($"HostId:        {config.ResolveHostId()}");
        Console.WriteLine($"Collector:     {sys.CollectorBaseUrl}");
        Console.WriteLine($"Collector OK:  {healthy}");
        Console.WriteLine($"Local UI:      http://{sys.LocalUiHost}:{sys.LocalUiPort}/");
        Console.WriteLine($"Config dir:    {config.ResolveConfigDirectory()}");
        Console.WriteLine($"Data dir:      {config.ResolveDataDirectory()}");
        Console.WriteLine($"Agent version: {AgentVersion.Current}");
        return 0;
    }

    private static async Task<int> PinAsync(string[] args, string? dataDir, string? configDir)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("pin status|reset|set");
            return 2;
        }

        await using var sp = BuildServices(dataDir, configDir);
        var auth = sp.GetRequiredService<ILocalUiPinAuth>();
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "status":
                var st = auth.GetStatus(null);
                Console.WriteLine($"Configured: {st.Configured}");
                return 0;
            case "reset":
                if (!args.Any(a => a is "--yes" or "-y"))
                {
                    Console.Error.WriteLine("PIN sıfırlamak için --yes ekleyin.");
                    return 2;
                }

                auth.ResetPin();
                Console.WriteLine("PIN sıfırlandı.");
                return 0;
            case "set":
                var pin = ExtractOption(args, "--pin") ?? ReadSecret("Yeni PIN: ");
                var confirm = ExtractOption(args, "--pin-confirm") ?? ReadSecret("Tekrar: ");
                var result = auth.AdminSetPin(pin, confirm);
                if (!result.Ok)
                {
                    Console.Error.WriteLine(result.Error);
                    return 1;
                }

                Console.WriteLine("PIN ayarlandı.");
                return 0;
            default:
                Console.Error.WriteLine("pin status|reset|set");
                return 2;
        }
    }

    private static async Task<int> PortAsync(string[] args, string? dataDir, string? configDir)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("port show|check|set");
            return 2;
        }

        await using var sp = BuildServices(dataDir, configDir);
        var config = sp.GetRequiredService<IAgentConfigStore>();
        var sys = config.Current.System;
        var sub = args[0].ToLowerInvariant();
        switch (sub)
        {
            case "show":
                Console.WriteLine($"{sys.LocalUiHost}:{sys.LocalUiPort}");
                return 0;
            case "check":
                var port = args.Length > 1 && int.TryParse(args[1], out var p) ? p : sys.LocalUiPort;
                var ok = LocalUiPortProbe.IsPortAvailable(sys.LocalUiHost, port, out var detail);
                Console.WriteLine(ok ? $"Port {port} kullanılabilir." : $"Port {port} kullanılamıyor: {detail}");
                return ok ? 0 : 1;
            case "set":
                if (args.Length < 2 || !int.TryParse(args[1], out var newPort))
                {
                    Console.Error.WriteLine("port set <port>");
                    return 2;
                }

                sys.LocalUiPort = newPort;
                await config.SaveSystemAsync(sys);
                Console.WriteLine($"Local UI port = {newPort}. Agent'ı yeniden başlatın.");
                return 0;
            default:
                Console.Error.WriteLine("port show|check|set");
                return 2;
        }
    }

    private static async Task<int> ConfigAsync(string[] args, string? dataDir, string? configDir)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("config show|set");
            return 2;
        }

        await using var sp = BuildServices(dataDir, configDir);
        var config = sp.GetRequiredService<IAgentConfigStore>();
        var sub = args[0].ToLowerInvariant();
        if (sub == "show")
        {
            var sys = config.Current.System;
            Console.WriteLine($"collector: {sys.CollectorBaseUrl}");
            Console.WriteLine($"apiKey:    {(string.IsNullOrEmpty(sys.ApiKey) ? "(empty)" : "***")}");
            Console.WriteLine($"hostId:    {config.ResolveHostId()}");
            Console.WriteLine($"ui:        {sys.LocalUiHost}:{sys.LocalUiPort}");
            Console.WriteLine($"configDir: {config.ResolveConfigDirectory()}");
            Console.WriteLine($"dataDir:   {config.ResolveDataDirectory()}");
            return 0;
        }

        if (sub != "set")
        {
            Console.Error.WriteLine("config show|set");
            return 2;
        }

        var system = config.Current.System;
        var collector = ExtractOption(args, "--collector");
        var apiKey = ExtractOption(args, "--api-key");
        var hostId = ExtractOption(args, "--host-id");
        var uiHost = ExtractOption(args, "--ui-host");
        var uiPortRaw = ExtractOption(args, "--ui-port");

        if (collector != null) system.CollectorBaseUrl = collector;
        if (apiKey != null) system.ApiKey = apiKey;
        if (hostId != null) system.HostId = hostId;
        if (uiHost != null) system.LocalUiHost = uiHost;
        if (uiPortRaw != null && int.TryParse(uiPortRaw, out var uiPort))
            system.LocalUiPort = uiPort;

        if (string.IsNullOrWhiteSpace(system.DataDirectory) && OperatingSystem.IsLinux())
            system.DataDirectory = PlatformPaths.LinuxDataDirectory;
        if (string.IsNullOrWhiteSpace(system.ConfigDirectory) && OperatingSystem.IsLinux())
            system.ConfigDirectory = PlatformPaths.LinuxConfigDirectory;

        await config.SaveSystemAsync(system);
        Console.WriteLine("system.json kaydedildi. Agent'ı yeniden başlatın.");
        return 0;
    }

    private static string? ExtractOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? string.Empty;
    }
}
