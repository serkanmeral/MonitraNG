# MngHub - WebSocket Gateway Architecture Plan

**Microservice:** Real-time WebSocket Gateway  
**Version:** 1.0.0 (Planning)  
**Last Updated:** 16 Aralık 2025

---

## 🎯 Genel Bakış

**MngHub**, RabbitMQ event'lerini WebSocket (SignalR) bağlantılarına bridge eden, real-time communication sağlayan bir mikroservistir.

### Temel Özellikler

- ✅ **SignalR Hub** - WebSocket connection management
- ✅ **Domain-based Rooms** - Multi-tenant isolation (SignalR Groups)
- ✅ **RabbitMQ Consumer** - Domain-specific topic subscription
- ✅ **JWT Authentication** - MngKeeper ile token validation
- ✅ **Message Filtering** - Domain-based message routing
- ✅ **Connection Management** - Lifecycle ve reconnection handling
- ✅ **Clean Architecture** - MngDataGateway pattern'i takip eder

### Amaç

**Multi-tenant real-time messaging:**
- Her domain kendi SignalR room'una sahip (`domain.{domainName}`)
- RabbitMQ'dan gelen domain-specific event'ler sadece o domain'in room'una gönderilir
- Örnek: `domain.seven.users.created` event'i sadece "seven" domain room'una bağlı kullanıcılar görür

---

## 🏗️ Mimari Yapı

### Clean Architecture Katmanları

```
MngHub/
├── Core/
│   ├── MngHub.Domain/          # Entities, Exceptions
│   └── MngHub.Application/     # Interfaces, DTOs, Configuration
├── Infrastructure/
│   ├── MngHub.Infrastructure/  # RabbitMQ Consumer, SignalR Hub
│   └── MngHub.Persistence/     # Connection tracking (opsiyonel)
└── Presentation/
    └── MngHub.Api/             # Program.cs, Middleware, Startup
```

### Katman Sorumlulukları

#### 1. Domain Layer
- `Connection` entity (connection tracking için)
- `ConnectionException`, `ValidationException` gibi exceptions
- Domain constants (routing key patterns, message types, room naming)

#### 2. Application Layer
- `MngHubSettings` - Configuration
- `IConnectionManager` - Connection lifecycle interface
- `IRabbitMqConsumer` - RabbitMQ subscription interface
- `IJwtValidator` - JWT validation interface
- DTOs (ConnectionInfo, MessageDto, etc.)

#### 3. Infrastructure Layer
- `RabbitMqConsumerService` - RabbitMQ topic subscription
- `ConnectionManager` - SignalR connection tracking
- `JwtValidatorService` - JWT validation (MngKeeper API)
- `SignalRHub` - SignalR Hub implementation

#### 4. Persistence Layer (Opsiyonel)
- Connection state tracking (MongoDB - gelecekte)
- Message buffer (reconnection için - gelecekte)

#### 5. Presentation Layer
- `Program.cs` - Startup configuration
- `NotificationHub` - SignalR Hub
- Middleware (exception handling, logging)

---

## 📋 Proje Yapısı

```
MngHub/
├── Core/
│   ├── MngHub.Domain/
│   │   ├── Entities/
│   │   │   ├── Base/
│   │   │   │   └── BaseEntity.cs
│   │   │   └── Connection.cs
│   │   ├── Exceptions/
│   │   │   ├── MngHubException.cs
│   │   │   ├── ConnectionException.cs
│   │   │   └── ValidationException.cs
│   │   └── Constants/
│   │       └── RoutingKeys.cs
│   │   └── MngHub.Domain.csproj
│   │
│   └── MngHub.Application/
│       ├── Configuration/
│       │   └── MngHubSettings.cs
│       ├── DTOs/
│       │   ├── Common/
│       │   │   ├── MessageDto.cs
│       │   │   └── ConnectionInfoDto.cs
│       │   └── Validation/
│       ├── Services/
│       │   ├── IConnectionManager.cs
│       │   ├── IRabbitMqConsumer.cs
│       │   └── IJwtValidator.cs
│       ├── ServiceRegistration.cs
│       └── MngHub.Application.csproj
│
├── Infrastructure/
│   ├── MngHub.Infrastructure/
│   │   ├── Services/
│   │   │   ├── RabbitMq/
│   │   │   │   └── RabbitMqConsumerService.cs
│   │   │   ├── SignalR/
│   │   │   │   └── NotificationHub.cs
│   │   │   ├── Connection/
│   │   │   │   └── ConnectionManager.cs
│   │   │   └── Jwt/
│   │   │       └── JwtValidatorService.cs
│   │   ├── ServiceRegistration.cs
│   │   └── MngHub.Infrastructure.csproj
│   │
│   └── MngHub.Persistence/ (Opsiyonel - Gelecekte)
│       ├── Services/
│       └── MngHub.Persistence.csproj
│
└── Presentation/
    └── MngHub.Api/
        ├── Config/
        │   └── Extensions.cs
        ├── Hubs/
        │   └── NotificationHub.cs (Infrastructure'dan referans)
        ├── Middleware/
        │   └── GlobalExceptionHandlerMiddleware.cs
        ├── Program.cs
        ├── appsettings.json
        └── MngHub.Api.csproj
```

