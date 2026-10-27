# app01 analiz

**Tarih:** 2026-10-20  
**Hedef:** MonitraNG (`mng_common` + `mng_apps`) kurulumu  
**Host:** `app01` / `192.168.1.252`  
**SSH:** `appadmin@192.168.1.252` — [../server/access.md](../server/access.md)

Okuma amaçlı envanter alındı. Paket kurulumu, disk değişikliği ve compose çalıştırılmadı.

## 1. Sunucu

| Alan | Değer |
|------|--------|
| Hostname | `app01` |
| IP | `192.168.1.252/24` (`eth0`) |
| Sanallaştırma | Hyper-V (`systemd-detect-virt`: microsoft) |
| Hyper-V host | `192.168.1.250` |
| İşletim sistemi | Ubuntu 26.04.1 LTS (codename `resolute`) |
| CPU | 4 vCPU, `avx` ve `avx2` var |
| RAM | 5,3 GiB (yaklaşık 4,8 GiB boş) |
| Swap | 4 GiB, kullanılmıyor |
| `/` | LVM `ubuntu-lv`, 72 GiB, yaklaşık 61 GiB boş |
| `/opt` | LVM `app-lv`, 72 GiB, neredeyse boş (yaklaşık 69 GiB) |
| Volume group | `ubuntu-vg` yaklaşık 147 GiB, **boş extent yok** |
| Docker | Yok |
| Git | `/usr/bin/git` |
| Dinleyen port | Yalnızca SSH `22` (yerel DNS stub hariç) |
| UFW | Kapalı |
| `appadmin` | `sudo` grubunda; `sudo` parola ister. `docker` grubu yok |
| Ev dizini | Boş; kurulu bir uygulama yok |
| Dış HTTPS | `gitlab.monitrang.com` yanıt veriyor |
| Posta sunucusu | `192.168.1.253` (`mail.ankarabt.com`) ping ile erişiliyor |

AVX olduğu için MongoDB 7 ve Keycloak imajları CPU yüzünden elenmez. Disk ikiye bölünmüş: kök ve `/opt`. Yeni sanal disk eklemeden bu iki birim büyütülemez.

## 2. Aynı ağdaki diğer makineler

| IP | Rol | Kurulumla ilişkisi |
|----|-----|--------------------|
| `192.168.1.250` | Hyper-V host | RAM veya disk artışı burada yapılır |
| `192.168.1.254` | DC (`DC01`, etki alanı `ANKARABT`) | DNS ve ileride AD. İlk kurulum buna bağlanmak zorunda değil |
| `192.168.1.253` | Mailcow (`mail.ankarabt.com`) | SMTP adayı. Posta portları bu envanterde ölçülmedi |

## 3. Bellek

`mng_common` içinde MongoDB, Postgres, Keycloak, Redis, RabbitMQ, MinIO, Mosquitto ve OpenSearch var. OpenSearch yığını `OPENSEARCH_JAVA_OPTS` ile 1 GiB heap istiyor; süreç bundan daha büyük durur.

`mng_apps` production compose’unda Gateway, Keeper, DataGateway, Reactor, Engine, Hub, Scheduler, Workflow ve worker, Alarm ve worker, LogCollector, Operations, Document, Admin, Notifier, UI, DomainUI, Gotenberg, Collabora ve draw.io vardır.

Fatih Home ve Mustafa Cafe kurulumları bu yığını yaklaşık 14–15 GiB RAM üzerinde, Ollama kapalıyken çalıştırdı. `app01` üzerinde 5,3 GiB var. Mevcut AnkaraBT uygulama override’ı Ollama’ya 4 GiB limit koyuyor; bu limit tek başına VM’nin belleğini doldurur.

Sonuç: tam yığın bu RAM ile kalkmaz. Ollama kapalı olsa bile OpenSearch + Keycloak + Mongo + uygulama konteynerleri 5,3 GiB içinde sıkışır. Kurulumdan önce Hyper-V’de bellek artırılmalı. Hedef, Ollama olmadan **16 GiB**. Ollama ayrı bir karar olarak kalır ve bu bellek hedefinin dışındadır.

## 4. Mevcut AnkaraBT compose dosyaları

Repoda AnkaraBT adına override var. Bunlar **uzak** `portal.ankarabt.com` hostu için yazılmış:

- `ApplicationResources/mng_common/docker-compose.ankarabt.yml`
- `ApplicationResources/mng_apps/docker-compose.ankarabt.apps.yml`
- `ApplicationResources/mng_common/.env.ankarabt.example`
- `ApplicationResources/mng_apps/.env.ankarabt.example`

O dosyalar şu varsayımlara dayanıyor:

- Dış adres `portal.ankarabt.com` (UI `:3000`, DomainUI `:3001`, Gateway `:5040`).
- Aynı hostta Elasticsearch `:9200`, Redmine ve adı `postgres` olan bir konteyner var. OpenSearch host portu bu yüzden `9210`. Postgres konteyner adı `mng_postgres`.
- GitLab, Nginx, MkDocs ve geliştirme arayüzleri `ankarabt-disabled` profiline alınmış; profil verilmeden başlamazlar.
- Ollama bellek limiti 4 GiB. MngLLM stub.

`app01` temiz. Bu çakışmalar yok. Buna rağmen o override’ı burada kullanmak arayüzü ve Keycloak hostname’ini uzak portal adına kilitler. Portal dosyaları olduğu gibi bırakılır. `app01` için ayrı bir yerel override ve `.env` yazılır.

## 5. DNS

Bu yerel ağda olmayan `*.ankarabt.com` adları `192.168.1.1` adresine çözülüyor. Genel DNS’te `app01.ankarabt.com` kaydı yok. DC (`192.168.1.254`) üzerinde gerçek bir A kaydı açılmadan hostname ile erişim güvenilir değil.

İlk erişim adresi IP olmalı: `http://192.168.1.252:3000`.

## 6. Kuruluma engel olmayanlar

- Docker’ın yokluğu kurulum adımıdır, engel değildir.
- `sudo` parola ister; `appadmin` parolası erişim dosyasında kayıtlı.
- UFW kapalı; ilk kurulumda host portları ayrıca bir güvenlik duvarına takılmaz.
- `/opt` boş ve git var. Repo ve Docker verisi bu birime konabilir.
