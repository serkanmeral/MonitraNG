# Reporting — Roadmap (Faz 3)

**Teklif:** §4.2 Raporlama  
**Klasör:** `docs/monitrang/faz3/reporting/`  
**Durum:** Planlama iskeleti  
**Son güncelleme:** 13 Temmuz 2026

---

## 1. Amaç

Parametreli raporlama; MonitraNG içi, HTTP ve DB kaynakları; export, DI belge, bildirim (in-app / e-posta / Telegram).

## 2. Kapsam özeti (tekliften)

| Alan | Madde |
|:---|:---|
| Kaynaklar | MNG dataset, HTTP endpoint, dahili/harici DB |
| Kural | Tanımlı + doğrulanmış erişim yoksa rapor yok |
| Ürün | Katalog, designer/runner, expand, özet, yetki, paylaşım, CSV/Excel, DI belge |
| Bildirim | in-app, e-posta, Telegram |

## 3. Mevcut / yeni

| | Durum (kabaca) |
|:---|:---|
| DG katalog, runner, expand, export B1, DI belge | Odak’ta ileri |
| HTTP / DB bağlayıcı + kaynak profili | **Yeni / genişletme** |
| Telegram kanalı | Bildirim omurgasına ek |

## 4. Fazlar (taslak)

| Faz | Hedef |
|:---|:---|
| RPT-0 | Gap: mevcut reporting_services ↔ teklif |
| RPT-1 | Kaynak profil modeli (HTTP/DB) + test bağlantı |
| RPT-2 | Raporun kaynak tipine bağlanması |
| RPT-3 | Bildirim (Telegram dahil) |
| RPT-4 | Export/PDF ve otomatik rapor (kapsam netleştirme) |

## 5. Bağımlılıklar

- `Mng.Ui` reporting, DG, Notifier  
- [../MIGRATION.md](../MIGRATION.md)

## 6. Kabul (özet)

Erişilemeyen kaynaktan rapor üretilmez; teklif kısıtları.

---

İş takibi: [work.md](./work.md)
