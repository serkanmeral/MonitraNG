# Fatih Home — Seed işlemleri

## Amaç

Dump restore sonrası (veya dump öncesi iskelet ortamda) eksik kalan **iş / şablon / katalog** verisini tamamlamak.

Dump Mongo tarafını getirir; MinIO dosyaları, DI şablon paketleri, letterhead/cover ve bazı seed script’leri ayrıdır.

---

## Ne zaman

| Sıra | Koşul |
|------|--------|
| Erken seed | Domain + user sonrası, dump’tan önce — yalnızca zorunlu iskelet (genelde gerekmez) |
| Ana seed | **Faz 7** — [DATABASE.md](./DATABASE.md) restore + rebind sonrası |

---

## Tipik kalemler (monitrang / Odak deneyiminden)

| Kalem | Kaynak / yöntem | Not |
|-------|-----------------|-----|
| DI şablon + letterhead/cover | API pack / export (lokal `DOCUMENT_TEMPLATES.md` benzeri) | Dump’ta yoksa |
| Menü / OC seed | Domain template veya script | |
| Dataset şema tamamlayıcıları | Dump’ta gelmiş olmalı; eksikse seed | |
| MinIO bucket dosyaları | Ayrı sync | bucket `mng-odak` |
| Test / demo kayıtları | İsteğe bağlı | Fatih ortamına göre |

Kesin liste dump içeriği görüldükten sonra bu dosyada güncellenir.

---

## Referanslar

- Lokal DI taşıma: `docs/monitrang/deploy/local/DOCUMENT_TEMPLATES.md`
- Online veri notları: `docs/monitrang/deploy/mngonline/USERS_AND_DATA.md`
- Odak seed script’leri: `docs/odak/` ve `scripts/` (ilgili servis)

---

## Doğrulama

- [ ] Kritik koleksiyonlar dolu
- [ ] UI’da menü / DI / ilgili modül smoke
- [ ] Login + yetki (grup) smoke — [CHECKLIST.md](./CHECKLIST.md)
