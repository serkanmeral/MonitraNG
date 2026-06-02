# Monitoring → SIEM (Odak)

Müşteri ortamında **güvenlik odaklı izleme / SIEM-hafif** çözümünün planlama ve teslim dokümanları. Mevcut MonitraNG Monitoring altyapısı (MngEngine, MngReactor, MngWorkflow) üzerine kurulur.

**Durum:** Planlama (Faz 0 — çerçeve)
**Son güncelleme:** 1 Haziran 2026

---

## Kapsam kararı (kilitli)

Müşteri ile alınan başlangıç kararları:

| Konu | Karar |
|------|-------|
| **Ürün kapsamı** | SIEM-hafif: hedefli senaryolarla başla, kademeli derinleştir |
| **İlk faz kaynakları** | Firewall (syslog) · Active Directory / login olayları · Sunucu/endpoint logları · Jump host / bastion / VPN |
| **Dağıtım** | On-prem — veri müşteri ağında kalır |
| **Tespit sonrası** | Onaylı müdahale (operatör onayıyla, ör. firewall blok) |
| **Uyum hedefi** | ISO/IEC 27001 |

---

## Dokümanlar

| Dosya | İçerik | Durum |
|-------|--------|--------|
| [SIEM_PLANNING.md](./SIEM_PLANNING.md) | Ana planlama: gap analizi, event veri modeli, mimari akış, kullanım senaryoları, korelasyon, onaylı müdahale, retention, fazlar | Taslak |
| [DEVAM.md](./DEVAM.md) | **Kaldığımız yer** — durum, kilitli kararlar, workflow planı sonrası yapılacaklar | ⏸️ Duraklatıldı |

---

## İlişkili dokümanlar

| Konu | Konum |
|------|-------|
| Genel siber güvenlik vizyonu (ürün geneli) | `docs/content/security/CYBERSECURITY_SOLUTION_PLANNING.md` |
| Monitoring mimarisi (Reactor/Engine/Workflow) | `docs/content/monitoring_plans/` |
| ISO 27001 kontrol eşlemesi | `docs/odak/compliance/ISO27001_PLAN.md` |
| Uyum yol haritası | `docs/odak/compliance/COMPLIANCE_ROADMAP.md` |

---

## Hızlı bağlantılar

- Üst odak indeksi: [../README.md](../README.md)
- Uyum (compliance) indeksi: [../compliance/README.md](../compliance/README.md)
