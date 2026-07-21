# Lokal kurulum checklist

Uygulama sırasında işaretlenir. Ayrıntılı prosedürler ilgili dokümanlarda.

## Ön koşullar

- [ ] Docker Desktop kurulu ve çalışıyor (WSL2)
- [ ] Repo güncel (`main`, GitHub pull tamam)
- [ ] Disk / RAM yeterli (MVP için yaklaşık not: _TBD_)
- [ ] Uzak iş akışı anlaşıldı → [REMOTE_CURSOR_WORKFLOW.md](./REMOTE_CURSOR_WORKFLOW.md)
- [ ] (Terminalde) Test ortamına erişim; artefaktlar lokal’e taşınacak kanal hazır

## Altyapı (`mng_common`)

- [ ] `.env` örneğinden türetildi (gitignore)
- [ ] `docker compose up -d` başarılı
- [ ] `mng_common_mng_network` mevcut
- [ ] Mongo / Redis / RabbitMQ / Keycloak health OK

## Domain & URL

- [ ] Hosts / DNS kararı uygulandı → [DOMAIN.md](./DOMAIN.md)
- [ ] UI ve Gateway URL’leri tarayıcıda çözülüyor

## Kimlik & tenant

- [ ] Keycloak realm / client’lar hazır (LDAP **yok**)
- [x] Eski lokal domainler temizlendi
- [x] Keeper domain **`odak`** oluşturuldu (Active; realm `odak`; bucket `mng-odak`) → [DOMAIN.md](./DOMAIN.md)
- [x] Mongo dump restore (`@users`/`@groups` hariç; lokal kimlik korundu) → [DATABASE.md](./DATABASE.md)
- [ ] Person/group id remap (eski Odak id → yeni lokal id) — expand için
- [ ] DI şablonları + letterhead/cover (test 20.20 → lokal, yöntem A) → [DOCUMENT_TEMPLATES.md](./DOCUMENT_TEMPLATES.md)
- [ ] Test kullanıcı ile token / login OK

## Veri

- [ ] Veri stratejisi uygulandı → [DATABASE.md](./DATABASE.md)
- [ ] Kritik koleksiyonlar / seed doğrulandı (liste: _TBD_)

## Uygulamalar (`mng_apps`)

- [ ] `.env` / compose override hazır
- [ ] Çekirdek servisler build + up
- [ ] Gateway health
- [ ] UI login smoke

## Kapanış

- [ ] [MIGRATION_PLAN.md](./MIGRATION_PLAN.md) karar tablosu doldu
- [ ] Bilinen sorunlar not edildi
