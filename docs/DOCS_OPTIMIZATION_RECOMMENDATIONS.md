# docs/ Klasörü — Optimizasyon Önerileri

**Tarih:** 26 Ocak 2026  
**Amaç:** `docs/` yapısını sadeleştirmek, tek kaynağa (`content/`) dayandırmak ve bulunabilirliği artırmak.

Bu belge, [DOCS_ORGANIZATION_PLAN.md](DOCS_ORGANIZATION_PLAN.md) ile uyumlu, öncelikli ve uygulanabilir önerileri toplar.

---

**Uygulandı (26 Ocak 2026):** Yüksek ve orta öncelikli maddeler uygulandı: DevOps Roadmap content’e alındı, docs/cicd–deployment–infrastructure kaldırılıp içerik content’e taşındı, kök .md’ler content altına taşındı, docs/docs kaldırıldı, geçici notlar arşive alındı. Ayrıntılar DOCS_ORGANIZATION_PLAN Aşama 4–6’da işlendi.

---

## 1. Öncelik sırasıyla aksiyonlar

### 🔴 Yüksek öncelik (hemen yapılabilir)

| Öneri | Açıklama | Etki |
|-------|----------|------|
| **Nav’daki kırık “DevOps Roadmap” linkini düzelt** | `mkdocs.yml` nav’da `devops-roadmap.md` kullanılıyor; bu dosya `content/` içinde yok. `docs/devops-roadmap.md` veya `docs/docs/devops-roadmap.md` içeriğini `content/devops-roadmap.md` veya `content/cicd/DEVOPS_ROADMAP.md` olarak taşıyıp nav’ı buna göre güncelle. | Build/nav hatası önlenir. |
| **docs/cicd → content/cicd birleştirme** | `docs/cicd/` içinde 14 dosya var; `content/cicd/` zaten 35+ dosya içeriyor. docs/cicd’te olup content’te olmayanları (isim farklarıyla) kontrol et; eksikleri content’e kopyala, sonra `docs/cicd/` klasörünü kaldır. | Tek kaynak (content), karışıklık azalır. |
| **docs/deployment → content’e taşı** | `content/deployment/` yok. `docs/deployment/` altındaki .md’leri `content/deployment/` altına taşı; nginx snippet vb. konfig dosyalarını da burada veya `content/infrastructure/` altında tut. Ardından `docs/deployment/` silinir. | Tüm deployment dokümanı MkDocs’ta derlenir. |
| **docs/infrastructure → content’e taşı** | `content/infrastructure/` oluştur, `docs/infrastructure/` içindeki tüm .md’leri oraya taşı. mkdocs nav’a “Infrastructure” bölümü ekle. Sonra `docs/infrastructure/` kaldır. | Altyapı dokümanı tek yerde ve nav’da görünür. |

### 🟡 Orta öncelik (planlı yapılmalı)

| Öneri | Açıklama | Etki |
|-------|----------|------|
| **Kök .md dosyalarını content’e taşı** | CERTIFICATE_MANAGEMENT_PLAN, DEVOPS_ROADMAP, DOCKER_DEPLOYMENT, HOSTING_*, INFRASTRUCTURE_*, MKdocs_KULLANIM, MKDOCS_SETUP, prd, ROADMAP, PYTHON_*, TEMPLATE_README vb. → ilgili content alt klasörüne (cicd, infrastructure, MkDocs, strategic vb.). README.md docs kökünde kalsın. | docs kökü sadeleşir; tüm içerik content’te. |
| **docs/docs/ temizliği** | `docs/docs/devops-roadmap.md` ve `index.md` içeriğini content’e al (veya mevcut cicd/roadmap ile birleştir). `docs/docs/` klasörünü kaldır. | İç içe “docs/docs” karışıklığı biter. |
| **DEVOPS_ROADMAP / devops-roadmap birleştirme** | İki ayrı dosya varsa, güncel olanı “DevOps Roadmap” tek kaynak yap; diğerini arşivle veya sil. Nav bu tek dosyayı göstermeli. | Çift roadmap kalkar. |
| **Geçici / tek kullanımlık notları arşivle** | NAVIGATION_CHECK.md, restore_files.md vb. geçici ise `docs/archive/` altına taşı veya sil. | Gürültü azalır. |

### 🟢 Düşük öncelik (bakım / iyileştirme)

| Öneri | Açıklama | Etki |
|-------|----------|------|
| **Legacy “README-only” klasörlerini toplu tutma kararı** | MngKeeper, MngDataGateway, Mng.Ui, MngLLM, MngMobile şu an sadece “içerik content’e taşındı” README’si içeriyor. İsterseniz bu klasörleri tamamen kaldırıp tek bir `docs/LEGACY_MIGRATION_README.md` ile yönlendirme yapabilirsiniz; ya da mevcut README’lerle bırakıp yeni içerik eklenmesini engellersiniz. | Kök altındaki klasör sayısı azalır veya net kurala kavuşur. |
| **content/cicd için index / özet sayfası** | 35+ dosya tek klasörde; “CI/CD” nav girişi doğrudan current_status’a gidiyor. `content/cicd/README.md` veya `index.md` ile “Bu bölümde neler var” özeti + kısa link listesi eklenebilir. | Okur yolunu bulur. |
| **content/deployment ve content/infrastructure index’leri** | Taşıma sonrası her biri için bir index/README sayfası tanımlanırsa nav’da “Deployment” / “Infrastructure” tek tıklamayla genel bakış sunar. | Tutarlılık ve bulunabilirlik. |
| **Internal link denetimi** | Taşımalardan sonra sayfalar arası `[metin](path)` linklerinin güncel path’lere işaret ettiğini kontrol et. Gerekirse `mkdocs build` + basit bir link checker (örn. markdown-link-check) kullan. | Kırık linkler azalır. |

