# Müşteri ortamı → Lokal Docker Desktop taşıma planı

**Durum:** Planlama + uygulama (Adım 1 tamam)  
**Son güncelleme:** 2026-07-11  
**Hedef:** Kendi lokal Docker Desktop’ta MonitraNG yığınını çalışır hale getirmek.

---

## 1. Bağlam

Uzun süre geliştirme **müşteri terminal / test sunucusunda** yapıldı; kod GitHub’a commit/push edildi. Kod bu makineye alındı. Eksik olan: **çalışma ortamı** (domain, DB, kullanıcı, Docker yapılandırması).

Bu taşıma, Odak production’ı değiştirmez. Amaç **kişisel lokal geliştirme yığınıdır**.

**Erişim:** Lokal PC’den test/prod’a doğrudan ağ yok. VPN + RDP ile müşteri **terminal PC**; oradaki Cursor çalıştırır; çıktılar buraya alınır → [REMOTE_CURSOR_WORKFLOW.md](./REMOTE_CURSOR_WORKFLOW.md).

---

## 2. Hedef başarı kriterleri

- [ ] `mng_common` (Mongo, Redis, RabbitMQ, Keycloak, …) Docker Desktop’ta ayakta
- [ ] `mng_apps` (Gateway, Keeper, DG, UI, …) build + up
- [ ] Tarayıcıdan UI / login mümkün (karar verilen local URL ile)
- [ ] En az bir tenant/domain ve bir test kullanıcısı ile temel smoke
- [ ] Secret’lar git’e girmiyor; `.env` şablonlardan türetiliyor

---

## 3. Fazlar (öneri)

| Faz | Konu | Doküman | Not |
|-----|------|---------|-----|
| 0 | Envanter & kararlar | [INVENTORY.md](./INVENTORY.md), bu dosya | Kaynak sunucu vs lokal farklar |
| 0b | Uzak Cursor iş akışı | [REMOTE_CURSOR_WORKFLOW.md](./REMOTE_CURSOR_WORKFLOW.md) | Prompt burada → terminalde çalışır → paket geri |
| 1 | Docker Desktop & ağ | [DOCKER.md](./DOCKER.md) | WSL2, disk, port çakışmaları |
| 2 | Domain / hosts / URL | [DOMAIN.md](./DOMAIN.md) | Tenant: `odak`; URL hosts TBD |
| 3 | Altyapı ayağa kaldırma | [DOCKER.md](./DOCKER.md) | Önce `mng_common` |
| 4 | Veritabanı stratejisi | [DATABASE.md](./DATABASE.md) | Boş seed vs dump |
| 5a | Domain temizle + `odak` oluştur | [DOMAIN.md](./DOMAIN.md) | **Sıra: 1** |
| 5b | User + group Local normalize (id koru) | [USERS_AND_AUTH.md](./USERS_AND_AUTH.md) | **Sıra: 2** — AD yok; user **ve** group |
| 5c | Varsayılan / iş verisi (dump) | [DATABASE.md](./DATABASE.md) | **Sıra: 3** — birebir; persons + personGroups |
| 5d | DI şablon + letterhead/cover | [DOCUMENT_TEMPLATES.md](./DOCUMENT_TEMPLATES.md) | **Sıra: 4** — API pack; kaynak test 20.20 |
| 6 | Uygulama stack (`mng_apps`) | [DOCKER.md](./DOCKER.md) | Compose + env |
| 7 | Smoke & checklist | [CHECKLIST.md](./CHECKLIST.md) | Login, API health |

---

## 4. Kararlar (planlama sırasında doldurulacak)

| Konu | Seçenekler | Karar | Tarih |
|------|------------|-------|-------|
| Veri stratejisi (Adım 3) | A) Template · B) Mongo dump + normalize · C) Hibrit | **B kısmi:** dump `mng_odak`, **`@users`/`@groups` hariç** (Adım 2 koru) | 2026-07-11 |
| Local URL / hosts | `*.monitra.local` · `localhost` port · diğer | _TBD_ | |
| Tenant/domain adı | tek domain | **`odak`** | 2026-07-11 |
| Kullanıcı modeli | Local / Directory | **Hepsi Local** (dump + normalize; id koru) | 2026-07-11 |
| Grup modeli | Local / Directory | **Hepsi Local** (dump + normalize; id koru) | 2026-07-11 |
| Erişim modeli | Doğrudan API · Terminal RDP + Cursor | **VPN → RDP → terminal Cursor**; çıktı buraya | 2026-07-11 |
| LDAP | Kapalı (local) · mock · müşteri LDAP | **Kapalı** | 2026-07-11 |
| Hangi servisler zorunlu (MVP) | Full stack · çekirdek (Gateway+Keeper+DG+UI) | _TBD_ | |
| MinIO / dosya | Boş bucket · senkron | _TBD_ | |

---

## 5. Açık sorular

1. Hosts dosyası / lokal DNS (`app.monitra.local` vb.) kullanılacak mı, yoksa yalnızca `localhost:port` mı?
2. Docker Desktop bu makinede hazır mı (WSL2, kaynak limitleri)?
3. Müşteri terminalinden artefakt taşıma kanalı nedir (paylaşım klasörü, USB, …)?
4. Toplu lokal kullanıcılar için tek ortak varsayılan şifre mi kullanılacak?

---

## 6. Riskler

| Risk | Etki | Önlem |
|------|------|--------|
| Secret’ların dokümana / git’e yazılması | Güvenlik | Yalnızca example; gerçek değer local ignore |
| Port çakışması (3000, 8080, 27017, …) | Stack kalkmaz | [DOCKER.md](./DOCKER.md) port envanteri |
| Keycloak + Keeper domain uyumsuzluğu | Login fail | [USERS_AND_AUTH.md](./USERS_AND_AUTH.md) sırası |
| Büyük Mongo dump | Disk / süre | Önce MVP boş ortam |

---

## 7. Bilinçli dışı bırakılanlar

- Müşteri production (`192.168.20.8`) — dokunulmaz
- GitLab Pages / sunucu CI — bu taşımanın parçası değil
- Eski Kalite (legacy) Docker — ayrı plan: `docs/odak/siparis/DOCKER_LOCAL_PLAN.md`
