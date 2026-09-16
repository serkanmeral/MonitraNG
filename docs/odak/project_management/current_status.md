# Teslimat Omurgası — Oturum durumu

**Son güncelleme:** 16 Eylül 2026  
**Konu:** TEST’te Kontrol sekmeleri UX (RAID, Kaynak, Bütçe, Okundu) + Eğitim DI  
**Ortam:** Odak **test** `192.168.20.20` (prod `192.168.20.8` bu oturumda dokunulmadı)  
**Manifest:** `docs/odak/project_management/install/manifest.json` **0.36.0**

**Ana referans:** [PLAN.md](./PLAN.md)

> **Kaldığımız yer:** TEST SEED-PMO üzerinde RAID / Kaynak / Bütçe / Okundu canlı örnekler duruyor. UI kart→tablo + seçicili Okundu modalı yerelde hazır; TEST Docker görüntüsüne deploy kullanıcı talebiyle.

---

## Son çalışılan konu (16 Eylül 2026 — TEST)

1. **RAID** — ekleme modalı tür kartları, canlı skor, WBS ipucu. Kart listesi değil.
2. **Kaynak** — kişi kartları kalktı; sayfalı özet + atama tablosu. Hafta çipleri satır açılınca.
3. **Bütçe** — paket kartları aynı tablo düzenine alındı. Kalem modalı kategori kartları + canlı kalan. Karışık kurda üst toplam yok.
4. **Okundu** — süreç defter (portal/e-posta yok). Belge `PmPickDocumentDialog` ile Kütüphane’den seçilir. Kişi iç veya kurum dışı ad. Damgayı PM basar.
5. **Eğitim DI** — genel DI kökü `Eğitim` → `Project` sayfaları güncellendi (RAID, Kaynak, Bütçe, Okundu, Paydaş). Kaynak metin: `docs/odak/project_management/egitim/`.
6. **SEED-PMO** (test): `027bdf17-6741-4001-835f-9c1412c42f21` — RAID, kaynak, bütçe, okundu demo kayıtları.

---

## TEST doğrulama özeti (SEED-PMO)

| Sekme | Seed / ölçüm |
|--------|----------------|
| RAID | 6 kayıt; Durum openRisk/openIssue/assumption/dependency |
| Kaynak | Ahmet aşırı yük (7 Eyl 60s); Can tarihsiz 50s; Durum overloadedResource 2.1/3.2/3.3 |
| Bütçe | Saha kablolama 95k/80k TRY aşım; 3.2 overBudget |
| Okundu | Wiki `Guvenlik talimati (DEMO)`; pendingAck=4 overdueAck=1; 1.1 bayrak |

Eğitim klasörleri: Eğitim `8ae95f2d-a99d-426b-ad70-5f59d6d8ec6d`, Project `397ecda1-e2fa-400f-b627-0e8a83a5894c`.

---

## Tamamlanan işler (hat)

| Dilim | Özet |
|--------|------|
| F1–F5 + F2-14..16 | Teslimat omurgası (test’te tamam) |
| **0.36.0 + prod parity** | Schema/menü/SEED + MarksWorkClosed (prod, 15 Eyl) |
| **Kontrol UX (16 Eyl, test)** | RAID/Kaynak/Bütçe tabloları + Okundu seçici |

**Katalog (7, 1.1.0):** `pmo` · `quality` · `architecture` · `proposal` · `eco` · `onboarding` · `acceptance`

---

## Kararlar

- Generic teslimat omurgası; NLP yok.
- RAID / Kaynak / Bütçe / Okundu kapatma kilidi değildir; Durum uyarısıdır.
- Okundu damgası portal değildir; dış kişi ada yazılır.
- Eğitim kaydı genel DI’dadır (proje kütüphanesi değil).

---

## Sonraki adımlar

1. TEST UI deploy (bu talep).
2. Yükümlülük / Denetim / Toplantı / Paydaş sekmeleri aynı UX turu.
3. F2-16 lifecycle 404 (DI) ve F1-9 create 500 — ayrı bakış.
4. MarksWorkClosed test parity — hâlâ yerelde uncommitted backend.

---

## Scriptler

| Script | Amaç |
|--------|------|
| `scripts/odak/sync-odak-source.ps1` / `deploy-odak-apps.ps1` | TEST sync/deploy (`192.168.20.20`) |
| `docs/odak/project_management/scripts/install-teslimat-omurgasi.ps1` | Schema + seed |
| `.tmp-di-egitim-project/publish.ps1` | Eğitim sayfalarını TEST DI’ya yükle |
| `scripts/tests/MngOperations/smoke-f*.ps1` | Smoke (varsayılan test gateway) |
