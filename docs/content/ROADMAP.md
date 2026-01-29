# Product Roadmap — MonitraNG

Bu sayfa **MonitraNG platformu** için ürün seviyesi yol haritasına kısa genel bakış ve önceliklere yer verir. Servis bazlı detaylı roadmap’ler ilgili backend/frontend sayfalarında yer alır.

---

## Genel bakış

MonitraNG, kimlik/yetkilendirme (MngKeeper), veri katmanı (MngDataGateway), bildirim (MngNotifier), zamanlama (MngScheduler), LLM/çeviri (MngLLM), event/reactor (MngHub, MngReactor), gateway ve admin bileşenleriyle çok kiracılı bir uygulama platformudur.

**Öncelikler (özet):**

1. **Çekirdek platform kararlılığı** — Kimlik, veri, gateway ve domain yaşam döngüsünün production’da güvenli ve ölçeklenebilir çalışması.
2. **Kullanıcı deneyimi ve yetkilendirme** — Profil alanları, Manager/Admin rolleri, şifre yönetimi (forgot-password dahil), gerekirse audit ve permission iyileştirmeleri.
3. **Bildirim ve entegrasyon** — MngNotifier ile mail template’leri, RabbitMQ tabanlı async bildirim, gerekirse retry/DLQ ve çoklu kanal planlaması.

---

## Servis roadmap’lerine linkler

Aşağıdaki sayfalar her servisin **yapılanlar / yapılacaklar / kararlar** roadmap’ini içerir. Navigasyonda “Backend Servisleri” altında ilgili servisin “Roadmap” girdisiyle de ulaşılabilir.

| Servis | Açıklama | Roadmap |
|--------|----------|---------|
| **MngKeeper** | IAM, domain, kullanıcı/grup, lisans, şablon | [MngKeeper Roadmap](MngKeeper/main/ROADMAP.md) |
| **MngDataGateway** | Veri katmanı, dataset, query, dosya | [MngDataGateway Roadmap](MngDataGateway/main/ROADMAP.md) |
| **MngNotifier** | Mail/SMS/çoklu kanal bildirimleri | [MngNotifier Roadmap](MngNotifier/main/ROADMAP.md) |
| **MngHub** | Event, SignalR, real-time | [MngHub Roadmap](MngHub/main/ROADMAP.md) |
| **MngReactor** | Event reaksiyonları, iş akışları | [MngReactor Roadmap](MngReactor/main/ROADMAP.md) |
| **MngEngine** | İş motoru | [MngEngine Roadmap](MngEngine/main/ROADMAP.md) |
| **MngScheduler** | Zamanlanmış işler | [MngScheduler Roadmap](MngScheduler/main/ROADMAP.md) |
| **MngLLM** | LLM, chatbot, çeviri | [MngLLM Roadmap](MngLLM/main/ROADMAP.md) |
| **MngGateway** | API gateway, yönlendirme, JWT | [MngGateway Roadmap](MngGateway/main/ROADMAP.md) |
| **MngAdmin** | Admin konsolu, backup | [MngAdmin Roadmap](MngAdmin/main/ROADMAP.md) |
| **MngDomainUI** | Domain yönetim arayüzü | [MngDomainUI Roadmap](MngDomainUI/guides/ROADMAP.md) |

Ek roadmap veya teknik planlar bazı servislerde “Guides” / “Roadmap (ek)” altında da bulunur (ör. MngDataGateway, MngScheduler, MngLLM).

---

*Detaylı sürüm notları için ilgili servisin Changelog sayfasına bakınız.*
