# Odak Kompozit — Teklif Çalışma Notları (İÇ)

> **GİZLİ / İÇ KULLANIM** — Müşteriye verilmez.  
> **Müşteri teklifi:** [Odak_Kompozit_Fiyat_Teklifi_MUSTERI.md](./Odak_Kompozit_Fiyat_Teklifi_MUSTERI.md)  
> **Birleşik çalışma taslağı:** [Odak_Kompozit_Fiyat_Teklifi.md](./Odak_Kompozit_Fiyat_Teklifi.md)

| | |
|:---|:---|
| **Son güncelleme** | 13 Temmuz 2026 |
| **Durum** | Fiyat/ödeme müşteri toplantısında doldurulacak |

---

## Paket mimarisi (güncel)

| # | Paket | Not |
|:--:|:---|:---|
| 1 | Döküman Zekası | Dosya ≠ Döküman |
| 2 | Raporlama | MNG / HTTP / DB |
| 3 | İzleme | SIEM yok; üretim **ayrı** |
| 4 | **Üretim Operasyonu** | OC workspace; Monitoring köprüsü |
| 5 | Dış Katılım (Anket) | Host **A: kurum** veya **B: monitrang.com** |
| 6 | **İş Paketleri — sürekli güncelleme** | Tek seferlik değil; backlog + dilimler |

## Toplantıda doldurulacak

- [ ] Proje bedeli / para birimi / KDV
- [ ] Ödeme planı
- [ ] Anket barındırma: A veya B
- [ ] İş paketleri güncelleme temposu
- [ ] Müşteri iletişim kişisi
- [ ] Faz süreleri

## Karar özeti (13 Tem)

- Fiyat ve ödeme **boş** bırakıldı (toplantıda yazılacak). Önceki 15.000 USD / canlıya almada %100 **iç not** olarak saklanabilir ama müşteri MD’de yok.
- Üretim Operasyonu Monitoring altından çıkarıldı → **§4.4**
- Anket: iki host seçeneği
- Yeni §4.6 İş Paketleri sürekli güncelleme

## Faz 3 geliştirme iskeleti

Konum: [`docs/monitrang/faz3/`](../../monitrang/faz3/README.md)

| Klasör | Teklif |
|:---|:---|
| ai_platform | Çapraz AI omurgası (on-prem) |
| document_intelligence | §4.1 |
| reporting | §4.2 |
| monitoring | §4.3 |
| production_operations | §4.4 |
| survey_portal | §4.5 (P3 — en son) |
| package_module | §4.6 |

Migration: tek dosya `faz3/MIGRATION.md`.  
Yeni chat bootstrap: [`docs/monitrang/faz3/AGENT_START.md`](../../monitrang/faz3/AGENT_START.md).

---

## Önceki iç referans (fiyat — sadece hatırlatma)

| Konu | Değer (toplantı öncesi taslak) |
|:---|:---|
| Bedel | 15.000 USD (KDV hariç) — *müşteriye yazılmadı* |
| Ödeme | Canlıya almada %100 — *müşteriye yazılmadı* |
| İletişim | Serkan MERAL · serkan.meral@outlook.com · 0532 420 67 56 |

---

*İç doküman*
