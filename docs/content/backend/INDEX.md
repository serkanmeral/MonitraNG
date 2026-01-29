# Backend servislerine genel bakış

MonitraNG platformunun backend servisleri; tenant yönetimi, veri API'leri, event hub, gateway, zamanlayıcı, bildirim ve LLM gibi alanlarda çalışır. Bu sayfa, dokümantasyon nav'ında **Backend Servisleri** altında yer alan tüm servisleri listeler.

---

## Servis listesi

| Servis | Kısa açıklama | Dokümantasyon |
|--------|----------------|----------------|
| **MngKeeper** | Domain/tenant, kimlik doğrulama, lisans yönetimi | [MngKeeper](../MngKeeper/main/CHANGELOG.md) |
| **MngDataGateway** | Veri ve dataset API'leri, validasyon, sorgu | [MngDataGateway](../MngDataGateway/main/CHANGELOG.md) |
| **MngHub** | Event ve mesajlaşma merkezi | [MngHub](../MngHub/main/CHANGELOG.md) |
| **MngGateway** | Tek giriş noktası, yönlendirme, ters proxy | [MngGateway](../MngGateway/main/CHANGELOG.md) |
| **MngReactor** | Reaksiyon ve workflow motoru | [MngReactor](../MngReactor/main/CHANGELOG.md) |
| **MngEngine** | İş mantığı ve motor servisi | [MngEngine](../MngEngine/main/CHANGELOG.md) |
| **MngNotifier** | E-posta ve bildirim gönderimi | [MngNotifier](../MngNotifier/main/CHANGELOG.md) |
| **MngScheduler** | Zamanlanmış işler ve job yönetimi | [MngScheduler](../MngScheduler/main/CHANGELOG.md) |
| **MngLLM** | LLM tabanlı chatbot ve doküman servisi | [MngLLM](../MngLLM/main/CHANGELOG.md) |
| **MngAdmin** | Admin işlemleri, backup vb. | [MngAdmin](../MngAdmin/main/CHANGELOG.md) |

Sol menüden **Backend Servisleri** bölümünde her servis için aynı yapı kullanılır:

- **Changelog** — Sürüm notları
- **Roadmap** — Yol haritası ve kararlar
- **Technical Specs** — API/test referansı
- **Mimari** — Tasarım ve diyagramlar
- **Rehberler** — Nasıl yapılır, entegrasyon rehberleri
- **Kurulum / Sorun giderme / Specs** — Servise göre varsa eklenir

---

## API dokümantasyonu

REST API özetleri ve endpoint listesi için bkz. [API Documentation](../api/overview.md). Servis bazlı API sayfaları:

- [MngKeeper API](../api/mngkeeper/index.md)
- [MngDataGateway API](../api/mngdatagateway/index.md)
- [MngHub API](../api/mnghub/index.md)
