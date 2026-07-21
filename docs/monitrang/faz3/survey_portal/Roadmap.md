# Survey Portal — Roadmap (Faz 3)

**Teklif:** §4.5 Dış Katılım Portalı (Anket)  
**Klasör:** `docs/monitrang/faz3/survey_portal/`  
**Durum:** Doküman / bekletme — **geliştirme en son (P3)**  
**Son güncelleme:** 13 Temmuz 2026

---

## 1. Amaç

MonitraNG kullanıcısı olmayan müşteri/tedarikçi anketleri; TR+EN; sonuçlar portal + Odak MonitraNG.

## 2. Barındırma (karar bekleniyor)

| Seçenek | Açıklama | Durum |
|:---|:---|:---|
| **A — Kurum ortamı** | Kurumun sağladığı sunucu/bulut | Karar yok |
| **B — MonitraNG bulutu** | örn. `odak.monitrang.com` | Karar yok |

> Deploy süreci ve MIGRATION satırları **barındırma kararı sonrası** yazılacak. Şimdilik yalnızca plan dokümanı.

## 3. Kapsam özeti (tekliften)

- Anket oluşturma, e-posta davet, yanıt, sonuç, export, hatırlatma, KVKK  
- Opsiyon / sonraki: duyuru modülü (O3)

## 4. Fazlar (taslak — şimdilik uygulama yok)

| Faz | Hedef |
|:---|:---|
| SUR-0 | Barındırma A/B kararı + güvenlik/KVKK |
| SUR-1 | MVP anket + mail magic link |
| SUR-2 | Sonuç senkronu → Odak MNG |
| SUR-3 | TR/EN, hatırlatma, admin UI |
| SUR-O | Duyuru (O3) |

## 5. Bağımlılıklar

- Notifier / mail itibarı  
- Tenant izolasyonu  
- [../MIGRATION.md](../MIGRATION.md) — **sonra**

## 6. Kabul (özet)

Karar + MVP sonrası; katılımcı MonitraNG hesabı gerektirmez.

---

İş takibi: [work.md](./work.md)