---

## 2. Yapısal öneriler

### 2.1 Tek kaynak kuralı

- **Canonical konum:** Yalnızca `docs/content/` MkDocs ile derlenir. Yeni eklenen her şey doğrudan `content/` altında, uygun servis/kategoriyle konumlandırılmalı.
- **docs kökü:** Mümkün olduğunca sadece şunlar kalsın: `mkdocs.yml`, `README.md`, `DOCS_ORGANIZATION_PLAN.md`, `DOCS_OPTIMIZATION_RECOMMENDATIONS.md`, `requirements.txt`, `run_mkdocs.ps1`, `Dockerfile`, `.dockerignore`. Tüm “içerik” dosyaları content’e taşınmalı.

### 2.2 content/ altı hedef yapı (kısa)

```
content/
├── index.md
├── DOCUMENTATION_STANDARDS.md
├── devops-roadmap.md  veya  cicd/DEVOPS_ROADMAP.md   # Nav’da tek “DevOps Roadmap”
├── cicd/              # Tüm CI/CD + GitLab dokümanları
├── deployment/        # Deploy, pipeline, nginx snippet vb.
├── infrastructure/    # Nginx, mail, DNS, port, letsencrypt, vb.
├── api/               # Mevcut API overview + servis index’leri
├── MkDocs/            # MkDocs kullanımı, kurulumu
├── strategic/ veya kök  # prd.md, ROADMAP.md (ürün roadmap) — isteğe bağlı
└── {ServiceName}/     # main/ + support/ (backend) veya guides/ vb. (frontend)
```

### 2.3 Nav sadeleştirme

- **DevOps:** “CI/CD” + “DevOps Roadmap” yerine, gerekirse tek “DevOps” sekmesi altında “Genel bakış”, “CI/CD rehberleri”, “Roadmap” gibi alt başlıklar.
- **Infrastructure / Deployment:** Yeni “Infrastructure” ve “Deployment” bölümleri eklenirse, çok sayfa tek tek nav’a yazmak yerine birer index sayfası + “Daha fazla” ile genişletilebilir.

### 2.4 Güncellik ve arşiv

- **“current_status” / “NEXT_STEPS” / “RUNNER_FIX_STEP2” türü dosyalar:** İçerik hâlâ geçerliyse ilgili kalıcı rehbere (örn. troubleshooting, setup) taşınabilir; sadece tarihsel değeri varsa `docs/archive/` altına alınır.
- **Tamamlanmış proje notları:** “GATEWAY_INTEGRATION_COMPLETE” benzeri belgeler, özetleri CHANGELOG/ROADMAP’e alındıysa arşivlenebilir; böylece “aktif rehber” ile “tarihsel not” ayrılır.

---

## 3. Hızlı kontrol listesi

Optimizasyon sonrası şunlar sağlanmış olmalı:

- [ ] `mkdocs build` hatasız çalışıyor.
- [ ] Nav’daki tüm sayfa yolları `content/` içinde var.
- [ ] “DevOps Roadmap” (ve varsa diğer kök .md’ler) content’te ve nav’da doğru.
- [ ] docs kökünde yalnızca config, README, plan ve araç dosyaları var; “içerik” yok.
- [ ] `docs/cicd/`, `docs/deployment/`, `docs/infrastructure/` kaldırıldı; içerikleri `content/` altında.
- [ ] DOCS_ORGANIZATION_PLAN’daki Aşama 4–7 ile uyumlu ilerleme kaydedildi.

---

## 4. Özet tablo

| Konu | Öneri | Zorluk |
|------|--------|--------|
| Nav “DevOps Roadmap” | İçeriği content’e taşı, nav’ı güncelle | Düşük |
| docs/cicd | content/cicd ile birleştir, docs/cicd’i kaldır | Orta |
| docs/deployment | content/deployment oluştur, taşı, kaldır | Düşük |
| docs/infrastructure | content/infrastructure oluştur, taşı, nav ekle, kaldır | Orta |
| Kök .md’ler | Amaca göre content altına taşı | Orta |
| docs/docs | İçeriği al, klasörü kaldır | Düşük |
| Roadmap çoklamaları | Tek “DevOps Roadmap” kaynağı | Düşük |
| Geçici notlar | Arşivle veya sil | Düşük |
| Legacy README klasörleri | Toplu kaldırma veya net kural | Düşük |
| cicd/infrastructure index | Özet sayfaları ekle | Düşük |
| Internal link kontrolü | Build + link checker | Orta |

Bu öneriler, mevcut DOCS_ORGANIZATION_PLAN ile uyumlu olacak şekilde aşama aşama uygulanabilir; önce yüksek öncelikli maddeler bitirilirse build ve tek-kaynak hedefi hızla sağlanır.
