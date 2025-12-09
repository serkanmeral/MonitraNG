# DataGateway Mimarisi - Uygulama Rehberi

## 📋 İçindekiler

1. [Genel Bakış](#genel-bakış)
2. [Mimari Prensipler](#mimari-prensipler)
3. [Katman Yapısı](#katman-yapısı)
4. [Proje Yapısı](#proje-yapısı)
5. [Configuration Pattern](#configuration-pattern)
6. [Dependency Injection](#dependency-injection)
7. [Base Entity Pattern](#base-entity-pattern)
8. [Service Pattern](#service-pattern)
9. [Controller Pattern](#controller-pattern)
10. [Exception Handling](#exception-handling)
11. [Authentication & Authorization](#authentication--authorization)
12. [MongoDB Context Service](#mongodb-context-service)
13. [Event Publishing Pattern](#event-publishing-pattern)
14. [Adım Adım Kurulum](#adım-adım-kurulum)
15. [Örnek Implementasyon](#örnek-implementasyon)

---

## 🎯 Genel Bakış

Bu döküman, **MngDataGateway** mimarisine benzer bir **Clean Architecture** tabanlı, **multi-tenant**, **event-driven** mikroservis oluşturmak için kapsamlı bir rehberdir.

### Temel Özellikler

- ✅ **Clean Architecture** - Katmanlı mimari
- ✅ **Multi-tenant İzolasyon** - JWT token bazlı domain izolasyonu
- ✅ **Event-Driven** - RabbitMQ entegrasyonu
- ✅ **Dynamic Schema** - Runtime'da schema tanımlama
- ✅ **MongoDB** - NoSQL veritabanı desteği
- ✅ **JWT Authentication** - Token bazlı kimlik doğrulama
- ✅ **Audit Trail** - Otomatik history tracking
- ✅ **Validation** - Kapsamlı doğrulama mekanizması

---

## 🏗️ Mimari Prensipler

### 1. Clean Architecture

**Dependency Rule:** İç katmanlar dış katmanları bilmez. Dış katmanlar iç katmanlara bağımlıdır.

```
┌─────────────────────────────────────┐
│   Presentation Layer (API)          │  ← En dış katman
├─────────────────────────────────────┤
│   Infrastructure Layer              │  ← Dış servisler
├─────────────────────────────────────┤
│   Persistence Layer                  │  ← Veri erişimi
├─────────────────────────────────────┤
│   Application Layer                 │  ← İş mantığı
├─────────────────────────────────────┤
│   Domain Layer                      │  ← En iç katman
└─────────────────────────────────────┘
```

### 2. SOLID Prensipleri

- **S**ingle Responsibility: Her sınıf tek bir sorumluluğa sahip
- **O**pen/Closed: Genişlemeye açık, değişikliğe kapalı
- **L**iskov Substitution: Alt sınıflar üst sınıfların yerine kullanılabilir
- **I**nterface Segregation: Küçük, özel interface'ler
- **D**ependency Inversion: Soyutlamalara bağımlılık

### 3. Multi-Tenancy Pattern

- JWT token'dan `domain_name` claim'i alınır
- Database adı: `{prefix}_{domain_name}` formatında
- Her domain kendi database'inde izole edilir

---

## 📁 Katman Yapısı

### Katman Sorumlulukları

#### 1. Domain Layer (En İç)
**Sorumluluklar:**
- Entity tanımları
- Domain exceptions
- Value objects
- Domain interfaces (opsiyonel)

**Bağımlılıklar:** Hiçbir katmana bağımlı değil

#### 2. Application Layer
**Sorumluluklar:**
- Service interface'leri
- DTOs (Data Transfer Objects)
- Configuration classes
- Application exceptions

**Bağımlılıklar:** Sadece Domain Layer

#### 3. Persistence Layer
**Sorumluluklar:**
- Service implementasyonları
- Repository pattern
- MongoDB operations
- Data mapping

**Bağımlılıklar:** Application + Domain

#### 4. Infrastructure Layer
**Sorumluluklar:**
- External service integrations (RabbitMQ, HTTP clients)
- Certificate handling
- Infrastructure utilities

**Bağımlılıklar:** Application + Domain

#### 5. Presentation Layer (API)
**Sorumluluklar:**
- Controllers
- Middleware
- Request/Response handling
- Authentication/Authorization

**Bağımlılıklar:** Application + Infrastructure + Persistence

---

## 📂 Proje Yapısı

### Klasör Organizasyonu

```
YourProject/
├── Core/
│   ├── YourProject.Domain/
│   │   ├── Entities/
│   │   │   ├── Base/
│   │   │   │   └── BaseEntity.cs
│   │   │   └── YourEntity.cs
│   │   └── Exceptions/
│   │       └── YourProjectException.cs
│   │   └── YourProject.Domain.csproj
│   │
│   └── YourProject.Application/
│       ├── Configuration/
│       │   └── YourProjectSettings.cs
│       ├── DTOs/
│       │   ├── Common/
│       │   ├── YourEntity/
│       │   └── Validation/
│       ├── Services/
│       │   └── IYourService.cs
│       ├── ServiceRegistration.cs
│       └── YourProject.Application.csproj
│
├── Infrastructure/
│   ├── YourProject.Infrastructure/
│   │   ├── Services/
│   │   │   └── RabbitMq/
│   │   │       └── RabbitMqService.cs
│   │   ├── ServiceRegistration.cs
│   │   └── YourProject.Infrastructure.csproj
│   │
│   └── YourProject.Persistence/
│       ├── Services/
│       │   ├── MongoContextService.cs
│       │   ├── UserInfoService.cs
│       │   └── YourService.cs
│       ├── ServiceRegistration.cs
│       └── YourProject.Persistence.csproj
│
└── Presentation/
    └── YourProject.Api/
        ├── Config/
        │   └── Extensions.cs
        ├── Controllers/
        │   └── YourController.cs
        ├── Middleware/
        │   └── GlobalExceptionHandlerMiddleware.cs
        ├── Program.cs
        ├── appsettings.json
        └── YourProject.Api.csproj
```

---

## ⚙️ Configuration Pattern

### 1. Settings Class (Application Layer)

```csharp
// Application/Configuration/YourProjectSettings.cs
namespace YourProject.Application.Configuration;

public class YourProjectSettings
{
    public ServerSettings Server { get; set; }
    public Mongodb MongoDB { get; set; }
    public Rabbitmq RabbitMQ { get; set; }
    public CertificateSettings CertificateSettings { get; set; }
    public string OpenApiServerPath { get; set; }
    public Actors Actors { get; set; }
    public HistorySettings History { get; set; } = new();
    public DeletedDataSettings DeletedData { get; set; } = new();
}

public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5010;
    public string Scheme { get; set; } = "https";
}

public class Mongodb
{
    public string ConnectionString { get; set; }
}

public class Rabbitmq
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string VirtualHost { get; set; }
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
    public string AuthService { get; set; }
}

public class HistorySettings
{
    public int MaxHistoryEntries { get; set; } = 50;
}

public class DeletedDataSettings
{
    public int RetentionDays { get; set; } = 7;
}
```

### 2. appsettings.json

```json
{
  "YourProjectSettings": {
    "Server": {
      "Host": "0.0.0.0",
      "Port": 5010,
      "Scheme": "https"
    },
    "OpenApiServerPath": "https://localhost:5010",
    "MongoDB": {
      "ConnectionString": "mongodb://admin:admin123@localhost:27017"
    },
    "RabbitMQ": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "admin",
      "Password": "admin123",
      "VirtualHost": "/"
    },
    "CertificateSettings": {
      "DNS": "localhost",
      "CERT_FILE": "",
      "KEY_FILE": ""
    },
    "Actors": {
      "AuthService": "https://localhost:5001"
    },
    "History": {
      "MaxHistoryEntries": 50
    },
    "DeletedData": {
      "RetentionDays": 7
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

### 3. Program.cs'de Kullanım

```csharp
var settings = builder.Configuration.GetSection("YourProjectSettings")
    .Get<YourProjectSettings>();

if (settings == null)
{
    throw new InvalidOperationException("YourProjectSettings configuration is required!");
}
```

---

## 🔌 Dependency Injection

### Service Registration Pattern

Her katman kendi `ServiceRegistration.cs` dosyasına sahiptir.

#### Application Layer

```csharp
// Application/ServiceRegistration.cs
using Microsoft.Extensions.DependencyInjection;
using YourProject.Application.Configuration;
using MongoDB.Driver;

namespace YourProject.Application;

public static class ServiceRegistration
{
    public static void AddApplicationServices(
        this IServiceCollection services, 
        YourProjectSettings settings)
    {
        // Configuration
        services.Configure<YourProjectSettings>(_ =>
        {
            _.MongoDB = settings.MongoDB;
            _.RabbitMQ = settings.RabbitMQ;
            _.CertificateSettings = settings.CertificateSettings;
            _.OpenApiServerPath = settings.OpenApiServerPath;
            _.Actors = settings.Actors;
        });

        // MongoDB Client (Singleton)
        services.AddSingleton<IMongoClient>(provider =>
        {
            var connectionString = settings.MongoDB.ConnectionString 
                ?? "mongodb://localhost:27017";
            
            // MongoDB conventions
            var conventionPack = new ConventionPack
            {
                new StringObjectIdIdGeneratorConvention()
            };
            ConventionRegistry.Register(
                "YourProjectConventions",
                conventionPack,
                t => true);
            
            return new MongoClient(connectionString);
        });

        // MediatR (opsiyonel)
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssemblies(
                AppDomain.CurrentDomain.GetAssemblies());
        });
    }
}
```

#### Persistence Layer

```csharp
// Persistence/ServiceRegistration.cs
using Microsoft.Extensions.DependencyInjection;
using YourProject.Application.Services;
using YourProject.Persistence.Services;

namespace YourProject.Persistence;

public static class ServiceRegistration
{
    public static void AddPersistenceServices(this IServiceCollection services)
    {
        // Scoped services - Her request için yeni instance
        services.AddScoped<IMongoContextService, MongoContextService>();
        services.AddScoped<IUserInfoService, UserInfoService>();
        services.AddScoped<IYourService, YourService>();
        
        // Diğer servisler...
    }
}
```

#### Infrastructure Layer

```csharp
// Infrastructure/ServiceRegistration.cs
using Microsoft.Extensions.DependencyInjection;
using YourProject.Application.Services;
using YourProject.Infrastructure.Services.RabbitMq;

namespace YourProject.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services)
    {
        // Singleton services - Uygulama boyunca tek instance
        services.AddSingleton<IRabbitMqService, RabbitMqService>();
        
        return services;
    }
}
```

#### Program.cs'de Kayıt

```csharp
// Application, Infrastructure & Persistence Services
builder.Services.AddApplicationServices(settings);
builder.Services.AddInfrastructureServices();
builder.Services.AddPersistenceServices();
```

---

## 🏛️ Base Entity Pattern

### BaseEntity Sınıfı

```csharp
// Domain/Entities/Base/BaseEntity.cs
using MongoDB.Bson.Serialization.Attributes;

namespace YourProject.Domain.Entities.Base;

/// <summary>
/// Base entity class - Tüm entities için ortak metadata pattern
/// </summary>
[BsonIgnoreExtraElements]
public abstract class BaseEntity
{
    /// <summary>
    /// Unique identifier (GUID) - Backend otomatik oluşturur
    /// </summary>
    public string __dataId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Creation metadata - Token'dan alınır, hiç değişmez
    /// </summary>
    public CreateInfo __createInfo { get; set; } = null!;

    /// <summary>
    /// Last update metadata - Her update'te güncellenir
    /// </summary>
    public UpdateInfo? __lastUpdateInfo { get; set; }

    /// <summary>
    /// History - Self logging (MaxHistoryEntries ile sınırlı)
    /// </summary>
    public List<HistoryEntry> __history { get; set; } = new();
}

public class CreateInfo
{
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.DateTime)]
    public DateTime createdAt { get; set; }

    public UserInfo userInfo { get; set; } = null!;
}

public class UpdateInfo
{
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.DateTime)]
    public DateTime updatedAt { get; set; }

    public UserInfo userInfo { get; set; } = null!;
}

[BsonIgnoreExtraElements]
public class UserInfo
{
    public string uid { get; set; } = string.Empty;
    public string userName { get; set; } = string.Empty;
    public string domain { get; set; } = string.Empty;
}

public class HistoryEntry
{
    public string operation { get; set; } = string.Empty;
    
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc, Representation = BsonType.DateTime)]
    public DateTime timestamp { get; set; }

    public UserInfo userInfo { get; set; } = null!;
    public Dictionary<string, ChangeDetail>? changes { get; set; }
}

public class ChangeDetail
{
    public object? oldValue { get; set; }
    public object? newValue { get; set; }
}
```

### Entity Kullanımı

```csharp
// Domain/Entities/YourEntity.cs
using YourProject.Domain.Entities.Base;
using MongoDB.Bson.Serialization.Attributes;

namespace YourProject.Domain.Entities;

[BsonIgnoreExtraElements]
public class YourEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    // ... diğer alanlar
}
```

---

## 🔧 Service Pattern

### 1. Interface Tanımı (Application Layer)

```csharp
// Application/Services/IYourService.cs
namespace YourProject.Application.Services;

public interface IYourService
{
    Task<YourEntity> CreateAsync(CreateYourEntityDto dto);
    Task<YourEntity?> GetByIdAsync(string id);
    Task<List<YourEntity>> ListAsync(int skip = 0, int limit = 50);
    Task<YourEntity?> UpdateAsync(string id, UpdateYourEntityDto dto);
    Task<bool> DeleteAsync(string id);
}
```

### 2. Implementation (Persistence Layer)

```csharp
// Persistence/Services/YourService.cs
using MongoDB.Driver;
using YourProject.Application.Services;
using YourProject.Domain.Entities;
using YourProject.Domain.Exceptions;

namespace YourProject.Persistence.Services;

public class YourService : IYourService
{
    private readonly IMongoContextService _mongoContext;
    private readonly IUserInfoService _userInfo;
    private readonly ILogger<YourService> _logger;

    public YourService(
        IMongoContextService mongoContext,
        IUserInfoService userInfo,
        ILogger<YourService> logger)
    {
        _mongoContext = mongoContext;
        _userInfo = userInfo;
        _logger = logger;
    }

    public async Task<YourEntity> CreateAsync(CreateYourEntityDto dto)
    {
        var database = _mongoContext.GetDatabase();
        var collection = database.GetCollection<YourEntity>("your_collection");
        var userInfo = _userInfo.GetCurrentUserInfo();

        var entity = new YourEntity
        {
            Name = dto.Name,
            Description = dto.Description,
            __createInfo = new CreateInfo
            {
                createdAt = DateTime.UtcNow,
                userInfo = userInfo
            },
            __history = new List<HistoryEntry>
            {
                new HistoryEntry
                {
                    operation = "create",
                    timestamp = DateTime.UtcNow,
                    userInfo = userInfo
                }
            }
        };

        await collection.InsertOneAsync(entity);
        return entity;
    }

    // Diğer metodlar...
}
```

---

## 🎮 Controller Pattern

### Controller Örneği

```csharp
// Api/Controllers/YourController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YourProject.Application.DTOs.Common;
using YourProject.Application.DTOs.YourEntity;
using YourProject.Application.Services;
using YourProject.Domain.Exceptions;

namespace YourProject.Api.Controllers;

[ApiController]
[Route("api/your-entities")]
[Authorize]
public class YourController : ControllerBase
{
    private readonly ILogger<YourController> _logger;
    private readonly IYourService _service;
    private readonly IMongoContextService _mongoContext;

    public YourController(
        ILogger<YourController> logger,
        IYourService service,
        IMongoContextService mongoContext)
    {
        _logger = logger;
        _service = service;
        _mongoContext = mongoContext;
    }

    /// <summary>
    /// Create new entity
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DataResponseDto<YourEntityResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateYourEntityDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            
            return Ok(new DataResponseDto<YourEntityResponseDto>
            {
                Success = true,
                Data = MapToResponseDto(result),
                Meta = new ResponseMetaDto
                {
                    Timestamp = DateTime.UtcNow,
                    Path = $"/api/your-entities"
                }
            });
        }
        catch (YourProjectException ex) when (ex.ValidationErrors != null)
        {
            return BadRequest(new ErrorResponseDto
            {
                Success = false,
                Error = new ErrorDetailDto
                {
                    Code = "VALIDATION_ERROR",
                    Message = ex.Message,
                    Details = ex.ValidationErrors
                },
                Meta = new ResponseMetaDto
                {
                    Timestamp = DateTime.UtcNow,
                    Path = $"/api/your-entities"
                }
            });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ErrorResponseDto
            {
                Success = false,
                Error = new ErrorDetailDto
                {
                    Code = "NOT_FOUND",
                    Message = ex.Message
                },
                Meta = new ResponseMetaDto
                {
                    Timestamp = DateTime.UtcNow,
                    Path = $"/api/your-entities"
                }
            });
        }
    }

    // Diğer endpoint'ler...
}
```

---

## ⚠️ Exception Handling

### 1. Domain Exceptions

```csharp
// Domain/Exceptions/YourProjectException.cs
namespace YourProject.Domain.Exceptions;

public class YourProjectException : Exception
{
    public List<object>? ValidationErrors { get; set; }

    public YourProjectException(string message) : base(message) { }
    public YourProjectException(string message, Exception innerException) 
        : base(message, innerException) { }
}

public class ValidationException : YourProjectException
{
    public ValidationException(string message) : base(message) { }
}

public class NotFoundException : YourProjectException
{
    public NotFoundException(string message) : base(message) { }
}

public class UnauthorizedException : YourProjectException
{
    public UnauthorizedException(string message) : base(message) { }
}
```

### 2. Global Exception Middleware

```csharp
// Api/Middleware/GlobalExceptionHandlerMiddleware.cs
using System.Net;
using System.Text.Json;

namespace YourProject.Api.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next, 
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            ArgumentNullException => (HttpStatusCode.BadRequest, "Required argument is missing"),
            ArgumentException => (HttpStatusCode.BadRequest, "Invalid argument provided"),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Access denied"),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Invalid operation"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found"),
            TimeoutException => (HttpStatusCode.RequestTimeout, "Operation timed out"),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            StatusCode = statusCode,
            Message = message,
            Timestamp = DateTime.UtcNow,
            TraceId = context.TraceIdentifier,
            Path = context.Request.Path,
            Method = context.Request.Method
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}

