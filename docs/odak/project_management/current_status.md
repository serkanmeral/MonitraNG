# Teslimat Omurgası — Oturum durumu

**Son güncelleme:** 15 Eylül 2026  
**Konu:** Teslimat omurgası — izlenebilirlik zinciri + kapatma kilitleri (F4-7, F2-15, F2-16)  
**Ortam:** Odak test `192.168.20.20` · UI kontrolü lokal `npm run dev` · UI Docker image/deploy yok · backend deploy serbest  
**Manifest:** `docs/odak/project_management/install/manifest.json` **0.35.0**

**Ana referans:** [PLAN.md](./PLAN.md)

> **Kaldığımız yer:** Omurga halkası kuruldu: plan (`reference`) → WBS → OC iş → kanıt → kapatma kilitleri (kapı / kanıt / onay). NLP / şartname parser yok.

---

## Son çalışılan konu

**F4-7 / F2-15 / F2-16** (3 Eylül 2026 oturumu, 15 Eylül’de durum senkronu):

- **F4-7** — WBS işine DI plan/kaynak belgesi `reference` bağı. Durum: `missingReference`. API: `/wbs/{id}/references`.
- **F2-15** — Kanıtsız kapatma engeli: `409 EVIDENCE_REQUIRED`.
- **F2-16** — Onaysız/taslak bağlı belgeyle kapatma engeli: `409 APPROVAL_REQUIRED`.
- WBS satırında “Kanıt yok / Plan yok” chip’leri (UI lokal).

Önceki dilimler: **F4-6** (kanıt bağı), **F2-14** (kapı kilidi), F4-1…F4-5 paket/OC iş iskeleti.

---

## Tamamlanan işler (bu hat)

| Dilim | Özet |
|--------|------|
| F1-0 … F1-9 | Kurulum, DI tür/ilişki, görsel kanıt, `pm_*`, Gantt, WBS–OC, iz/durum, karar, PMO+kalite tohumu |
| F2-1 … F2-13 | İç katalog + kapı kaydı, RAID, kapasite, bütçe, okundu, yükümlülük, denetim, toplantı, paydaş, portföy, süreç |
| **F2-14** | Kapı iş kilidi. Smoke: `smoke-f214-gate-lock-test.ps1` |
| **F2-15** | Kanıt kapatma kilidi. Smoke: `smoke-f215-evidence-lock-test.ps1` |
| **F2-16** | Onay kapatma kilidi. Smoke: `smoke-f216-approval-lock-test.ps1` |
| F3-1 … F3-5 | Sektör rafları |
| F4-1 … F4-5 | İnce workspace, boş klasör sökme, kural/SLA/pano, yaprak+özet iş |
| **F4-6** | İş → kanıt belgesi. Smoke: `smoke-f46-pack-evidence-test.ps1` |
| **F4-7** | İş → plan/kaynak (`reference`). Smoke: `smoke-f47-pack-reference-test.ps1` |
| F5-1 | Paket kökeni / SHA-256. Smoke: `smoke-f51-pack-trust-test.ps1` |

**Katalog (7, sürüm 1.1.0):** `pmo` · `quality` · `architecture` · `proposal` · `eco` · `onboarding` · `acceptance`

---

## Kararlar (hatırla)

- Generic teslimat omurgası. AnkaraBT DOCX **örnek**; şartname parse edilmez, NLP açılmaz.
- Kapı kilidi: kapatma (done/closed). Başlatma serbest. Feragat kilidi açar.
- Kanıt bağı: `dm_resource_links` (`evidence` / `output`). Plan bağı: `reference`. Otomatik paketten bağ yok; kullanıcı bağlar.
- Kapatma sırası: kapı → kanıt zorunluluğu → bağlı belgeler yayınlanmış olmalı.
- UI image Odak’a basılmaz; kontrol `npm run dev`.
- Token: `docs/odak/operationcore/scripts/get-operationcore-token.ps1` (`odak_admin`). Smoke: `pwsh` (PS 7+).

---

## Bilinçli ertelenen

- App Store / üçüncü taraf paket satışı / ücret
- RAID Monte Carlo; kaynak dengeleme / CPM; bütçe ERP/FX
- Okundu LMS/e-imza; yükümlülük otomatik madde; denetim ZIP; toplantı takvim/Teams
- Paydaş tenant/portal; süreç editörü/BPMN; C4 editörü
- Paketten otomatik plan/kanıt önerisi; sponsor durum export

---

## Sonraki adımlar

Omurga hazır. Sonraki dilim ayrı onay (ör. paket önerisi, export, veya başka omurga halkası).

Smoke: `scripts/tests/MngOperations/smoke-f*.ps1` (`pwsh`).  
Deploy: `scripts/odak/sync-odak-source.ps1 -Paths MngOperations` sonra `scripts/odak/deploy-odak-apps.ps1 -Services mngoperations -NoCache`.
