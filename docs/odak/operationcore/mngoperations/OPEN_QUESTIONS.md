# MngOperations — Açık sorular

**Son güncelleme:** 26 Mayıs 2026

Birlikte netleştirilecek maddeler. Karar verildikçe ilgili belgeye + [OPERATION_CORE §12](../OPERATION_CORE_IMPLEMENTATION_PLAN.md) karar loguna işlenir.

---

## 1. Altyapı

| # | Soru | Öneri (agent) | Durum |
|---|------|---------------|--------|
| Q1 | Host port **5086** uygun mu? | Scheduler 5090, Workflow 5085 arası | **Karar: 5086** (26 May 2026) |
| Q2 | MO → DG her zaman doğrudan `mngdatagateway:5010` mi? | Evet; `DataGateway:BaseUrl` appsettings + env (dev/prod farklı) | **Karar: doğrudan DG; Bearer forward** (26 May 2026) |
| Q3 | Health check DG down → **Unhealthy** mi **Degraded** mi? | **Degraded** | **Kararlandı** (26 May 2026) |

---

## 2. Veri ve tutarlılık

| # | Soru | Öneri | Durum |
|---|------|--------|--------|
| Q4 | WorkItem key: **scan son key** vs **counter dokümanı**? | TM ile aynı semantik: workspace prefix + artan sıra; MO üretir (DG incremental değil) | **Karar: workspace `workItemKeyPrefix` + sıra; format `workItemKeyFormat` (varsayılan `{PREFIX}-{SEQ:D4}`, TM gibi)** (26 May 2026) |
| Q5 | Çok adımlı persist / compensation | **Faz 1:** `PARTIAL_FAILURE` + tamamlanan adımlar listesi; **Faz 2:** outbox/saga | **Kararlandı** (26 May 2026) |
| Q6 | `from-origin` **idempotency** | **Faz 1:** `correlationId` lookup → varsa mevcut WI; **Faz 2:** genel idempotency/outbox | **Kararlandı** (26 May 2026) |

---

## 3. API ve UX sözleşmesi

| # | Soru | Öneri | Durum |
|---|------|--------|--------|
| Q7 | Board kolon kartları | **Ayrı** `POST /runtime/queries/{queryKey}/execute` (board context kart taşımaz) | **Kararlandı** (26 May 2026) |
| Q8 | Attachment / file | **DG native `file` alanı** (`/data/api/v1/files` + data body); MO proxy **yok** | **Kararlandı** (26 May 2026) |
| Q9 | `isAdmin` operasyonel **tam bypass** mı? | Evet + audit (L4) | **Kararlandı** (26 May 2026) |
| Q10 | Hata gövdesi | **`code` zorunlu**; MO sık kodlarda `messageTr`; UI code map (B) | **Kararlandı** (26 May 2026) |

---

## 4. Entegrasyon ve deploy

| # | Soru | Öneri | Durum |
|---|------|--------|--------|
| Q11 | RabbitMQ exchange adı: `oc.events` mi mevcut hub exchange mi? | Ayrı topic `oc.events`; routing `{domainId}.oc.workitem.*` | **Kararlandı** (26 May 2026) |
| Q12 | Odak’ta `mngoperations` ilk deploy **OC UI’dan önce** mi? | Evet — API smoke + seed workspace | **Kararlandı** (26 May 2026) |
| Q13 | Seed veri (örnek workspace, flow, board): script MO repo’da mı OC scripts’te mi? | `docs/odak/operationcore/scripts/seed-*.ps1` | **Kararlandı** (26 May 2026) |
| Q14 | DG `publish_mode` vs MO bildirim | `op_*` → **`none`**; bildirim + `oc.events` **MO** | **Kararlandı** (26 May 2026) |

---

## 5. Yetki modeli (L1–L4)

