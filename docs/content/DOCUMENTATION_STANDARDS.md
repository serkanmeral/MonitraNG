# Dokümantasyon Standartları

Bu belge, MonitraNG projesindeki tüm dokümantasyon kurallarının tek referansıdır. Yeni dokümantasyon eklerken veya mevcut dokümantasyonu güncellerken bu standartlara uyulur.

---

## 1. Amaç ve kapsam

- **Amaç:** Dokümantasyonun tutarlı, bulunabilir ve sürdürülebilir olması.
- **Kapsam:** MkDocs içeriği, dosya/klasör yapısı, **backend servisleri için main/support yapısı**, changelog formatı, technical specs (test ekibi API referansı), commit mesajları ve ilgili süreçler.

---

## 2. Genel ilkeler

| Konu | Kural |
|------|--------|
| **Dil** | Kullanıcıya yönelik metinler Türkçe; kod örnekleri, teknik terimler ve değişken adları İngilizce olabilir. |
| **Format** | Markdown (.md). MkDocs Material theme ile uyumlu sözdizimi kullanılır. |
| **Konum** | Tüm dokümantasyon içeriği `docs/content/` altında tutulur (MkDocs `docs_dir: content`). |

---

## 3. Dokümantasyon yapısı

### 3.1 Klasör organizasyonu

- Dokümantasyon **servis/uygulama bazlı** organize edilir.
- Her servis/arayüz için: `docs/content/{ServiceName}/`
- Örnekler: `MngKeeper/`, `MngHub/`, `Mng.Ui/`, `MngDataGateway/`, `MngReactor/`, `MngScheduler/`, `MngEngine/`, `MngNotifier/`, `MngLLM/`, `MngGateway/`, `MngAdmin/`.

### 3.2 Kategoriler

Her servis klasörü altında amaca göre şu kategoriler kullanılır:

| Kategori | Açıklama | Örnek |
|----------|----------|--------|
| `api/` | API dokümantasyonu, OpenAPI/Swagger | `MngKeeper/api/API_DOCUMENTATION.md` |
| `architecture/` | Mimari kararlar, sistem tasarımı, diyagramlar | `MngKeeper/architecture/ARCHITECTURE_GUIDE.md` |
| `guides/` | Kullanım rehberleri, nasıl yapılır | `MngKeeper/guides/GATEWAY_INTEGRATION.md` |
| `changelog/` | Sürüm bazlı değişiklik kayıtları | `MngKeeper/changelog/CHANGELOG.md` |
| `setup/` | Kurulum, konfigürasyon | (gerektiğinde) |
| `deployment/` | Deployment, CI/CD | (genelde `docs/content/cicd/` altında) |
| `troubleshooting/` | Sorun giderme | (gerektiğinde) |
| `specs/` | Teknik spesifikasyonlar | (gerektiğinde) |

### 3.3 Servis kaynak kodu içinde izin verilenler

Servislerin kendi kaynak klasörlerinde (örn. `MngKeeper/`, `Mng.Ui/`) **yalnızca** şunlar bulunabilir:

- `README.md` — Servisin genel açıklaması, hızlı başlangıç
- `ROADMAP.md` veya `RoadMap.md` — Yol haritası

Tüm diğer dokümantasyon (API, mimari, rehberler, changelog) **her zaman** `docs/content/{ServiceName}/` altında tutulur.

### 3.4 Yeni dosya eklerken

1. Servis/ad alanını belirle (örn. MngHub, Mng.Ui).
2. Amaca uygun kategoriyi seç (api, architecture, guides, changelog, vb.).
3. **Backend servisleri** için önce 3.5’e bak; diğer servisler için dosyayı `docs/content/{ServiceName}/{Category}/` altına koy; klasör yoksa oluştur.
4. `docs/mkdocs.yml` içindeki `nav` bölümüne ilgili başlığı ekle.

---

### 3.5 Backend servisleri: main ve support (zorunlu yapı)

Bu kurallar **backend servisleri** için geçerlidir. Hedef servisler: MngKeeper, MngHub, MngDataGateway, MngReactor, MngEngine, MngNotifier, MngScheduler, MngLLM, MngGateway, MngAdmin.

Her backend servis klasörü (`docs/content/{ServiceName}/`) altında **iki ana klasör** bulunur:

