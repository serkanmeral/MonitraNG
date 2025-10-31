# Changelog

Tüm önemli değişiklikler bu dosyada dokümante edilir.

Format [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) standardına uygundur.
Versiyonlama [Semantic Versioning](https://semver.org/spec/v2.0.0.html) kullanır.

## [Unreleased]

## [1.0.0] - 2025-10-31

### Added
- **MngKeeper** - Multi-tenant management service
  - Clean Architecture implementation (CQRS + MediatR)
  - Domain CRUD operations
  - User CRUD operations  
  - Group CRUD operations
  - Keycloak integration
  - MongoDB multi-database support
  
- **Authentication & Authorization**
  - JWT token authentication
  - Refresh token support with rotation
  - Token revocation (logout)
  - Domain-based claims (domain_id, domain_name, is_admin)
  - Role-based access control
  
- **API Documentation**
  - Swagger UI (http://localhost:5001/api-docs)
  - Scalar UI (http://localhost:5001/scalar/v1)
  - GraphQL support (http://localhost:5001/graphql)
  - 38+ RESTful endpoints
  
- **Logging & Monitoring**
  - Serilog structured logging
  - Seq integration (http://localhost:5341)
  - Console logging with colors
  - Environment-specific configurations
  - Health check endpoints
  
- **Infrastructure Services**
  - Keycloak service (realm, user, group management)
  - MQTT service integration
  - RabbitMQ event publisher
  - Redis cache & session service
  - JWT token service
  
- **Testing**
  - PowerShell test scripts
  - Domain creation tests
  - Authentication flow tests
  - Refresh token tests
  - Test data fixtures

### Changed
- Test controllers removed (7 test controllers cleaned up)
- Test endpoints updated to production endpoints
- Documentation updated with real API endpoints

### Fixed
- Swagger schema ID conflicts resolved
- Token expiration calculation
- Domain realm name resolution

### Technical Details
- **.NET Version:** 9.0
- **MongoDB:** 7.0
- **Keycloak:** 23.0.3
- **Architecture:** Clean Architecture
- **Pattern:** CQRS + MediatR
- **Database:** MongoDB (multi-tenant)
- **Authentication:** Keycloak + JWT

### Breaking Changes
- None (initial release)

### Deprecated
- Test controllers (removed)

### Security
- JWT tokens with domain claims
- Refresh token rotation
- Token revocation support
- Input validation
- Global exception handling

---

## Version History Summary

| Version | Date | Description |
|---------|------|-------------|
| 1.0.0 | 2025-10-31 | Initial production release |

