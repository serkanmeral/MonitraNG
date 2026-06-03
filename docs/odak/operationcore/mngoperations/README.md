# MngOperations

MonitraNG **Operation Core (OC)** backend servisi — operasyonel karar ve iş yönetim motoru.

**Son güncelleme:** 26 Mayıs 2026  
**Durum:** Faz 1 backend MVP + Odak deploy; UI form tanımı/create ilerlemesi — [DEVAM.md](./DEVAM.md).  
**Üst modül:** [operationcore](../README.md)  
**Spec:** [operationcore_phase1.md](../operationcore_phase1.md)  
**Kod:** [MngOperations](../../../../MngOperations/) (solution kökü)

---

## Planlama dokümanları

| # | Belge | Konu |
|---|--------|------|
| 1 | [SERVICE_SCOPE.md](./SERVICE_SCOPE.md) | Amaç, sınırlar, sorumluluklar |
| 2 | [ARCHITECTURE.md](./ARCHITECTURE.md) | Clean Architecture, modüller, bağımlılıklar |
| 3 | [DG_INTEGRATION.md](./DG_INTEGRATION.md) | MngDataGateway client, `op_*` okuma/yazma |
| 3b | [AUTH_AND_CONFIGURATION.md](./AUTH_AND_CONFIGURATION.md) | Token forward, appsettings, multi-tenant |
| 4 | [API_SURFACE.md](./API_SURFACE.md) | Komutlar + `/runtime/*` |
| 5 | [PIPELINES.md](./PIPELINES.md) | Create, PATCH, transition, from-origin |
| 6 | [RULE_ENGINE.md](./RULE_ENGINE.md) | `op_rules`, action tipleri, merge |
| 7 | [PERMISSIONS_AND_FIELD_BEHAVIOR.md](./PERMISSIONS_AND_FIELD_BEHAVIOR.md) | Group-first, field runtime |
| 7b | [PERMISSIONS_LAYERING.md](./PERMISSIONS_LAYERING.md) | DG dataset permission × MO workspace |
| 8 | [RUNTIME_CONTEXT.md](./RUNTIME_CONTEXT.md) | Form / Profile / Board / Dashboard context |
| 8b | [FORM_LAYOUT_AND_EXTRA_FIELDS.md](./FORM_LAYOUT_AND_EXTRA_FIELDS.md) | `op_forms.layout`, `extraFields`, FormRuntimeBuilder |
| 9 | [INTEGRATIONS.md](./INTEGRATIONS.md) | Notifier, RabbitMQ, Keeper |
| 9b | [NOTIFICATIONS_AND_EVENTS.md](./NOTIFICATIONS_AND_EVENTS.md) | DG `publish_mode` vs MO bildirim / `oc.events` |
| 10 | [GATEWAY_AND_DEPLOY.md](./GATEWAY_AND_DEPLOY.md) | Ocelot, Odak port, compose |
| 11 | [MVP_CHECKLIST.md](./MVP_CHECKLIST.md) | Faz 1 backend kilometre taşları |
| — | [DEVAM.md](./DEVAM.md) | **Checkpoint** — nerede kaldık, moladan sonra sıra |
| — | [PERF_OPTIMIZATION.md](./PERF_OPTIMIZATION.md) | Mayıs board/profil perf + Haziran UI Faz 1/1B özeti |
| — | [../../diagnostic/README.md](../../diagnostic/README.md) | Odak ölçüm scriptleri, raporlar, yol haritası |
| — | [OPEN_QUESTIONS.md](./OPEN_QUESTIONS.md) | Karar logu (tamamlandı) |

**UI:** [ui/OC_UI_FORM_DEFINITIONS.md](../ui/OC_UI_FORM_DEFINITIONS.md) — form editör handoff; genel plan [ui/README.md](../ui/README.md).

---

## Hızlı özet

```text
UI / dış modüller  →  API Gateway  /operations/api/v1  →  MngOperations
                                                              ↓
                                                    MngDataGateway (op_*)
                                                    MngNotifiers (mail)
                                                    RabbitMQ (domain events)
```

Kimlik: **MngKeeper** token üretimi; MO **Jwt:Authority** ile doğrulama + aynı Bearer’ı DG’ye forward ([AUTH_AND_CONFIGURATION.md](./AUTH_AND_CONFIGURATION.md)).
