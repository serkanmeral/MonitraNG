# Widget & Dashboard Designer — Odak planlama

**Son güncelleme:** 7 Haziran 2026  
**Durum:** ✅ **Planlama dokümantasyonu tamam** — implementasyon bekliyor · [DEVAM.md](./DEVAM.md)

---

## Amaç

MonitraNG için **birleşik widget kütüphanesi** ve **dashboard designer** mimarisini tanımlamak. Bu klasör **yalnızca planlama**; kod implementasyonu ayrı chat/faz.

---

## Terminoloji (D7)

| Kısaltma | Anlam | Manifest `domain` |
|----------|--------|-------------------|
| **MO** | **MngOperations** — Operation Core | `operation-core` |
| **Monitoring** | Metrik / asset modülü | `monitoring` — **V1 dışı** |

---

## V1 katalog (D7 + D8)

| Domain | Veri yolu | Servis / dataset |
|--------|-----------|------------------|
| alarm | `serviceRef` | MngAlarm |
| siem | `serviceRef` | MngReactor (+ Alarm snapshot) |
| operation-core (MO) | `queryRef` | DG `op_work_items` |
| document-intelligence | `serviceRef` | MngDocument |

Detay: [KATALOG_V1.md](./KATALOG_V1.md) · [DATA_CATALOG.md](./DATA_CATALOG.md)

---

## Doküman haritası

### Mimari & sözleşme

| Belge | İçerik |
|-------|--------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Katmanlar, SurfaceContext, backend §13, serviceRef §5.5 |
| [MANIFEST_SCHEMA.md](./MANIFEST_SCHEMA.md) | Manifest prose + örnekler |
| [schemas/widget-manifest-v1.schema.json](./schemas/widget-manifest-v1.schema.json) | JSON Schema |
| [INTERACTIVITY_MODEL.md](./INTERACTIVITY_MODEL.md) | Grafana karşılaştırma |
| [DATA_CATALOG.md](./DATA_CATALOG.md) | queryRef / serviceRef master index |

### Katalog & UX

| Belge | İçerik |
|-------|--------|
| [KATALOG_V1.md](./KATALOG_V1.md) | V1 template listesi (~19) |
| [PRESENTATION_PRESETS.md](./PRESENTATION_PRESETS.md) | Chart/kart preset map |
| [DESIGNER_UX.md](./DESIGNER_UX.md) | Widget wizard + dashboard builder |

### Domain (V1)

| Belge | Domain |
|-------|--------|
| [DOMAIN_ALARM.md](./DOMAIN_ALARM.md) | alarm |
| [DOMAIN_SIEM.md](./DOMAIN_SIEM.md) | siem |
| [DOMAIN_OPERATION_CORE.md](./DOMAIN_OPERATION_CORE.md) | MO |
| [DOMAIN_DOCUMENT_INTELLIGENCE.md](./DOMAIN_DOCUMENT_INTELLIGENCE.md) | DI |

### Dataset & ilerleme

| Belge | İçerik |
|-------|--------|
| [datasets/DATASETS.md](./datasets/DATASETS.md) | `@widget_templates` şema |
| [datasets/KURULUM.md](./datasets/KURULUM.md) | Kurulum sırası (script hedef) |
| [datasets/widget_categories_seed_v1.json](./datasets/widget_categories_seed_v1.json) | Kategori seed |
| [DEVAM.md](./DEVAM.md) | Kararlar, faz planı, hazırlık durumu |

---

## Mimari özet

**4 katman:** Template → Definition → Surface Context → Placement  
**3 veri tipi:** `queryRef` (MO) · `serviceRef` (Alarm, SIEM, DI) · statik banner  
**Backend:** Ayrı servis yok (D0) — DG + Mng.Ui domain API proxy’leri

---

## İlişkili odak modülleri

| Modül | Widget ilişkisi |
|-------|-----------------|
| [alarm/](../alarm/README.md) | DOMAIN_ALARM |
| [monitoring/](../monitoring/README.md) | SIEM planlama (domain `siem`) |
| [operationcore/](../operationcore/README.md) | MO — DOMAIN_OPERATION_CORE |
| [document_intelligence/](../document_intelligence/DEVAM.md) | DOMAIN_DOCUMENT_INTELLIGENCE |

---

## Eski spec (arşiv)

[docs/content/Mng.Ui/support/specs/](../../content/Mng.Ui/support/specs/) — implementasyon detayı; odak kararları **bu klasör** esas alınır.
