# MngNotifier

Centralized Notification Service for MonitraNG Platform

## 🎯 Overview

**MngNotifier** is a centralized notification service that manages all notification operations for the MonitraNG platform. Initially starting with email notifications, it will be extended to support SMS, WhatsApp, Slack, and other notification channels in the future.

## 🏗️ Architecture

Clean Architecture implementation with the following layers:

```
MngNotifier/
├── Core/
│   ├── MngNotifier.Domain/          # Domain entities, exceptions
│   └── MngNotifier.Application/     # Interfaces, configurations, DTOs
├── Infrastructure/
│   ├── MngNotifier.Infrastructure/  # MongoDB, RabbitMQ, Mail services
│   └── MngNotifier.Persistence/     # Repositories
└── Presentation/
    └── MngNotifier.Api/             # API controllers, middleware
```

## 🚀 Features

- ✅ Clean Architecture
- ✅ Health Check endpoints
- ✅ Version service (API versioning + application versioning)
- ✅ Swagger UI documentation
- ✅ Scalar API Reference (modern API documentation)
- ✅ IOptions<> pattern for configuration
- ✅ Serilog logging (Console + Seq)
- ✅ Global exception handler
- ✅ HTTPS support
- 🔄 Mail notification (planned)
  - Direct API endpoint
  - RabbitMQ event consumer
- 🔄 Template management (planned)
  - Template CRUD operations
  - Template-based mail sending
  - Placeholder replacement
- 🔄 Multi-channel support (SMS, WhatsApp, Slack - planned)

## 🔧 Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://admin:admin123@localhost:27017"
  },
  "MngNotifierSettings": {
    "MongoDB": {
      "DatabaseName": "mngnotifier"
    },
    "RabbitMQ": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "admin",
      "Password": "admin123",
      "VirtualHost": "/"
    },
    "Mail": {
      "Provider": "SMTP",
      "Smtp": {
        "Host": "smtp.gmail.com",
        "Port": 587,
        "Username": "",
        "Password": "",
        "EnableSsl": true
      }
    }
  }
}
```

## 🏃 Running the Application

```bash
cd Presentation/MngNotifier.Api
dotnet run
```

Application will start on:
- HTTP: http://localhost:5030
- HTTPS: https://localhost:5031

## 📚 API Documentation

- Swagger UI: http://localhost:5030/swagger
- Scalar API Reference: http://localhost:5030/scalar/v1
- OpenAPI JSON: http://localhost:5030/api-docs/v1/swagger.json

## 🔍 Endpoints

### Health Check
- `GET /api/v1/health` - Get comprehensive health status
- `GET /api/v1/health/live` - Liveness probe
- `GET /api/v1/health/ready` - Readiness probe

### Version
- `GET /api/v1/version` - Get detailed version information
- `GET /api/v1/version/short` - Get simple version string

### Notifications (Planned)
- `POST /api/v1/notifications/send` - Send direct mail
- `POST /api/v1/notifications/send-template` - Send mail using template
- `GET /api/v1/notifications/{id}` - Get notification status
- `GET /api/v1/notifications` - List notifications

### Templates (MngDataGateway API)

Template'ler MngDataGateway'in dataset ve data endpoint'leri kullanılarak yönetilir:
- Dataset: `@mail_templates`
- `GET /api/v1/data/@mail_templates` - List templates (MngDataGateway)
- `POST /api/v1/data/@mail_templates` - Create template (MngDataGateway)
- `PUT /api/v1/data/@mail_templates/{__dataId}` - Update template (MngDataGateway)
- `DELETE /api/v1/data/@mail_templates/{__dataId}` - Delete template (MngDataGateway)

## 📦 Dependencies

- .NET 9.0
- MongoDB.Driver 3.3.0
- RabbitMQ.Client 7.0.0
- Serilog 8.0.0
- Asp.Versioning.Mvc 8.1.0
- Asp.Versioning.Mvc.ApiExplorer 8.1.0
- Swashbuckle.AspNetCore (Swagger)
- Scalar.AspNetCore 2.5.1
- FluentValidation 11.3.1
- MediatR 13.0.0 (optional)

## 🔐 Authentication

JWT token-based authentication (to be implemented based on roadmap)

## 📄 License

Copyright © 2026 MonitraNG

## 👤 Author

Serkan MERAL - serkan.meral@isimplatform.io

## 📋 Roadmap

For detailed development roadmap, see [ROADMAP.md](./ROADMAP.md)
