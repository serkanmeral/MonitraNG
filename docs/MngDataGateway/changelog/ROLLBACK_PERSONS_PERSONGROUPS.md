# Rollback: persons/personGroups Implementation

**Date:** 10 Aralık 2025  
**Reason:** Yaklaşım değişikliği - Sync işlemi MngKeeper'a taşınıyor

---

## 🔄 Değişiklik Kararı

**Eski Yaklaşım (Yanlış):**
- MngDataGateway → RabbitMQ event consumer
- MngDataGateway → MongoDB sync
- Event-driven architecture

**Yeni Yaklaşım (Doğru):**
- MngKeeper → Direkt MongoDB sync (mng_{domain} DB)
- MngKeeper → Custom data desteği
- RabbitMQ sadece notification için

---

## 🗑️ MngDataGateway'den Kaldırılacaklar

### 1. Event Consumer Service
- ❌ `Infrastructure/MngDataGateway.Infrastructure/Services/MngKeeperEventConsumer.cs`
- ❌ `Core/MngDataGateway.Application/Services/IMngKeeperEventConsumer.cs`
- ❌ `Core/MngDataGateway.Application/Services/IMngKeeperEventHandler.cs`
- ❌ `Infrastructure/MngDataGateway.Persistence/Services/MngKeeperEventHandler.cs`

### 2. Sync Service
- ❌ `Infrastructure/MngDataGateway.Persistence/Services/MngKeeperSyncService.cs`
- ❌ `Core/MngDataGateway.Application/Services/IMngKeeperSyncService.cs`
- ❌ `Presentation/MngDataGateway.Api/Controllers/SyncController.cs`

### 3. Event DTOs
- ❌ `Core/MngDataGateway.Application/DTOs/Events/MngKeeperEventDto.cs`

### 4. Service Registrations
- ❌ `Infrastructure/MngDataGateway.Infrastructure/ServiceRegistration.cs` → Event consumer registration
- ❌ `Infrastructure/MngDataGateway.Persistence/ServiceRegistration.cs` → Sync service registration

---

## ✅ Korunacaklar

### 1. Domain Entities
- ✅ `Core/MngDataGateway.Domain/Entities/UserSync.cs` → MngKeeper'da da kullanılabilir
- ✅ `Core/MngDataGateway.Domain/Entities/GroupSync.cs` → MngKeeper'da da kullanılabilir

### 2. Domain Lookup Service
- ✅ `Infrastructure/MngDataGateway.Persistence/Services/DomainLookupService.cs` → Başka amaçlar için kullanılabilir
- ✅ `Core/MngDataGateway.Application/Services/IDomainLookupService.cs`

### 3. Aggregate Pipeline Expansion
- ✅ `Infrastructure/MngDataGateway.Persistence/Services/AggregatePipelineBuilder.cs` → `AddPersonExpansion()` method'u
- ✅ `Infrastructure/MngDataGateway.Persistence/Services/DataService.cs` → Person expansion çağrıları

**Not:** persons/personGroups field expansion çalışmaya devam edecek, sadece sync mekanizması değişiyor.

---

## 📋 Rollback Checklist

- [ ] MngKeeperEventConsumer.cs sil
- [ ] IMngKeeperEventConsumer.cs sil
- [ ] IMngKeeperEventHandler.cs sil
- [ ] MngKeeperEventHandler.cs sil
- [ ] MngKeeperSyncService.cs sil
- [ ] IMngKeeperSyncService.cs sil
- [ ] SyncController.cs sil
- [ ] MngKeeperEventDto.cs sil
- [ ] Service registration'lardan kaldır
- [ ] Program.cs'den HttpClient registration kaldır (eğer sadece sync için kullanılıyorsa)

---

## 🎯 Sonuç

**Korunan:**
- ✅ persons/personGroups field expansion ($lookup)
- ✅ Domain lookup service (başka amaçlar için)
- ✅ UserSync/GroupSync entities (MngKeeper'da kullanılabilir)

**Kaldırılan:**
- ❌ Event consumer
- ❌ Sync service
- ❌ Sync controller

**Yeni Lokasyon:**
- ✅ Sync işlemi → MngKeeper'a taşınıyor
- ✅ Roadmap: `MngKeeper/DATAGATEWAY_SYNC_ROADMAP.md`

---

**Son Güncelleme:** 10 Aralık 2025  
**Status:** Rollback planı hazır

