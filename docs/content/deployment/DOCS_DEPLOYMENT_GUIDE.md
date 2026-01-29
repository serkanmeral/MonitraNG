# Dokümantasyon Yayınlama Rehberi

Bu rehber, MkDocs dokümantasyonunun **lokal Docker Desktop** ve **uzak sunucu** üzerinde nasıl çalıştırılacağını, ayrıca **GitLab CI içindeki docs/pages job'larının** ne işe yaradığını ve nasıl kullanılacağını özetler.

---

## 1. Lokal Docker Desktop’ta Dokümantasyon

### 1.1 MkDocs serve (geliştirme, canlı önizleme)

**Amaç:** `docs/content/` veya `mkdocs.yml` değişince tarayıcıda anında görmek. Python kurmaya gerek yok.

**Yol 1 — `docs/` altında docker-compose (önerilen):**

```bash
cd docs
docker compose -f docker-compose.serve.yml up --build
```

- Tarayıcı: **http://localhost:6010**
- Volume: `.:/docs` → içerik değişince sayfa yenilenir.

**Yol 2 — Tek komutla çalışan image:**

```bash
cd docs
docker build -f Dockerfile.serve -t mkdocs-serve .
docker run --rm -p 6010:8000 -v "${PWD}:/docs" mkdocs-serve
```

- Tarayıcı: **http://localhost:6010**

**Yol 3 — `ApplicationResources/mng_apps` ile birlikte:**

- `docker-compose.yml` içinde `mkdocs` servisi tanımlıysa, tüm stack ile birlikte `mkdocs` da ayağa kalkar.
- Port ve volume ilgili compose dosyasına göre değişir (ör. 8000 veya 6010).

### 1.2 Lokal “production” build (sadece site üretmek)

```bash
cd docs
docker build -t mkdocs-built .
docker run --rm -v "${PWD}/site:/usr/share/nginx/html" mkdocs-built
```

Bu örnekte build sonucu `site/` çıkar; isterseniz farklı bir volume ile nginx’e de verebilirsiniz.

---

## 2. Uzak Sunucuda Dokümantasyon

### 2.1 Hedef davranış

- **URL:** `https://docs.monitrang.com`
- **İçerik:** MkDocs’un `mkdocs build` çıktısı (statik HTML/JS/CSS).
- **Sunucu:** 45.141.151.52; Nginx ile SSL (Let’s Encrypt wildcard).

### 2.2 Sunucuda yapılması gerekenler

| # | Konu | Açıklama |
|---|------|----------|
| 1 | **Dizin** | `/var/www/docs.monitrang.com` — Nginx’in dokümanları serve ettiği root. |
| 2 | **Nginx** | `docs.monitrang.com` için bir server block; `root /var/www/docs.monitrang.com;` ve `index index.html;`. SSL ile birlikte (Let’s Encrypt). |
| 3 | **İçerik kaynağı** | Bu dizin ya pipeline’dan (CI/CD) ya da manuel build+rsync ile doldurulur. |

**Önemli:** Doc’larda anlatılan bazı Nginx örnekleri `docs.monitrang.com`’u şu an **GitLab’a (port 8090)** proxy ediyor. Eğer “kendi” MkDocs sitenizi `docs.monitrang.com`’da göstermek istiyorsanız, o server block’ta **proxy_pass değil**, **static root** kullanılmalı:

```nginx
server {
    listen 443 ssl http2;
    server_name docs.monitrang.com;
    root /var/www/docs.monitrang.com;
    index index.html;
    location / {
        try_files $uri $uri/ /index.html;
    }
    # ssl_certificate / ssl_certificate_key (Let's Encrypt)
}
```

### 2.3 İçeriği sunucuya nasıl götürürsünüz?

**A) CI/CD ile (tercih edilen):**  
`.gitlab-ci.yml` içindeki `deploy-docs-to-server` job’u, `deploy-docs` artifact’ını alıp rsync+ssh ile sunucudaki `/var/www/docs.monitrang.com`’a kopyalar. Şu an **devre dışı** (`when: never`); SSH key ve rules düzenlenerek açılabilir.

**B) Manuel:**  
Lokal veya başka bir makinede `mkdocs build` yapıp `docs/site/` (veya `public/`) içeriğini sunucuya kopyalarsınız:

```bash
# Lokal
cd docs && mkdocs build

# Sunucuya kopyala (örnek)
rsync -avz --delete docs/site/ user@45.141.151.52:/var/www/docs.monitrang.com/
```

**C) Sunucuda build:**  
Repo’yu sunucuda clone edip orada build alıp aynı dizine yazmak da mümkün; ancak genelde CI’da build, sunucuda sadece “serve” tercih edilir.

---

## 3. GitLab CI’daki Docs/Pages Job’ları

### 3.1 Mevcut job’ların özeti

| Job | Ne yapar? | Tetiklenme | Çıktı / Etki |
|-----|-----------|------------|--------------|
| **validate-docs** | Markdown lint + link check | main, develop, MR | Pipeline’ı bloklamaz (`allow_failure: true`). Sadece kalite uyarısı. |
| **deploy-docs** | `mkdocs build` → `docs/site/` | Sadece **main** | Artifact: `docs/site/` (1 gün). Doc değişmediyse build atlanır (incremental). |
| **deploy-docs-preview** | MR için ayrı `mkdocs build` | Sadece **MR** | Artifact: `docs/site/` (7 gün). Environment: `docs-preview/mr-X`. |
| **pages** | `deploy-docs`’tan sonra `docs/site/` → `public/` kopyalar, temizlik yapar | Sadece **main** | Artifact: `public/`. GitLab, **job adı "pages"** ve **paths: public** olduğu için bunu **GitLab Pages**’e otomatik yayınlar. |
| **deploy-docs-to-server** | `docs/site/`’ı rsync+ssh ile sunucuya atar | **Kapalı** (`when: never`) | Hedef: `/var/www/docs.monitrang.com`. SSH key gerekir. |