public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
```

### 3. Program.cs'de Kullanım

```csharp
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
// veya
app.UseGlobalExceptionHandler();
```

---

## 🔐 Authentication & Authorization

### 1. JWT Configuration

```csharp
// Api/Config/Extensions.cs
public static void InitAuthentication(
    this WebApplicationBuilder builder, 
    YourProjectSettings settings)
{
    builder.Services.AddAuthentication("Bearer")
        .AddJwtBearer("Bearer", options =>
        {
            options.Authority = settings.Actors.AuthService;
            options.RequireHttpsMetadata = false;

            options.BackchannelHttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = delegate { return true; }
            };

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = false,
                SignatureValidator = delegate (string token, TokenValidationParameters parameters)
                {
                    var jwt = new JsonWebToken(token);
                    return jwt;
                }
            };
        });
}
```

### 2. Program.cs'de Kullanım

```csharp
builder.InitAuthentication(settings);

// ...

app.UseAuthentication();
app.UseAuthorization();
```

### 3. Controller'da Kullanım

```csharp
[Authorize] // Tüm endpoint'ler için
public class YourController : ControllerBase
{
    // ...
}

// veya belirli endpoint'ler için
[Authorize]
[HttpPost]
public async Task<IActionResult> Create(...) { }
```

---

## 🗄️ MongoDB Context Service

### Interface

```csharp
// Application/Services/IMongoContextService.cs
using MongoDB.Driver;

