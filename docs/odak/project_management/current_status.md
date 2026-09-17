# Teslimat Omurgası — Oturum durumu

**Son güncelleme:** 17 Eylül 2026  
**Konu:** Toplantı sicilleri + tutanak listesi + sekme modalı TEST’te  
**Ortam:** Odak **test** `192.168.20.20` (prod `192.168.20.8` dokunulmaz)  
**Manifest:** `docs/odak/project_management/install/manifest.json` **0.38.0** (şema TEST’te)

**Ana referans:** [PLAN.md](./PLAN.md)

> **Kaldığımız yer:** Toplantı TEST’te: Takvim | Liste (haftalık seri + anlık tablolar) | Tutanaklar (Kayıtlı / Eksik). Olay diyaloğu Olay | Gündem | Tutanak | Aksiyonlar. `mngoperations` + `mngui` NoCache deploy edildi. SEED-PMO’da **Haftalık PMO (DEMO)** ve **Kick-off tutanak (DEMO)** duruyor. Süreç sekmesi ayrı yol.

---

## Toplantı — kilit

- Yalnız **bu proje**. Tenant-geneli toplantı ürünü değil.
- Haftalık seri: gün + saat + süre + bitiş; örnekler yazılır (en fazla 26).
- Liste kart değil: üstte seri sicili, altta anlık (arama, son 30 gün + gelecek, sayfalama).
- Tutanak 1 toplantı = 1 sayfa (`minutesResourceId`). Sicil ayrı; dataset yeni değil.
- Tutanaklar: Kayıtlı / Eksik, arama, tarih, seri. Toplantısız tutanak yok.
- Modal sekmeli. Kaydet tüm sekmeleri basar. Aksiyonlar kayıt olduktan sonra.
- API: `kind`, `seriesId`, `from`, `to`, `q`, `skip`, `take`, `includeSeries`, `minutes=present|missing`.
- Dış takvim yok. NLP yok. “Bitti” OC kapatmaz.

---

## Tamamlanan (bu hat)

- Takvim + haftalık seri + gündem/tutanak MD (Kütüphane → Toplantı notları)
- Liste iki sicil; panel kendi sayfalarını çeker (üst sayfa tam tarama yapmaz)
- Tutanaklar görünümü ve sekme modalı
- TEST deploy (17 Eylül 2026)

---

## Sonraki adımlar

1. Süreç sekmesi — ayrı yol.
2. Durum sekmesi toplantı taraması hâlâ tam; bu kilit dışı.
