# MonitraNG SIEM — Aktif yol haritası

**Son güncelleme:** 4 Haziran 2026  
**Durum:** MVP (U1–U7) tamam · post-MVP genişleme devam  
**Handoff:** [DEVAM.md](./DEVAM.md) · [HANDOFF.md](./HANDOFF.md)

---

## 1. Stratejik sıra

LogAlarm / tam SIEM paritesi (5651, WORM, sertifikasyon) **en sona** bırakıldı — kendi SIEM yol haritası tamamlanana kadar kod yok.

| Sıra | Alan | Durum |
|------|------|--------|
| **1** | B1 parser + senaryo (U8/U9…) | 🟡 devam |
| **2** | Toplama olgunluğu (WEF/agent) | 🟡 |
| **3** | Perf / operasyon / E2E | ⬜ |
| **4** | UX (dashboard, arama) | 🟡 |
| **5** | Uyum arşivi + pazar kıyaslaması | ⬜ ertelendi |

Referans (kod yok): [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md) · [SIEM_WORM_5651_SPIKE.md](./SIEM_WORM_5651_SPIKE.md)

---

## 2. Faz 1 — Parser & senaryo

| # | İş | Not |
|---|-----|-----|
| 1.1 | `windows.security.extended.v1` | ✅ 4720/4728/5136 |
| 1.2 | `firewall.vendor.v1` FortiGate + PAN-OS + **Cisco ASA** | ✅ |
| 1.3 | **U8** `group_member_added` alarm | ✅ |
| 1.4 | **U9** `account_created` alarm | ✅ |
| 1.5 | Üçüncü FW vendor veya bastion parser | ✅ Cisco ASA |
| 1.6 | Extended event genişletme (4722/4726/5137…) | ⬜ |

---

## 3. Faz 2 — Toplama

| # | İş | Not |
|---|-----|-----|
| 2.1 | WEF→WEC→Engine batch | ✅ S5 E2E |
| 2.2 | Forwarder şablonu + extended fixture | ✅ |
| 2.3 | WEC XPath — extended Event ID’ler | ✅ forwarder doc |
| 2.4 | NxLog prod şablonu doğrulama | ⬜ müşteri ops |
| 2.5 | Linux agent / rsyslog hardening | ⬜ |

---

## 4. Faz 3 — Perf & operasyon

| # | İş | Not |
|---|-----|-----|
| 3.1 | P0/P1 benchmark regression | ✅ baseline |
| 3.2 | MQ backlog diagnostic + purge scriptleri | ✅ |
| 3.3 | E2E suite `-Quick` CI kapısı | ✅ unit gate · `scripts/ci/test-siem-unit-gate.ps1` |
| 3.4 | P2 soak profili (5 dk @ 150 evt/s hedef) | ✅ ~93 evt/s · `benchmark-P2-2026-06-04.json` |

---

## 5. Faz 4 — UX

| # | İş | Not |
|---|-----|-----|
| 4.1 | Dashboard MVP + P2 timeline | ✅ |
| 4.2 | U8/U9 senaryo kartları | ✅ |
| 4.3 | Olay arama presets U8/U9 | ✅ |
| 4.4 | `directory_object_modified` preset | ⬜ |

---

## 6. Faz 5 — Ertelenen (LogAlarm / uyum)

| # | İş | Not |
|---|-----|-----|
| 5.1 | 5651 / WORM arşiv (C1) | spike only — implementasyon yok |
| 5.2 | Denetim raporları (C2) | ⬜ |
| 5.3 | Geniş SIEM pazar kıyaslaması | Splunk / Elastic / LogAlarm kombinasyon |

---

## 7. Kanıt zinciri

```
sec_events → observation → correlation/sequence → alarm.raised → Workflow → (approval) → block.ip
```

Odak: `test-siem-e2e-suite.ps1` · `-Quick` regression

---

## 8. Referanslar

- [SIEM_PARSER_PLAN.md](./SIEM_PARSER_PLAN.md)
- [SIEM_WEF_WEC_FORWARDER.md](./SIEM_WEF_WEC_FORWARDER.md)
- [SIEM_DASHBOARD.md](./SIEM_DASHBOARD.md)