namespace YourProject.Application.Services;

public interface IMongoContextService
{
    IMongoDatabase GetDatabase();
    IMongoDatabase GetDatabase(string domainName);
    string? GetCurrentDomainName();
    string? GetCurrentUserId();
    string? GetCurrentUsername();
    bool IsCurrentUserAdmin();
}
```

### Implementation

```csharp
// Persistence/Services/MongoContextService.cs
using MongoDB.Driver;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace YourProject.Persistence.Services;

public class MongoContextService : IMongoContextService
{
    private readonly IMongoClient _mongoClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MongoContextService(
        IMongoClient mongoClient, 
        IHttpContextAccessor httpContextAccessor)
    {
        _mongoClient = mongoClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public IMongoDatabase GetDatabase()
    {
        var domainName = GetCurrentDomainName();
        
        if (string.IsNullOrWhiteSpace(domainName))
        {
            throw new InvalidOperationException(
                "JWT token'da domain_name claim'i bulunamadı.");
        }

        return GetDatabase(domainName);
    }

    public IMongoDatabase GetDatabase(string domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            throw new ArgumentException("Domain name boş olamaz.", nameof(domainName));
        }

        // Database adı: {prefix}_{domain_name} formatında
        var databaseName = $"yourprefix_{domainName}";
        
        return _mongoClient.GetDatabase(databaseName);
    }

