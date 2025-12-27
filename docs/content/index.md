# MonitraNG Documentation

Welcome to the MonitraNG documentation! 🚀

MonitraNG is a modern, multi-tenant IoT monitoring and management platform built with Clean Architecture, CQRS pattern, and Keycloak-based authentication.

## 🏗️ Architecture

MonitraNG consists of multiple microservices:

- **MngKeeper** - Authorization & Multi-tenant Management Service
- **MngDataGateway** - Dynamic Data Gateway for MongoDB
- **MngHub** - Real-time Event Hub with SignalR
- **MngReactor** - Main Business Logic Service
- **MngEngine** - Data Collection Engine
- **Mng.Ui** - Frontend (Nuxt 3 + Vuetify)

## 🚀 Quick Start

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Git](https://git-scm.com/)

### Installation

1. **Clone the repository:**
   ```bash
   git clone https://github.com/serkanmeral/MonitraNG.git
   cd MonitraNG
   ```

2. **Start Docker containers:**
   ```bash
   cd ApplicationResources/mng_common
   docker-compose up -d
   ```

3. **Start backend services:**
   ```bash
   # MngKeeper
   cd ../../MngKeeper/Presentation/MngKeeper.Api
   dotnet run

   # MngDataGateway
   cd ../../MngDataGateway/Presentation/MngDataGateway.Api
   dotnet run

   # MngHub
   cd ../../MngHub/Presentation/MngHub.Api
   dotnet run
   ```

## 📚 Documentation Structure

- **[User Guide](user-guide/getting-started.md)** - Getting started, installation, configuration
- **[API Documentation](api/overview.md)** - Complete API reference for all services
- **[Services](services/)** - Architecture and roadmap for each service
- **[Development](development/contributing.md)** - Contributing guidelines and coding standards

## 🔗 Quick Links

- [GitHub Repository](https://github.com/serkanmeral/MonitraNG)
- [API Documentation (MngKeeper)](http://localhost:5001/api-docs)
- [API Documentation (MngDataGateway)](http://localhost:5010/api-docs)
- [API Documentation (MngHub)](http://localhost:5020/api-docs)

## 📖 What's Next?

- Read the [Getting Started Guide](user-guide/getting-started.md)
- Explore the [API Documentation](api/overview.md)
- Check out the [Architecture Guide](services/mngkeeper/architecture.md)

---

**Need help?** Open an issue on [GitHub](https://github.com/serkanmeral/MonitraNG/issues) or contact [serkan.meral@isimplatform.io](mailto:serkan.meral@isimplatform.io)