---

## ⚙️ Configuration

### MngHubSettings

```csharp
// Application/Configuration/MngHubSettings.cs
namespace MngHub.Application.Configuration;

public class MngHubSettings
{
    public ServerSettings Server { get; set; }
    public Rabbitmq RabbitMQ { get; set; }
    public CertificateSettings CertificateSettings { get; set; }
    public string OpenApiServerPath { get; set; }
    public Actors Actors { get; set; }
    public SignalRSettings SignalR { get; set; }
    public ConnectionSettings Connection { get; set; }
}

public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5020;
    public string Scheme { get; set; } = "https";
}

public class Rabbitmq
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string VirtualHost { get; set; }
    public string ExchangeName { get; set; } = "mng.topics";
}

public class CertificateSettings
{
    public string DNS { get; set; }
    public string CERT_FILE { get; set; }
    public string KEY_FILE { get; set; }
    public string CERT_FILE_CONTENT { get; set; }
    public string KEY_FILE_CONTENT { get; set; }
}

public class Actors
{
    public string MngKeeper { get; set; } // JWT validation için
}

public class SignalRSettings
{
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ClientTimeoutInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public int MaximumReceiveMessageSize { get; set; } = 32 * 1024; // 32KB
}

public class ConnectionSettings
{
    public int MaxConcurrentConnections { get; set; } = 5000;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxMessageSize { get; set; } = 32 * 1024; // 32KB
    public int RateLimitPerConnection { get; set; } = 100; // messages/minute
}
```

### appsettings.json

```json
{
  "MngHubSettings": {
    "Server": {
      "Host": "0.0.0.0",
      "Port": 5020,
      "Scheme": "https"
    },
    "OpenApiServerPath": "https://localhost:5020",
    "RabbitMQ": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "admin",
      "Password": "admin123",
      "VirtualHost": "/",
      "ExchangeName": "mng.topics"
    },
    "CertificateSettings": {
      "DNS": "localhost",
      "CERT_FILE": "",
      "KEY_FILE": ""
    },
    "Actors": {
      "MngKeeper": "https://localhost:5001"
    },
    "SignalR": {
      "KeepAliveInterval": "00:00:30",
      "ClientTimeoutInterval": "00:01:00",
      "HandshakeTimeout": "00:00:15",
      "MaximumReceiveMessageSize": 32768
    },
    "Connection": {
      "MaxConcurrentConnections": 5000,
      "ConnectionTimeout": "00:30:00",
      "HeartbeatInterval": "00:00:30",
      "MaxMessageSize": 32768,
      "RateLimitPerConnection": 100
    }
  },
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.Seq"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341"
        }
      }
    ]
  }
}
```

---

## 🔌 Core Services

### 1. IConnectionManager

```csharp
// Application/Services/IConnectionManager.cs
namespace MngHub.Application.Services;

public interface IConnectionManager
{
    Task<ConnectionInfo> AddConnectionAsync(string connectionId, string userId, string domainName);
    Task RemoveConnectionAsync(string connectionId);
    Task<ConnectionInfo?> GetConnectionAsync(string connectionId);
    Task<List<ConnectionInfo>> GetConnectionsByDomainAsync(string domainName);
    Task<List<ConnectionInfo>> GetAllConnectionsAsync();
    Task<bool> IsConnectedAsync(string connectionId);
    string GetDomainRoomName(string domainName); // "domain.{domainName}"
    string GetGlobalRoomName(); // "global"
}
```