    public string? GetCurrentDomainName()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirst("domain_name")?.Value;
    }

    public string? GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? user?.FindFirst("sub")?.Value;
    }

    public string? GetCurrentUsername()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirst("preferred_username")?.Value;
    }

    public bool IsCurrentUserAdmin()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirst("isAdmin")?.Value == "true";
    }
}
```

### UserInfoService

```csharp
// Application/Services/IUserInfoService.cs
using YourProject.Domain.Entities.Base;

namespace YourProject.Application.Services;

public interface IUserInfoService
{
    UserInfo GetCurrentUserInfo();
}

// Persistence/Services/UserInfoService.cs
public class UserInfoService : IUserInfoService
{
    private readonly IMongoContextService _mongoContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserInfoService(
        IMongoContextService mongoContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _mongoContext = mongoContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public UserInfo GetCurrentUserInfo()
    {
        var userId = _mongoContext.GetCurrentUserId() 
            ?? throw new UnauthorizedAccessException("User ID not found");
        var username = _mongoContext.GetCurrentUsername() 
            ?? throw new UnauthorizedAccessException("Username not found");
        var domain = _mongoContext.GetCurrentDomainName() 
            ?? throw new UnauthorizedAccessException("Domain not found");

        return new UserInfo
        {
            uid = userId,
            userName = username,
            domain = domain
        };
    }
}
```

---

## 📨 Event Publishing Pattern

### RabbitMQ Service Interface

```csharp
// Application/Services/IRabbitMqService.cs
namespace YourProject.Application.Services;

public interface IRabbitMqService
{
    Task ConnectAsync();
    Task EnsureExchangeAsync(string domainName);
    Task PublishAsync(
        string exchange, 
        string routingKey, 
        object payload, 
        Dictionary<string, object>? headers = null);
    Task PublishWithRetryAsync(
        string exchange, 
        string routingKey, 
        object payload, 
        int maxRetries = 3);
}
```

### Implementation

```csharp
// Infrastructure/Services/RabbitMq/RabbitMqService.cs
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace YourProject.Infrastructure.Services.RabbitMq;

public class RabbitMqService : IRabbitMqService, IDisposable
{
    private readonly YourProjectSettings _settings;
    private readonly ILogger<RabbitMqService> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqService(
        IOptions<YourProjectSettings> settings,
        ILogger<RabbitMqService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ConnectAsync()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.RabbitMQ.Host,
                Port = _settings.RabbitMQ.Port,
                UserName = _settings.RabbitMQ.Username,
                Password = _settings.RabbitMQ.Password,
                VirtualHost = _settings.RabbitMQ.VirtualHost
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
            _logger.LogInformation("RabbitMQ connected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to RabbitMQ");
            throw;
        }
    }