### 3.2 Pages job’ı sizin için uygun mu?

**GitLab Pages kullanıyorsanız (GitLab’ın kendi hosting’i):**

- **pages** job’ı **uygun ve gerekli**. GitLab, sadece adı `pages` olan ve `public` artifact’ı olan job’un çıktısını Pages’e basar.
- Dokümanlarınızın adresi şu şekilde olur:  
  `https://<namespace>.gitlab.io/<project>/`  
  veya GitLab CE kurulumunuza göre:  
  `https://<gitlab-host>/<namespace>/<project>/-/pages`

**Kendi domain’inizde (docs.monitrang.com) göstermek istiyorsanız:**

- **Seçenek A — GitLab Pages + Nginx proxy:**  
  Pages’i açık bırakırsınız, `docs.monitrang.com` için Nginx’te GitLab Pages URL’ine reverse proxy yaparsınız. Bu durumda **pages** job’ı yine anlamlı; tek “yayın” kaynağı Pages olur.
- **Seçenek B — Sadece kendi sunucu:**  
  Yayını tamamen kendi sunucunuzda (`/var/www/docs.monitrang.com`) yapacaksanız, GitLab Pages’e ihtiyaç yok. O zaman:
  - **pages** job’ını **kaldırmanız** veya **devre dışı bırakmanız** mantıklı (tekrarlı ve boyut sınırına takılma riski).
  - **deploy-docs-to-server**’ı açıp, SSH key’i ayarlayarak dokümanı doğrudan sunucuya göndermek “en uygun yol” olur.

### 3.3 Öneri özeti

| Senaryo | pages job | deploy-docs-to-server | Öneri |
|---------|-----------|------------------------|--------|
| Sadece GitLab Pages (GitLab’ın URL’i yeterli) | **Tut** | Opsiyonel / kapalı | pages yeterli. |
| docs.monitrang.com = GitLab Pages’e proxy | **Tut** | Kapalı | Nginx’te docs.monitrang.com → GitLab Pages URL’i. |
| docs.monitrang.com = kendi sunucu, kendi Nginx | **Kaldır veya devre dışı** | **Aç** (SSH key ile) | Tek kaynak = sunucudaki `/var/www/docs.monitrang.com`. |

### 3.4 deploy-docs-preview (MR preview)

- **Tutulması önerilir.**  
  MR’larda “doküman nasıl görünüyor?” diye bakmak işe yarar.  
- GitLab’ın “environment” URL’i her zaman çalışmayabilir; ama artifact indirilip lokal açılabildiği için yine de faydalı.

### 3.5 pages job’ı kaldırılırsa / devre dışı bırakılırsa

- **Kaldırma:** `.gitlab-ci.yml` içinde `pages:` bloğunu silin veya `when: never` koyun.
- **Sonuç:** GitLab Pages güncellenmez; dokümanı yalnızca `deploy-docs-to-server` (açıksa) veya manuel yöntemle yayınlarsınız.

### 3.6 deploy-docs-to-server’ı açmak için

1. GitLab CI/CD variables’da `SSH_PRIVATE_KEY` veya `DEPLOY_SSH_PRIVATE_KEY` tanımlayın (passphrase’siz key).
2. Bu key’in public kısmını sunucuda `~/.ssh/authorized_keys` içine ekleyin.
3. `.gitlab-ci.yml` içinde `deploy-docs-to-server` kurallarını güncelleyin; örn. `when: never` kaldırıp `if: $CI_COMMIT_BRANCH == "main"` ve `when: manual` yapabilirsiniz.

---

## 4. Hızlı Karar Tablosu

| “Şunu yapmak istiyorum” | Ne yapmalı? |
|-------------------------|------------|
| Lokal’de değiştirirken canlı önizleme | `docs/docker-compose.serve.yml` ile serve (örn. http://localhost:6010). |
| Uzak sunucuda docs.monitrang.com’u kendi Nginx’ten yayınlamak | Sunucuda `/var/www/docs.monitrang.com` + Nginx root; CI’da `deploy-docs-to-server`’ı açıp `pages`’i kapatmak/devre dışı bırakmak. |
| GitLab’ın kendi Pages adresini kullanmak | `pages` job’ını olduğu gibi bırakmak; `deploy-docs-to-server` opsiyonel. |
| MR’da doküman önizlemesi | `deploy-docs-preview` kalsın. |
| CI’da doc değişmediyse build’i atlamak | Mevcut `deploy-docs` mantığı (incremental) zaten var; aynen kullanılabilir. |

---

## 5. İlgili dosyalar

- **CI/CD:** `.gitlab-ci.yml` — `validate-docs`, `deploy-docs`, `deploy-docs-preview`, `pages`, `deploy-docs-to-server`.
- **Lokal serve:** `docs/docker-compose.serve.yml`, `docs/Dockerfile.serve`.
- **Build (production):** `docs/Dockerfile`, `docs/mkdocs.yml`, `docs/requirements.txt`.
- **Sunucu script:** `scripts/infrastructure/deploy-docs-from-artifacts.sh` — artifact/public dizinini `/var/www/docs.monitrang.com` ile uyumlu kullanır.
- **Nginx örnekleri:** `docs/content/infrastructure/nginx.md`, `docs.monitrang.com` server block.

---

*Bu rehber, lokalde ve sunucuda dokümantasyon yayınlama ile GitLab CI docs/pages job’larına ilişkin tek referans olarak güncellenebilir.*