### 2. IRabbitMqConsumer

```csharp
// Application/Services/IRabbitMqConsumer.cs
namespace MngHub.Application.Services;

public interface IRabbitMqConsumer
{
    Task SubscribeAsync(string connectionId, List<string> routingKeys, Func<string, object, Task> messageHandler);
    Task UnsubscribeAsync(string connectionId);
    Task UnsubscribeAllAsync(string connectionId);
    Task<bool> IsSubscribedAsync(string connectionId, string routingKey);
    Task ConnectAsync();
}

// Routing Key Patterns:
// - "global.*" → Tüm kullanıcılara (tüm domain'ler)
// - "domain.{domainName}.#" → Sadece belirli domain'e
// Örnek: "domain.seven.#" → seven domain'indeki tüm event'ler
```

### 3. IJwtValidator

```csharp
// Application/Services/IJwtValidator.cs
namespace MngHub.Application.Services;

public interface IJwtValidator
{
    Task<Dictionary<string, string>> ValidateAsync(string token);
    Task<bool> IsValidAsync(string token);
}
```

---

## 🎮 SignalR Hub Implementation

### NotificationHub

```csharp
// Infrastructure/Services/SignalR/NotificationHub.cs
using Microsoft.AspNetCore.SignalR;
using MngHub.Application.Services;
using System.Security.Claims;

namespace MngHub.Infrastructure.Services.SignalR;

public class NotificationHub : Hub
{
    private readonly IConnectionManager _connectionManager;
    private readonly IRabbitMqConsumer _rabbitMqConsumer;
    private readonly IJwtValidator _jwtValidator;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        IConnectionManager connectionManager,
        IRabbitMqConsumer rabbitMqConsumer,
        IJwtValidator jwtValidator,
        ILogger<NotificationHub> logger)
    {
        _connectionManager = connectionManager;
        _rabbitMqConsumer = rabbitMqConsumer;
        _jwtValidator = jwtValidator;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            // 1. Get JWT token from query string
            var token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
            
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Connection rejected: No token provided. ConnectionId: {ConnectionId}", 
                    Context.ConnectionId);
                Context.Abort();
                return;
            }

            // 2. Validate JWT token
            var claims = await _jwtValidator.ValidateAsync(token);
            
            var domainName = claims.GetValueOrDefault("domain_name");
            var userId = claims.GetValueOrDefault("sub") ?? claims.GetValueOrDefault(ClaimTypes.NameIdentifier);
            var username = claims.GetValueOrDefault("preferred_username");

            if (string.IsNullOrEmpty(domainName) || string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Connection rejected: Invalid token claims. ConnectionId: {ConnectionId}", 
                    Context.ConnectionId);
                Context.Abort();
                return;
            }

            // 3. Register connection
            var connectionInfo = await _connectionManager.AddConnectionAsync(
                Context.ConnectionId, 
                userId, 
                domainName);

            // 4. Add to SignalR domain-based room
            var domainRoomName = _connectionManager.GetDomainRoomName(domainName); // "domain.seven"
            await Groups.AddToGroupAsync(Context.ConnectionId, domainRoomName);
            
            // Optionally add to global room for system-wide announcements
            var globalRoomName = _connectionManager.GetGlobalRoomName(); // "global"
            await Groups.AddToGroupAsync(Context.ConnectionId, globalRoomName);

            // 5. Subscribe to RabbitMQ topics
            var routingKeys = new List<string>
            {
                "global.*",                          // Global events (system announcements)
                $"domain.{domainName}.#"              // Domain-specific events (örn: domain.seven.#)
            };

            await _rabbitMqConsumer.SubscribeAsync(
                Context.ConnectionId,
                routingKeys,
                async (routingKey, message) =>
                {
                    // Route message to appropriate SignalR room based on routing key
                    if (routingKey.StartsWith("global."))
                    {
                        // Global events → Send to all users in global room
                        await Clients.Group(globalRoomName).SendAsync("ReceiveMessage", new
                        {
                            routingKey,
                            message,
                            timestamp = DateTime.UtcNow
                        });
                    }
                    else if (routingKey.StartsWith($"domain.{domainName}."))
                    {
                        // Domain events → Send only to domain room
                        await Clients.Group(domainRoomName).SendAsync("ReceiveMessage", new
                        {
                            routingKey,
                            message,
                            timestamp = DateTime.UtcNow
                        });
                    }
                });

            _logger.LogInformation(
                "Client connected. ConnectionId: {ConnectionId}, UserId: {UserId}, Domain: {Domain}",
                Context.ConnectionId, userId, domainName);

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during connection. ConnectionId: {ConnectionId}", Context.ConnectionId);
            Context.Abort();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            // Get connection info before removing
            var connectionInfo = await _connectionManager.GetConnectionAsync(Context.ConnectionId);
            
            // Unsubscribe from RabbitMQ
            await _rabbitMqConsumer.UnsubscribeAsync(Context.ConnectionId);
            
            // Remove from SignalR groups (automatic, but explicit for logging)
            if (connectionInfo != null)
            {
                var domainRoomName = _connectionManager.GetDomainRoomName(connectionInfo.DomainName);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, domainRoomName);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, _connectionManager.GetGlobalRoomName());
            }
            
            // Remove connection
            await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);

            _logger.LogInformation("Client disconnected. ConnectionId: {ConnectionId}", Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnection. ConnectionId: {ConnectionId}", Context.ConnectionId);
        }
    }

    // Client can send messages (optional)
    public async Task SendMessage(string message)
    {
        await Clients.Caller.SendAsync("ReceiveMessage", new
        {
            message,
            timestamp = DateTime.UtcNow
        });
    }
}
```