| # | Soru | Durum |
|---|------|--------|
| L1 | Faz 1 DG `permissions` | **Karar: Tüm `op_*` açık (null); yetki MO’da kayıt/alan/workspace/board** |
| L2 | Grup / Keeper vs MO | **Karar: DG dataset kapısı ≠ MO operasyonel yetki; Keeper claim MO okur** |
| L3 | Metadata DG update | **Karar: `op_*` DG açık; metadata düzenleme MO (`isManager`/workspace admin)** |
| L4 | `isAdmin` operasyonel bypass | **Karar: tam bypass + audit** (Q9 ile aynı) |

## Karar logu

| Tarih | ID | Karar | Belge |
|-------|-----|-------|-------|
| 26 May 2026 | Q1 | API/container port **5086** | GATEWAY_AND_DEPLOY.md |
| 26 May 2026 | Q4 | WorkItem key = TM’deki proje/task key mantığı; workspace `workItemKeyPrefix` + monoton sıra; üretim **MngOperations** create pipeline | DG_INTEGRATION.md |
| 26 May 2026 | Q2 | MO → DG doğrudan; `DataGateway__BaseUrl`; Bearer forward; `Actors.MngKeeper` + `Jwt:Authority` (Keycloak realm) env/appsettings | AUTH_AND_CONFIGURATION.md |
| 26 May 2026 | L1 (revize) | Tüm `op_*` DG permissions null; asıl yetki MO workspace/board/kayıt/alan | PERMISSIONS_LAYERING.md §5.1 |
| 26 May 2026 | L2 | DG dataset erişimi ≠ MO operasyonel; Keeper token | PERMISSIONS_LAYERING.md §5.2 |
| 26 May 2026 | L3 | Metadata da op_* DG açık; config yetkisi MO | PERMISSIONS_LAYERING.md §7 |
| 26 May 2026 | L4 / Q9 | `isAdmin` operasyonel tam bypass; audit zorunlu | PERMISSIONS_LAYERING.md §5.2, PERMISSIONS_AND_FIELD_BEHAVIOR.md |
| 26 May 2026 | Q3 | DG/RabbitMQ down → health **Degraded**; yalnızca MO çökerse **Unhealthy** | INTEGRATIONS.md §7 |
| 26 May 2026 | Q5 | Faz 1: otomatik rollback yok; `PARTIAL_FAILURE` + `completedSteps[]`; Faz 2: outbox/saga | PIPELINES.md §9, ARCHITECTURE.md §4 |
| 26 May 2026 | Q6 | Faz 1: `from-origin` + `origin.correlationId` idempotent lookup; Faz 2: genel | PIPELINES.md §6, API_SURFACE.md |
| 26 May 2026 | Q7 | Board metadata `GET /runtime/boards/{id}`; kolon kartları ayrı query execute | RUNTIME_CONTEXT.md §5 |
| 26 May 2026 | Q8 | Dosya: DG `file` alanı + Files API; MO yalnızca field behavior / isteğe bağlı metadata PATCH | DG_INTEGRATION.md §7 |
| 26 May 2026 | Q10 | Hata: `code` + opsiyonel `message` / `messageTr`; UI ana çeviri map | ARCHITECTURE.md §4 |
| 26 May 2026 | Q11 | Exchange **`oc.events`**; routing **`{domainId}.oc.workitem.{created\|updated\|transitioned}`**; payload `domainId` + `domainName` zorunlu | INTEGRATIONS.md §3 |
| 26 May 2026 | Q14 | `op_*` **`publish_mode: none`**; kullanıcı bildirimi MO (`op_notifications`, policies, Notifier); domain olay **`oc.events`** | NOTIFICATIONS_AND_EVENTS.md |
| 26 May 2026 | Q12 | Odak deploy: **MngOperations + gateway route + smoke** önce; OC UI sonra | GATEWAY_AND_DEPLOY.md §4 |
| 26 May 2026 | Q13 | Demo/seed scriptleri **`docs/odak/operationcore/scripts/seed-*.ps1`** (MO repo değil) | scripts/README.md |