| Klasör   | Amaç |
|----------|------|
| `main/`  | Servisin ana, standart dokümanları (changelog, roadmap, teknik spec). Sayı ve içerik sınırlıdır. |
| `support/` | Diğer tüm dokümanlar; amaca göre alt klasörlerde toplanır. |

#### 3.5.1 main/ — Zorunlu ve sabit dosyalar

`main/` altında **yalnızca** aşağıdaki dosyalar yer alır:

| Dosya | Açıklama | Güncelleme |
|-------|----------|------------|
| `CHANGELOG.md` | Sürüm bazlı değişiklik kayıtları (Keep a Changelog + SemVer). | Her commit/push ile otomatik üretilir veya güncellenir. |
| `ROADMAP.md` | Yaptıklarımız, yapacaklarımız, kararlarımız. Ürün/yol haritası ve mimari kararların özeti. | Manuel; periyodik güncelleme. |
| `TECHNICAL_SPECS.md` | Tüm REST/API endpoint’leri için teknik referans. **Test ekiplerinin birincil kaynağı.** | API değiştiğinde güncellenir. |

Tam yol örnekleri:

- `docs/content/MngKeeper/main/CHANGELOG.md`
- `docs/content/MngKeeper/main/ROADMAP.md`
- `docs/content/MngKeeper/main/TECHNICAL_SPECS.md`

Changelog otomasyonu, ilgili servise ait path’lere dokunan commit’lere göre `docs/content/{ServiceName}/main/CHANGELOG.md` dosyasını günceller.

#### 3.5.2 support/ — Diğer tüm dokümanlar

**Kural:** Backend servisi için üretilen veya taşınan **main dışındaki** her doküman, `support/` altında ve **amaca uygun** bir alt klasörde bulunur.

Önerilen alt klasörler:

| Alt klasör | Kullanım | Örnek dosya |
|------------|----------|-------------|
| `architecture/` | Mimari kararlar, sistem tasarımı, diyagramlar. | `ARCHITECTURE_GUIDE.md` |
| `guides/` | Kullanım rehberleri, “nasıl yapılır”, entegrasyon rehberleri. | `GATEWAY_INTEGRATION.md`, `USAGE_GUIDE.md` |
| `setup/` | Kurulum, ortam ve konfigürasyon. | `SETUP.md`, `CONFIGURATION.md` |
| `troubleshooting/` | Sorun giderme, sık hatalar ve çözümleri. | `TROUBLESHOOTING.md` |
| `integration/` | Dış sistemlerle entegrasyon (API contract’lar, event şemaları vb.). | İhtiyaç halinde. |

Yeni bir ihtiyaç çıktığında (ör. “runbook”, “security”) yeni bir `support/` alt klasörü eklenebilir; isim amaca göre İngilizce ve kısa tutulur.

**Özet kural:** Oluşturduğun veya taşıdığın her türlü ek doküman, ilgili backend servisi için `docs/content/{ServiceName}/support/{amaç}/{dosya}.md` konumunda olmalı.

#### 3.5.3 Mevcut yapıdan geçiş

Bugün bazı backend servislerinde `changelog/`, `api/`, `architecture/`, `guides/` doğrudan servis kökünde yer alıyor. Yeni kural:

- **Yeni ve güncellenen dokümanlar** doğrudan `main/` ve `support/` yapısına göre yazılır/taşınır.
- **Mevcut dosyalar** kademeli olarak taşınabilir: `changelog/CHANGELOG.md` → `main/CHANGELOG.md`; `api/`, `architecture/`, `guides/` → `support/` altındaki ilgili klasörlere.
- Changelog otomasyonu hedef path’i `main/CHANGELOG.md` olacak şekilde güncellenir.

---

### 3.6 Technical specs (TECHNICAL_SPECS.md) — İçerik kuralları

`main/TECHNICAL_SPECS.md`, test ekiplerinin kullanacağı **birincil API referansı**dır. Aşağıdakilere uyulur:

