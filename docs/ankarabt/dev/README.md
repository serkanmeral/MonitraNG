# AnkaraBT — app01 kurulum dokümanları

MonitraNG’nin yerel uygulama sunucusuna (`app01`, `192.168.1.252`) kurulumu için analiz ve plan. Kurulum bu belgeler yazılırken başlatılmadı.

| Belge | İçerik |
|-------|--------|
| [ANALIZ.md](./ANALIZ.md) | 2026-10-20 sunucu envanteri, bellek sınırı, mevcut compose dosyalarının neden bu VM’ye aynen uygulanmayacağı |
| [KURULUM_PLANI.md](./KURULUM_PLANI.md) | Fazlar, açık kararlar, başarı kriterleri |

Erişim bilgileri: [../server/access.md](../server/access.md).

Bu klasör uzak sunucu (`portal.ankarabt.com`) kurulumunu anlatmaz. O host için yazılmış override’lar `ApplicationResources/mng_common/docker-compose.ankarabt.yml` ve `ApplicationResources/mng_apps/docker-compose.ankarabt.apps.yml` altındadır.