---

## 📨 RabbitMQ Consumer Service

### RabbitMqConsumerService

```csharp
// Infrastructure/Services/RabbitMq/RabbitMqConsumerService.cs
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using MngHub.Application.Services;
using MngHub.Application.Configuration;

namespace MngHub.Infrastructure.Services.RabbitMq;

public class RabbitMqConsumerService : IRabbitMqConsumer, IDisposable
{
    private readonly ILogger<RabbitMqConsumerService> _logger;
    private readonly MngHubSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly Dictionary<string, List<string>> _subscriptions = new();
    private readonly Dictionary<string, EventingBasicConsumer> _consumers = new();
    private readonly object _lockObject = new();

    public RabbitMqConsumerService(
        ILogger<RabbitMqConsumerService> logger,
        IOptions<MngHubSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task ConnectAsync()
    {
        if (_connection?.IsOpen == true)
            return;

        var factory = new ConnectionFactory
        {
            HostName = _settings.RabbitMQ.Host,
            Port = _settings.RabbitMQ.Port,
            UserName = _settings.RabbitMQ.Username,
            Password = _settings.RabbitMQ.Password,
            VirtualHost = _settings.RabbitMQ.VirtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Ensure exchange exists
        _channel.ExchangeDeclare(
            exchange: _settings.RabbitMQ.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        _logger.LogInformation("RabbitMQ connected for consumer");
    }

    public async Task SubscribeAsync(
        string connectionId, 
        List<string> routingKeys, 
        Func<string, object, Task> messageHandler)
    {
        await ConnectAsync();

        lock (_lockObject)
        {
            if (_subscriptions.ContainsKey(connectionId))
            {
                _logger.LogWarning("Connection {ConnectionId} already subscribed", connectionId);
                return;
            }

            // Create queue for this connection
            var queueName = $"mnghub.{connectionId}";
            _channel.QueueDeclare(
                queue: queueName,
                durable: false,
                exclusive: true,
                autoDelete: true);

            // Bind queue to exchange with routing keys
            foreach (var routingKey in routingKeys)
            {
                _channel.QueueBind(
                    queue: queueName,
                    exchange: _settings.RabbitMQ.ExchangeName,
                    routingKey: routingKey);

                _logger.LogDebug("Bound queue {QueueName} to {Exchange} with routing key {RoutingKey}",
                    queueName, _settings.RabbitMQ.ExchangeName, routingKey);
            }

            // Create consumer
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = JsonSerializer.Deserialize<object>(
                        Encoding.UTF8.GetString(body));
                    var routingKey = ea.RoutingKey;

                    await messageHandler(routingKey, message!);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message for connection {ConnectionId}", connectionId);
                }
            };

            _channel.BasicConsume(
                queue: queueName,
                autoAck: true,
                consumer: consumer);

            _subscriptions[connectionId] = routingKeys;
            _consumers[connectionId] = consumer;

            _logger.LogInformation(
                "Subscribed connection {ConnectionId} to {Count} routing keys",
                connectionId, routingKeys.Count);
        }
    }

    public async Task UnsubscribeAsync(string connectionId)
    {
        lock (_lockObject)
        {
            if (!_subscriptions.ContainsKey(connectionId))
                return;

            // Queue will be auto-deleted when connection closes
            _subscriptions.Remove(connectionId);
            _consumers.Remove(connectionId);

            _logger.LogInformation("Unsubscribed connection {ConnectionId}", connectionId);
        }

        await Task.CompletedTask;
    }

    public async Task UnsubscribeAllAsync(string connectionId)
    {
        await UnsubscribeAsync(connectionId);
    }

    public async Task<bool> IsSubscribedAsync(string connectionId, string routingKey)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            return _subscriptions.ContainsKey(connectionId) &&
                   _subscriptions[connectionId].Contains(routingKey);
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
```

