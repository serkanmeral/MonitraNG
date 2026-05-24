# El değişimi — MngScheduler (K3) ✅ tamamlandı

**Tarih:** 23 Mayıs 2026  
**Durum:** Kod + Odak deploy + sunucuda periyodik sync doğrulandı  
**Sonraki chat:** [HANDOFF_UI.md](./HANDOFF_UI.md) (K1.6 / K5 — Mng.Ui)

---

## K3 — tamamlanan özet

| Bileşen | Detay |
|---------|--------|
| **Orchestration** | `DirectorySyncOrchestrationJob`, `DirectorySyncOrchestrationService`, `MngKeeperDirectorySyncClient` |
| **System job** | `system-directory-sync-all-domains` → `@scheduled_jobs` (Mongo `mngkeeper`) |
| **Cron (üretim)** | `0 0/30 * * * ?` — her 30 dakika (Quartz: dakika alanı `0/30`, saniye `0`) |
| **Endpoint** | `orchestration://directory-sync` (HttpJob değil) |
| **Keeper çağrısı** | `POST /api/directory/sync` body `{ "domainId": "<realm>", "triggeredBy": 1 }` (enum sayısal) |
| **Domain listesi** | `DomainLookupService` — `status` Active: string `"Active"` veya int `1` |
| **Deploy** | `sync-odak-source.ps1 -Paths MngScheduler` + `deploy-odak-apps.ps1 -Services mngscheduler` |
| **Sunucu** | `http://192.168.20.20:5090` — container `mngscheduler:latest` |

### Düzeltmeler (oturumda)

| Sorun | Çözüm |
|--------|--------|
| Keeper HTTP 400 | `triggeredBy` string değil **`1`** (Scheduled) |
| DataGateway HttpClient retry | Her Polly denemesinde yeni `HttpRequestMessage` |
| `0/30 * * * * ?` | Quartz’ta **30 saniye**; üretim: **`0 0/30 * * * ?`** |
| DataGateway 401 log gürültüsü | `JobSync.SyncUserJobs=false` (user job için service JWT yok) |

### Seed / test scriptleri

| Dosya | Açıklama |
|-------|----------|
| [system-directory-sync-all-domains.job.json](../../../scripts/tests/MngScheduler/system-directory-sync-all-domains.job.json) | Mongo job şablonu |
| [seed-directory-sync-system-job.ps1](../../../scripts/tests/MngScheduler/seed-directory-sync-system-job.ps1) | mongosh upsert |
| [enable-backup-test.ps1](../../../scripts/tests/MngScheduler/enable-backup-test.ps1) | Backup test (ertelendi; job pasif) |

---

## Tamamlanan LDAP yığını (K1–K4 + K3)

| Kod | Özet |
|-----|------|
| **K1** | Keycloak AD federation, odak realm |
| **K2** | `POST /api/directory/sync`, 409 coordinator |
| **P0** | `directoryPrivileges`, JWT claims |
| **K4** | Login tek kullanıcı sync |
| **K3** | Periyodik sync — MngScheduler → Keeper |
| **Deploy** | Keeper **v1.3.0** + Scheduler K3 image @ `192.168.20.20` |

---

## Ortam sabitleri

| Bileşen | Adres |
|---------|--------|
| Sunucu | `192.168.20.20` |
| MngScheduler | http://192.168.20.20:5090 |
| MngKeeper (container içi) | `http://mngkeeper:5001` |
| MngKeeper (host) | http://192.168.20.20:5001 |
| Gateway | http://192.168.20.20:5040/keeper/api/... |

Keeper’da Quartz **yok**. Periyodik iş yalnızca **MngScheduler**.

---

## Deploy komutları (hatırlatma)

```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths MngScheduler
.\scripts\odak\deploy-odak-apps.ps1 -Services mngscheduler
```

---

## İlgili kod

| Servis | Dosyalar |
|--------|----------|
| MngScheduler | `DirectorySyncOrchestrationJob.cs`, `DirectorySyncOrchestrationService.cs`, `DomainLookupService.cs`, `JobSyncService.cs`, `MngKeeperDirectorySyncClient.cs` |
| MngKeeper | `DirectorySyncController.cs`, `KeycloakToMongoSyncService.cs`, `DirectorySyncCoordinator.cs` |

Teknik plan: [SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md)

---

## Doküman indeksi

| Dosya | Ne için |
|-------|---------|
| [DEVAM.md](./DEVAM.md) | Güncel faz özeti |
| [HANDOFF_UI.md](./HANDOFF_UI.md) | **Sonraki chat** — UI |
| [SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md) | K3 mimari |
| [DEPLOY_KEEPER_LDAP.md](./DEPLOY_KEEPER_LDAP.md) | Keeper deploy |
