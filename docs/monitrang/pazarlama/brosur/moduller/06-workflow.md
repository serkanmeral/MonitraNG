# Workflow

**Orkestrasyon ve dış entegrasyon**

---

## Tek cümle

Onay, gecikme, HTTP çağrısı, belge üretimi ve operasyon kaydını **tek zincirde** birleştiren platform otomasyon katmanı.

---

## Dış kapılar

| Kapı | Kim kullanır? |
|------|----------------|
| **HTTP flow** | ERP, partner, portal — SDK yok |
| **Kanal Akışları** | WhatsApp, Telegram self-servis — flow ile tasarlanır |
| **Event / schedule** | OC, alarm, Scheduler tetikleri |

---

## Ne zaman Workflow?

| Senaryo | Katman |
|---------|--------|
| Tek mail veya tek kural | OC veya Notifier |
| Çok adım + onay + ERP + belge | **Workflow** |
| «Faturam / borcum nedir?» diyalog | **Kanal Akışları** (Workflow alt) |

---

## Öne çıkan faydalar

- Versiyonlu akış tanımı
- Onay bekleme, gecikme, HTTP adımları
- WorkItem oluşturma / güncelleme
- Otomasyon Merkezi yönetim UI

---

*MonitraNG Platform Broşürü · Modüller*