1. **Kapsam:** İlgili servisteki **tüm** HTTP/API endpoint’leri listelenir.
2. **Her endpoint için en az:** Method, path, kısa amaç, request (body/query/header) alanları, response yapısı ve örnekleri.
3. **Request alanları:** Her alan için aşağıdaki bilgiler **detaylı** verilir; test senaryoları ve veri setleri buradan türetilebilir.

   | Bilgi | Zorunlu | Açıklama |
   |-------|---------|----------|
   | Alan adı (JSON/query key) | Evet | Tam adı, büyük/küçük harf duyarlılığı belirtilir. |
   | Veri tipi | Evet | string, number, boolean, object, array, enum (değerler listelenir). |
   | Zorunlu / Opsiyonel | Evet | Required / Optional; koşullu zorunluluk varsa açıklanır. |
   | Açıklama | Evet | Alanın işlevi, sınırları, kabul edilen değer aralığı. |
   | Örnek değer | Önerilen | Test için kullanılabilecek gerçekçi değer. |
   | Varsayılan | Opsiyonel | Varsayılan değer varsa yazılır. |

4. **Format:** Markdown tabloları, kod blokları (örnek request/response) ve başlık hiyerarşisi kullanılır; hem **insan** hem **ChatBot** tarafından metin tabanlı parse edilebilir olmalı.
5. **Dil:** Açıklama metinleri Türkçe; alan adları, tipler ve örnekler İngilizce (kod/API ile uyum için).

MkDocs formatı ve sunum detayları (nav, tabs, vs.) ileride ayrıca tanımlanacaktır; şu an içerik yapısı ve alan detayları bu kurallara uyar.

---

### 3.7 Okunabilirlik ve yayınlama

- **ChatBot ve insan:** Tüm bu dokümanlar **düz Markdown** ile yazılır; anlamlı başlıklar (H1–H4), tablolar ve kod blokları kullanılır. Kritik bilgi yalnızca resimde olmamalı; gerekirse resme ek olarak metin özeti bulunur.
- **MkDocs:** Tüm içerik `docs/content/` altında ve `docs/mkdocs.yml` ile uyumlu tutulur; MkDocs ile derlenip yayınlanabilir olmalıdır. MkDocs’e özel sözdizimi (nav, meta, tab’lar vb.) ileride belirlenecektir.

---

## 4. MkDocs kullanımı

- **Config:** `docs/mkdocs.yml`
- **İçerik dizini:** `docs/content/` (`docs_dir: content`)
- **Build çıktısı:** `docs/site/`
- **Yerel önizleme:** `mkdocs serve` (genelde `docs/` içinden veya proje kökünden uygun context ile)

Nav yapısı `mkdocs.yml` içinde tanımlıdır. Yeni sayfa ekledikten sonra ilgili `nav` girdisini eklemek gerekir; aksi halde sayfa sitede görünmez.

---

## 5. Changelog kuralları

### 5.1 Her servis/arayüz için ayrı changelog

- **Kural:** Her servis ve her arayüz (ör. Mng.Ui) için **tek bir** changelog dosyası vardır.
- **Konum:**
  - **Backend servisleri** (3.5’e tâbi): `docs/content/{ServiceName}/main/CHANGELOG.md`
  - **Diğer servisler / arayüzler:** `docs/content/{ServiceName}/changelog/CHANGELOG.md`
- **MkDocs:** Bu dosya, ilgili servisin sayfasında “Changelog” bağlantısı ile sunulur (`mkdocs.yml` nav’da tanımlanır).

Örnek servisler: MngKeeper, MngHub, MngDataGateway, MngReactor, MngEngine, MngNotifier, MngScheduler, MngLLM, MngGateway, MngAdmin, Mng.Ui, MngDomainUI (tanımlandığında).

### 5.2 Format: Keep a Changelog

