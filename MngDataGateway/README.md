# MngDataGateway

Dynamic Data Gateway for MongoDB with Schema Management

## 🏗️ Architecture

Clean Architecture implementation with the following layers:

```
MngDataGateway/
├── Core/
│   ├── MngDataGateway.Domain/          # Domain entities, exceptions
│   └── MngDataGateway.Application/     # Interfaces, configurations, DTOs
├── Infrastructure/
│   ├── MngDataGateway.Infrastructure/  # MongoDB, RabbitMQ services
│   └── MngDataGateway.Persistence/     # Repositories
└── Presentation/
    └── MngDataGateway.Api/             # API controllers, middleware
```

## 🚀 Features

- ✅ Clean Architecture
- ✅ IOptions<> pattern for configuration
- ✅ Serilog logging (Console + Seq)
- ✅ Global exception handler
- ✅ HTTPS support
- ✅ Swagger documentation
- ✅ Version endpoint (`/api/version`)

## 🔧 Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://admin:admin123@localhost:27017"
  },
  "MongoDB": {
    "DatabaseName": "mngdatagateway"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "admin",
    "Password": "admin123",
    "VirtualHost": "/"
  }
}
```

## 🏃 Running the Application

```bash
cd Presentation/MngDataGateway.Api
dotnet run
```

Application will start on:
- HTTP: http://localhost:5010
- HTTPS: https://localhost:5011

## 📚 API Documentation

- Swagger UI: http://localhost:5010/swagger

## 🔍 Endpoints

### Version
- `GET /api/version` - Get detailed version information
- `GET /api/version/short` - Get simple version string

## 📦 Dependencies

- .NET 9.0
- MongoDB.Driver 3.3.0
- RabbitMQ.Client 7.0.0
- Serilog 8.0.0
- MediatR 13.0.0
- FluentValidation 11.3.1

## 🔐 Authentication

JWT token-based authentication (to be implemented based on roadmap)

## 📄 License

Copyright © 2025 MonitraNG

## 👤 Author

Serkan MERAL - serkan.meral@isimplatform.io