---

## 🔐 JWT Validation Service

### JwtValidatorService

```csharp
// Infrastructure/Services/Jwt/JwtValidatorService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using MngHub.Application.Services;
using MngHub.Application.Configuration;

namespace MngHub.Infrastructure.Services.Jwt;

public class JwtValidatorService : IJwtValidator
{
    private readonly ILogger<JwtValidatorService> _logger;
    private readonly MngHubSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public JwtValidatorService(
        ILogger<JwtValidatorService> logger,
        IOptions<MngHubSettings> settings,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache)
    {
        _logger = logger;
        _settings = settings.Value;
        _httpClient = httpClientFactory.CreateClient();
        _cache = cache;
    }

    public async Task<Dictionary<string, string>> ValidateAsync(string token)
    {
        // Check cache first
        var cacheKey = $"jwt_{token.GetHashCode()}";
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, string>? cachedClaims))
        {
            return cachedClaims!;
        }

        try
        {
            // Validate via MngKeeper API
            var request = new HttpRequestMessage(HttpMethod.Get, 
                $"{_settings.Actors.MngKeeper}/api/auth/validate");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new UnauthorizedAccessException("Invalid token");
            }

            var claims = new Dictionary<string, string>();
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);

            foreach (var claim in jsonToken.Claims)
            {
                claims[claim.Type] = claim.Value;
            }

            // Cache for 5 minutes
            _cache.Set(cacheKey, claims, TimeSpan.FromMinutes(5));

            return claims;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWT validation failed");
            throw;
        }
    }

    public async Task<bool> IsValidAsync(string token)
    {
        try
        {
            await ValidateAsync(token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

---

## 🚀 Program.cs

```csharp
// Presentation/MngHub.Api/Program.cs
using MngHub.Api.Config;
using MngHub.Application;
using MngHub.Application.Configuration;
using MngHub.Infrastructure;
using MngHub.Infrastructure.Services.SignalR;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Load configuration
builder.Configuration.AddEnvironmentVariables();

var settings = builder.Configuration.GetSection("MngHubSettings")
    .Get<MngHubSettings>();

if (settings == null)
{
    throw new InvalidOperationException("MngHubSettings configuration is required!");
}

// Initialize Serilog
var log = builder.InitSerilog(settings);

// Get certificate
X509Certificate2 certificate;
try
{
    certificate = CertificateHandler.GetCertificate(log, settings);
    log.Information("Certificate loaded successfully");
}
catch (Exception ex)
{
    log.Fatal(ex, "Failed to load certificate");
    throw;
}

// Initialize services
builder.InitWebAPP(certificate);
builder.InitOpenApi();
builder.InitSignalR(settings);

// HttpContextAccessor
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Application & Infrastructure Services
builder.Services.AddApplicationServices(settings);
builder.Services.AddInfrastructureServices();

var app = builder.Build();

// Initialize RabbitMQ connection
try
{
    var rabbitMqConsumer = app.Services.GetRequiredService<IRabbitMqConsumer>();
    await rabbitMqConsumer.ConnectAsync();
    Log.Information("RabbitMQ consumer initialized");
}
catch (Exception ex)
{
    Log.Warning(ex, "Failed to connect to RabbitMQ on startup");
}

app.UseApplicationSettings(settings);

// Map SignalR Hub
app.MapHub<NotificationHub>("/ws");