    public async Task EnsureExchangeAsync(string domainName)
    {
        if (_channel == null)
        {
            await ConnectAsync();
        }

        var exchangeName = $"yourproject.events.{domainName}";
        
        _channel.ExchangeDeclare(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);
    }

    public async Task PublishAsync(
        string exchange, 
        string routingKey, 
        object payload, 
        Dictionary<string, object>? headers = null)
    {
        if (_channel == null || !_channel.IsOpen)
        {
            await ConnectAsync();
        }

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        if (headers != null)
        {
            properties.Headers = headers;
        }

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        
        _channel.BasicPublish(
            exchange: exchange,
            routingKey: routingKey,
            basicProperties: properties,
            body: body);

        _logger.LogInformation(
            "Event published: {Exchange} / {RoutingKey}", 
            exchange, routingKey);
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
```

---

## 🚀 Adım Adım Kurulum

### 1. Proje Oluşturma

```bash
# Solution oluştur
dotnet new sln -n YourProject

# Domain projesi
dotnet new classlib -n YourProject.Domain -f net9.0
dotnet sln add YourProject.Domain/YourProject.Domain.csproj

# Application projesi
dotnet new classlib -n YourProject.Application -f net9.0
dotnet sln add YourProject.Application/YourProject.Application.csproj
dotnet add YourProject.Application/YourProject.Application.csproj reference YourProject.Domain/YourProject.Domain.csproj

# Persistence projesi
dotnet new classlib -n YourProject.Persistence -f net9.0
dotnet sln add YourProject.Persistence/YourProject.Persistence.csproj
dotnet add YourProject.Persistence/YourProject.Persistence.csproj reference YourProject.Application/YourProject.Application.csproj
dotnet add YourProject.Persistence/YourProject.Persistence.csproj reference YourProject.Domain/YourProject.Domain.csproj

# Infrastructure projesi
dotnet new classlib -n YourProject.Infrastructure -f net9.0
dotnet sln add YourProject.Infrastructure/YourProject.Infrastructure.csproj
dotnet add YourProject.Infrastructure/YourProject.Infrastructure.csproj reference YourProject.Application/YourProject.Application.csproj
dotnet add YourProject.Infrastructure/YourProject.Infrastructure.csproj reference YourProject.Domain/YourProject.Domain.csproj

# API projesi
dotnet new webapi -n YourProject.Api -f net9.0
dotnet sln add YourProject.Api/YourProject.Api.csproj
dotnet add YourProject.Api/YourProject.Api.csproj reference YourProject.Application/YourProject.Application.csproj
dotnet add YourProject.Api/YourProject.Api.csproj reference YourProject.Infrastructure/YourProject.Infrastructure.csproj
dotnet add YourProject.Api/YourProject.Api.csproj reference YourProject.Persistence/YourProject.Persistence.csproj
```

### 2. NuGet Paketleri

```bash
# Domain (genellikle paket gerekmez)
# Application
cd YourProject.Application
dotnet add package MongoDB.Driver
dotnet add package MediatR

# Persistence
cd ../YourProject.Persistence
dotnet add package MongoDB.Driver
dotnet add package Microsoft.Extensions.Http

# Infrastructure
cd ../YourProject.Infrastructure
dotnet add package RabbitMQ.Client

# API
cd ../YourProject.Api
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.Seq
dotnet add package Swashbuckle.AspNetCore
dotnet add package Scalar.AspNetCore
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

### 3. Klasör Yapısı Oluşturma

```bash
# Domain
mkdir -p YourProject.Domain/Entities/Base
mkdir -p YourProject.Domain/Exceptions

# Application
mkdir -p YourProject.Application/Configuration
mkdir -p YourProject.Application/DTOs/Common
mkdir -p YourProject.Application/Services

# Persistence
mkdir -p YourProject.Persistence/Services

# Infrastructure
mkdir -p YourProject.Infrastructure/Services/RabbitMq

# API
mkdir -p YourProject.Api/Config
mkdir -p YourProject.Api/Controllers
mkdir -p YourProject.Api/Middleware
```

### 4. Program.cs Yapılandırması

```csharp
// Program.cs
using YourProject.Api.Config;
using YourProject.Application;
using YourProject.Application.Configuration;
using YourProject.Infrastructure;
using YourProject.Persistence;
using System.Security.Cryptography.X509Certificates;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables
builder.Configuration.AddEnvironmentVariables();

var settings = builder.Configuration.GetSection("YourProjectSettings")
    .Get<YourProjectSettings>();

if (settings == null)
{
    throw new InvalidOperationException(
        "YourProjectSettings configuration is required!");
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
builder.InitAuthentication(settings);

// HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Application, Infrastructure & Persistence Services
builder.Services.AddApplicationServices(settings);
builder.Services.AddInfrastructureServices();
builder.Services.AddPersistenceServices();

var app = builder.Build();

// Initialize RabbitMQ connection
try
{
    var rabbitMqService = app.Services.GetRequiredService<IRabbitMqService>();
    await rabbitMqService.ConnectAsync();
    Log.Information("RabbitMQ connection initialized");
}
catch (Exception ex)
{
    Log.Warning(ex, "Failed to connect to RabbitMQ on startup");
}

app.UseApplicationSettings(settings);

try
{
    Log.Information("Starting YourProject API");
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

## 📝 Örnek Implementasyon

### Senaryo: Task Management Service

#### 1. Domain Entity

```csharp
// Domain/Entities/Task.cs
using YourProject.Domain.Entities.Base;
using MongoDB.Bson.Serialization.Attributes;

namespace YourProject.Domain.Entities;

[BsonIgnoreExtraElements]
public class Task : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? DueDate { get; set; }
}
```

#### 2. DTOs

```csharp
// Application/DTOs/Task/CreateTaskDto.cs
namespace YourProject.Application.DTOs.Task;

public class CreateTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime? DueDate { get; set; }
}

