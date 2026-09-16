# Teslimat Omurgası — Oturum durumu

**Son güncelleme:** 16 Eylül 2026  
**Konu:** Kontrol + Paydaş UX durak noktası; Toplantı ve Süreç ayrı yola bırakıldı  
**Ortam:** Odak **test** `192.168.20.20` (prod `192.168.20.8` bu hatta dokunulmadı)  
**Manifest:** `docs/odak/project_management/install/manifest.json` **0.36.0**

**Ana referans:** [PLAN.md](./PLAN.md)

> **Kaldığımız yer:** RAID → Kaynak → Bütçe → Okundu → Yükümlülük → Denetim → Paydaş turu TEST SEED-PMO’da örnekli. Yerel UI’da Kütüphane seçici, OC iş seçici (sayfalı) ve Türkçe tarih formatı hazır. TEST Docker’a **mngui** ve **mngoperations** deploy henüz yapılmadı. **Toplantı** ve **Süreç** aynı kart/seçici turuna girmeyecek; sonraki oturumda ayrı yol.

---

## Son çalışılan konu (16 Eylül 2026 — TEST)

1. **Yükümlülük** — madde → tarih → iş → kanıt. Kaynak/kanıt `PmPickDocumentDialog`, iş `PmPickWorkItemDialog` (arama + skip/take). Karşılandı kanıt ister; feragat not ister. Kapatma kilidi değil.
2. **Denetim** — çoklu kanıt seçici; teslim ≥1 belge; geri çekmek not ister. ZIP/e-posta/sihirbaz yok.
3. **Paydaş** — dış tarafın hangi belgelere bakabileceği. Portal/SSO/e-posta yok. Belgeler kütüphaneden seçilir; kimlik yapıştırılmaz. Geri almak not ister.
4. **Tarih gösterimi** — tablolar ve başlıklar `gg.aa.yyyy` (`usePmDate` / `pmFormatDate`). Form `type="date"` ISO kalır.
5. **OC iş araması** — `GET /projects/{id}/work-items?q&skip&take` sayfa DTO; istemci eski dizi yanıtını da okur. TEST operations henüz bu API ile deploy edilmedi.
6. **MarksWorkClosed** — `category=closed` da kapatma kilidine girer (yerel backend; TEST ops deploy yok).

---

## TEST doğrulama özeti (SEED-PMO)

Proje: `027bdf17-6741-4001-835f-9c1412c42f21` · Kütüphane hub `8d98f1c5-40dc-4d2c-b3e5-f8b9a0f34482` · Wiki `d117c4d1-c68d-418d-bcc5-268c65d6aa5a`

| Sekme | Seed / ölçüm |
|--------|----------------|
| RAID | 6 kayıt; Durum openRisk/openIssue |
| Kaynak | Ahmet aşırı yük; Durum overloadedResource |
| Bütçe | Saha kablolama aşım; 3.2 overBudget |
| Okundu | Wiki `Guvenlik talimati (DEMO)`; pendingAck / overdueAck |
| Yükümlülük | 6 kayıt (karşılandı / açık / iş bağsız / feragat) |
| Denetim | 6 paket; open=4 incomplete=2 overdue=2; WBS 1.1 openAuditPack+overdueAuditPack |
| Paydaş | 6 kayıt; open=5 incomplete=2 overdue=2; WBS 1.1 / 3.2 bayrakları |

Demo belgeler (Wiki): `Sartname ozeti (DEMO)` `2b8a18eb-…`, `Yangin butonu tutanak (DEMO)` `1141beb9-…`, `Guvenlik talimati (DEMO)` `e75c8c5f-…`.

Eğitim klasörleri (genel DI): Eğitim `8ae95f2d-a99d-426b-ad70-5f59d6d8ec6d`, Project `397ecda1-e2fa-400f-b627-0e8a83a5894c`.

---

## Tamamlanan işler (hat)

| Dilim | Özet |
|--------|------|
| F1–F5 + F2-14..16 | Teslimat omurgası (test’te tamam) |
| **0.36.0 + prod parity** | Schema/menü/SEED (prod, 15 Eyl) |
| **Kontrol UX (16 Eyl, test)** | RAID / Kaynak / Bütçe tabloları + Okundu seçici |
| **Yükümlülük / Denetim / Paydaş (16 Eyl, test)** | Seed + kütüphane seçici + tablo/modal; iş seçici yerelde |
| **Tarih (16 Eyl)** | Proje sekmelerinde Türkçe gg.aa.yyyy |

**Katalog (7, 1.1.0):** `pmo` · `quality` · `architecture` · `proposal` · `eco` · `onboarding` · `acceptance`

---

## Kararlar

- Generic teslimat omurgası; NLP yok.
- RAID / Kaynak / Bütçe / Okundu / Yükümlülük / Denetim / Paydaş **kapatma kilidi değildir**; Durum uyarısıdır.
- Okundu damgası portal değildir; dış kişi ada yazılır.
- Paydaş giriş hesabı değildir; e-posta bilgi alanıdır.
- Belge kimliği yapıştırılmaz; Kütüphane’den seçilir.
- Eğitim kaydı genel DI’dadır (proje kütüphanesi değil).
- **Toplantı** ve **Süreç** bir sonraki oturumda **aynı UX turuyla yapılmayacak**; ayrı yol.

---

## Sonraki adımlar

1. Toplantı ve Süreç — ayrı tasarım (bu duraktan sonra).
2. TEST deploy: `mngui` (seçici/modal/tarih) ve `mngoperations` (iş arama sayfası + MarksWorkClosed + kapı kilidi).
3. Eğitim DI sayfalarını `docs/odak/project_management/egitim/` kaynağından yeniden yayınla (Yükümlülük, Denetim, Paydaş güncel).
4. F2-16 lifecycle 404 (DI) ve F1-9 create 500 — ayrı bakış.

---

## Scriptler

| Script | Amaç |
|--------|------|
| `scripts/odak/sync-odak-source.ps1` / `deploy-odak-apps.ps1` | TEST sync/deploy (`192.168.20.20`) |
| `docs/odak/project_management/scripts/install-teslimat-omurgasi.ps1` | Schema + seed |
| `docs/odak/project_management/scripts/seed-obligations.ps1` | TEST SEED-PMO yükümlülük örnekleri |
| `docs/odak/project_management/scripts/seed-audit-packs.ps1` | TEST SEED-PMO denetim paketleri |
| `docs/odak/project_management/scripts/seed-stakeholders.ps1` | TEST SEED-PMO paydaş kayıtları |
| `docs/odak/project_management/egitim/` | Eğitim markdown kaynağı |
| `scripts/tests/MngOperations/smoke-f*.ps1` | Smoke (varsayılan test gateway) |