try
{
    Log.Information("Starting MngHub API on {Host}:{Port}", 
        settings.Server.Host, settings.Server.Port);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

---

## 📦 NuGet Paketleri

### Domain
- (Genellikle paket gerekmez)

### Application
- `Microsoft.Extensions.Options`

### Infrastructure
- `RabbitMQ.Client`
- `Microsoft.AspNetCore.SignalR`
- `System.IdentityModel.Tokens.Jwt`
- `Microsoft.Extensions.Http`
- `Microsoft.Extensions.Caching.Memory`

### API
- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`
- `Serilog.Sinks.Seq`
- `Swashbuckle.AspNetCore`
- `Scalar.AspNetCore`
- `Microsoft.AspNetCore.Authentication.JwtBearer`

---

## ✅ Implementation Checklist

### Phase 1: Core Infrastructure
- [ ] Solution ve projeler oluşturuldu
- [ ] Proje referansları ayarlandı
- [ ] NuGet paketleri yüklendi
- [ ] Configuration (MngHubSettings) oluşturuldu
- [ ] appsettings.json yapılandırıldı

### Phase 2: Domain & Application Layer
- [ ] Domain entities oluşturuldu
- [ ] Domain exceptions oluşturuldu
- [ ] Service interface'leri tanımlandı
- [ ] DTOs oluşturuldu
- [ ] ServiceRegistration.cs oluşturuldu

### Phase 3: Infrastructure Layer
- [ ] RabbitMqConsumerService implementasyonu
- [ ] ConnectionManager implementasyonu
- [ ] JwtValidatorService implementasyonu
- [ ] NotificationHub implementasyonu
- [ ] ServiceRegistration.cs oluşturuldu

### Phase 4: API Layer
- [ ] Program.cs yapılandırıldı
- [ ] Extensions.cs oluşturuldu
- [ ] GlobalExceptionHandlerMiddleware oluşturuldu
- [ ] SignalR configuration
- [ ] Certificate handling

### Phase 5: Testing
- [ ] Uygulama çalışıyor
- [ ] SignalR connection test edildi
- [ ] RabbitMQ subscription test edildi
- [ ] JWT validation test edildi
- [ ] Message forwarding test edildi

---

## 🎯 Domain-based Room Yapısı

### Room Naming Convention

```
SignalR Room Names:
- "domain.{domainName}" → Domain-specific room (örn: "domain.seven")
- "global" → System-wide announcements room
```

### Message Routing Logic

```
RabbitMQ Routing Key → SignalR Room Mapping:

1. "global.*" 
   → Clients.Group("global")
   → Tüm bağlı kullanıcılar görür

2. "domain.{domainName}.#" (örn: "domain.seven.users.created")
   → Clients.Group("domain.{domainName}")
   → Sadece o domain'in room'una bağlı kullanıcılar görür
```

### Örnek Senaryo

**Senaryo:** "seven" domain'inde yeni kullanıcı oluşturuldu

1. **MngKeeper** → RabbitMQ'ya event publish eder:
   ```
   Routing Key: "domain.seven.users.created"
   Exchange: "mng.topics"
   ```

2. **MngHub RabbitMQ Consumer** → Event'i alır ve routing key'e göre filtreler

3. **SignalR Hub** → Mesajı sadece `domain.seven` room'una gönderir:
   ```csharp
   await Clients.Group("domain.seven").SendAsync("ReceiveMessage", message);
   ```

4. **Frontend** → Sadece "seven" domain'ine bağlı kullanıcılar mesajı alır

### Güvenlik

- ✅ JWT token'dan `domain_name` claim'i alınır
- ✅ Kullanıcı sadece kendi domain'inin room'una eklenir
- ✅ RabbitMQ routing key'leri domain'e göre filtrelenir
- ✅ Cross-domain message leakage yok

---

## 🎯 Sonraki Adımlar

1. **Proje Yapısını Oluştur** - Solution ve projeler
2. **Configuration Setup** - Settings ve appsettings.json
3. **Core Services** - Interface'ler ve implementasyonlar
4. **SignalR Hub** - Domain-based room management
5. **RabbitMQ Consumer** - Domain-specific subscription ve message routing
6. **JWT Validation** - MngKeeper entegrasyonu
7. **Testing** - End-to-end test (domain isolation test)

---

**Not:** Bu plan, MngDataGateway Architecture Guide pattern'lerini takip eder ve **domain-based multi-tenant WebSocket Gateway** gereksinimlerine göre özelleştirilmiştir.