// Application/DTOs/Task/TaskResponseDto.cs
public class TaskResponseDto
{
    public string __dataId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

#### 3. Service Interface

```csharp
// Application/Services/ITaskService.cs
using YourProject.Application.DTOs.Task;

namespace YourProject.Application.Services;

public interface ITaskService
{
    Task<TaskResponseDto> CreateAsync(CreateTaskDto dto);
    Task<TaskResponseDto?> GetByIdAsync(string id);
    Task<(List<TaskResponseDto> tasks, long totalCount)> ListAsync(
        int skip = 0, int limit = 50);
    Task<TaskResponseDto?> UpdateAsync(string id, UpdateTaskDto dto);
    Task<bool> DeleteAsync(string id);
}
```

#### 4. Service Implementation

```csharp
// Persistence/Services/TaskService.cs
using MongoDB.Driver;
using YourProject.Application.DTOs.Task;
using YourProject.Application.Services;
using YourProject.Domain.Entities;
using YourProject.Domain.Exceptions;

namespace YourProject.Persistence.Services;

public class TaskService : ITaskService
{
    private readonly IMongoContextService _mongoContext;
    private readonly IUserInfoService _userInfo;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        IMongoContextService mongoContext,
        IUserInfoService userInfo,
        ILogger<TaskService> logger)
    {
        _mongoContext = mongoContext;
        _userInfo = userInfo;
        _logger = logger;
    }

    public async Task<TaskResponseDto> CreateAsync(CreateTaskDto dto)
    {
        var database = _mongoContext.GetDatabase();
        var collection = database.GetCollection<Task>("tasks");
        var userInfo = _userInfo.GetCurrentUserInfo();

        var task = new Task
        {
            Title = dto.Title,
            Description = dto.Description,
            Priority = dto.Priority,
            IsCompleted = false,
            DueDate = dto.DueDate,
            __createInfo = new CreateInfo
            {
                createdAt = DateTime.UtcNow,
                userInfo = userInfo
            },
            __history = new List<HistoryEntry>
            {
                new HistoryEntry
                {
                    operation = "create",
                    timestamp = DateTime.UtcNow,
                    userInfo = userInfo
                }
            }
        };

        await collection.InsertOneAsync(task);
        return MapToResponseDto(task);
    }

    public async Task<TaskResponseDto?> GetByIdAsync(string id)
    {
        var database = _mongoContext.GetDatabase();
        var collection = database.GetCollection<Task>("tasks");
        
        var task = await collection
            .Find(t => t.__dataId == id)
            .FirstOrDefaultAsync();

        return task == null ? null : MapToResponseDto(task);
    }

    public async Task<(List<TaskResponseDto> tasks, long totalCount)> ListAsync(
        int skip = 0, int limit = 50)
    {
        var database = _mongoContext.GetDatabase();
        var collection = database.GetCollection<Task>("tasks");

        var totalCount = await collection.CountDocumentsAsync(_ => true);
        
        var tasks = await collection
            .Find(_ => true)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();

        return (tasks.Select(MapToResponseDto).ToList(), totalCount);
    }

    // Update, Delete metodları...

    private TaskResponseDto MapToResponseDto(Task task)
    {
        return new TaskResponseDto
        {
            __dataId = task.__dataId,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            IsCompleted = task.IsCompleted,
            DueDate = task.DueDate,
            CreatedAt = task.__createInfo.createdAt
        };
    }
}
```

#### 5. Controller

```csharp
// Api/Controllers/TasksController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YourProject.Application.DTOs.Common;
using YourProject.Application.DTOs.Task;
using YourProject.Application.Services;
using YourProject.Domain.Exceptions;

namespace YourProject.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        ITaskService taskService,
        ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskDto dto)
    {
        try
        {
            var result = await _taskService.CreateAsync(dto);
            
            return Ok(new DataResponseDto<TaskResponseDto>
            {
                Success = true,
                Data = result,
                Meta = new ResponseMetaDto
                {
                    Timestamp = DateTime.UtcNow,
                    Path = "/api/tasks"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            return StatusCode(500, new ErrorResponseDto
            {
                Success = false,
                Error = new ErrorDetailDto
                {
                    Code = "INTERNAL_ERROR",
                    Message = ex.Message
                }
            });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var task = await _taskService.GetByIdAsync(id);
        
        if (task == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Success = false,
                Error = new ErrorDetailDto
                {
                    Code = "NOT_FOUND",
                    Message = "Task not found"
                }
            });
        }

        return Ok(new DataResponseDto<TaskResponseDto>
        {
            Success = true,
            Data = task
        });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int skip = 0, 
        [FromQuery] int limit = 50)
    {
        var (tasks, totalCount) = await _taskService.ListAsync(skip, limit);
        
        return Ok(new PagedResultDto<TaskResponseDto>
        {
            Success = true,
            Data = tasks,
            Meta = new PagedMetaDto
            {
                TotalCount = totalCount,
                Skip = skip,
                Limit = limit,
                HasMore = skip + limit < totalCount
            }
        });
    }
}
```

---

## ✅ Checklist

### Proje Oluşturma
- [ ] Solution ve projeler oluşturuldu
- [ ] Proje referansları ayarlandı
- [ ] NuGet paketleri yüklendi

### Domain Layer
- [ ] BaseEntity sınıfı oluşturuldu
- [ ] Entity sınıfları oluşturuldu
- [ ] Exception sınıfları oluşturuldu

### Application Layer
- [ ] Settings class oluşturuldu
- [ ] Service interface'leri tanımlandı
- [ ] DTOs oluşturuldu
- [ ] ServiceRegistration.cs oluşturuldu

### Persistence Layer
- [ ] Service implementasyonları yapıldı
- [ ] MongoContextService implementasyonu
- [ ] UserInfoService implementasyonu
- [ ] ServiceRegistration.cs oluşturuldu

### Infrastructure Layer
- [ ] RabbitMQ service implementasyonu
- [ ] ServiceRegistration.cs oluşturuldu

### API Layer
- [ ] Program.cs yapılandırıldı
- [ ] Extensions.cs oluşturuldu
- [ ] GlobalExceptionHandlerMiddleware oluşturuldu
- [ ] Controllers oluşturuldu
- [ ] appsettings.json yapılandırıldı

### Test
- [ ] Uygulama çalışıyor
- [ ] Swagger erişilebilir
- [ ] Authentication çalışıyor
- [ ] CRUD operasyonları test edildi

---

## 📚 Ek Kaynaklar

### Önemli Dosyalar
- `Program.cs` - Uygulama başlangıç noktası
- `ServiceRegistration.cs` - DI kayıtları
- `BaseEntity.cs` - Ortak entity pattern
- `MongoContextService.cs` - Multi-tenant database seçimi
- `GlobalExceptionHandlerMiddleware.cs` - Global hata yönetimi

### Best Practices
1. **Her katman kendi ServiceRegistration.cs'ine sahip olmalı**
2. **Interface'ler Application layer'da, implementasyonlar Persistence/Infrastructure'da**
3. **Domain layer hiçbir katmana bağımlı olmamalı**
4. **Controller'lar sadece HTTP işlemlerini yönetmeli, iş mantığı service'lerde olmalı**
5. **Exception handling her seviyede yapılmalı**

---

## 🎯 Sonuç

Bu döküman, **MngDataGateway** mimarisine benzer bir mikroservis oluşturmak için gereken tüm bilgileri içermektedir. Adım adım takip ederek, production-ready bir uygulama geliştirebilirsiniz.

**Sorularınız için:** Bu dökümanı referans olarak kullanarak, benzer özelliklerde yeni projeler oluşturabilirsiniz.

---

**Hazırlayan:** AI Assistant  
**Tarih:** 2025  
**Versiyon:** 1.0