Changelog formatı **[Keep a Changelog](https://keepachangelog.com/en/1.0.0/)** standardına uygundur. Sürüm numaraları **[Semantic Versioning](https://semver.org/spec/v2.0.0.html)** (SemVer) kullanır.

Örnek iskelet:

```markdown
# {ServisAdı} Changelog

Tüm önemli değişiklikler bu dosyada dokümante edilir.

Format [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) standardına uygundur.
Versiyonlama [Semantic Versioning](https://semver.org/spec/v2.0.0.html) kullanır.

## [Unreleased]

### Added
- (henüz release edilmemiş eklemeler)

### Changed
- (henüz release edilmemiş değişiklikler)

### Fixed
- (henüz release edilmemiş düzeltmeler)

## [X.Y.Z] - YYYY-MM-DD

### Added
- (madde listesi)

### Changed
- (madde listesi)

### Fixed
- (madde listesi)
```

### 5.3 Başlıklar: Added / Fixed / Changed

- **Added:** Yeni özellikler, yeni API’ler, yeni konfigürasyonlar.
- **Changed:** Mevcut davranışın veya arayüzün değişmesi (breaking olmayan).
- **Fixed:** Hata düzeltmeleri.

İhtiyaç olursa **Deprecated**, **Removed**, **Security** gibi ek başlıklar da kullanılabilir.

### 5.4 İçerik kaynağı: commit mesajları

- Changelog maddeleri, ilgili sürüm için yapılan **commit mesajlarından** türetilir.
- Otomasyon (versiyon bump + changelog güncelleme) kullanıldığında, `origin/main..HEAD` aralığında o servise ait path’lere dokunan commit’ler listelenir.
- Maddeler kısa, okunabilir ve teknik olarak doğru olmalıdır; gerekirse commit mesajı sadeleştirilip buraya uyarlanır.

### 5.5 Conventional Commits ile uyum (önerilen)

Commit mesajlarında **Conventional Commits** kullanılması önerilir; böylece changelog başlıklarına otomatik eşleme yapılabilir:

| Commit tipi | Changelog bölümü |
|-------------|-------------------|
| `feat:`     | **Added**         |
| `fix:`      | **Fixed**         |
| `docs:`     | **Changed** (veya ayrı “Documentation” altında) |
| `refactor:`, `perf:`, `chore:` (davranış değişikliği varsa) | **Changed** |
| `BREAKING CHANGE:` | **Changed** veya ayrı “Breaking changes” altı |

Conventional Commits kullanıldığında otomatik changelog üretimi daha tutarlı olur; kullanılmadığında maddeler genelde **Changed** altında listelenebilir.

### 5.6 İsteğe bağlı: YAML frontmatter

MkDocs ve arama için metadata eklemek isterseniz, dosya başına YAML frontmatter kullanılabilir (MngKeeper örneğinde olduğu gibi):

```yaml
---
title: "MngKeeper Changelog"
category: "changelog"
tags: ["keeper", "changelog", "version", "releases"]
service: "MngKeeper"
language: "tr"
---
```

Zorunlu değildir; mevcut sayfalarla tutarlılık için kullanılabilir.

---

## 6. Commit mesajları (Conventional Commits özeti)

Dokümantasyon ve kod için ortak kullanım önerisi:

- `feat: ...` — Yeni özellik
- `fix: ...` — Hata düzeltme
- `docs: ...` — Sadece dokümantasyon değişikliği
- `refactor: ...` — Davranış değiştirmeyen kod değişikliği
- `chore: ...` — Build, script, sürüm bump vb.
- `BREAKING CHANGE:` — Uyumluluğu bozan değişiklik (gövde veya footer’da belirtilir)

Bu yapı, hem changelog otomasyonunu hem de geçmişi okumayı kolaylaştırır.

---

## 7. Dokümantasyon oluşturma ve güncelleme

- **Oluşturma:** Kullanıcı veya AI “döküman hazırla”, “dokümantasyon oluştur”, “spec yaz” vb. talep etmedikçe yeni .md dosyaları oluşturulmaz.
- **Güncelleme:** “Dokümantasyon güncelle”, “docs güncelle”, “roadmap güncelle” vb. açık talep olmadan mevcut dokümantasyon dosyaları değiştirilmez.
- **Kaynak:** Bu standartlar, proje kuralları (örn. `.cursorrules`) ile uyumludur; çakışma durumunda **bu belge esas alınır**.

---

## 8. Referanslar

- [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
- [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
- [Conventional Commits](https://www.conventionalcommits.org/)
- [MkDocs](https://www.mkdocs.org/)
- [MkDocs Material](https://squidfunk.github.io/mkdocs-material/)

---

*Son güncelleme: Backend servisleri için `main/` ve `support/` yapısı, TECHNICAL_SPECS.md kuralları ile ChatBot/insan okunabilirliği ve MkDocs uyumluluğu eklendi. Güncellemeler bu dosya üzerinden yapılır.*
