# docs/ Klasörü — Organizasyon ve Temizlik Planı

**Tarih:** 26 Ocak 2026  
**Amaç:** Tüm dokümantasyonu `docs/content/` (MkDocs kaynağı) etrafında toplamak, duplicate ve geçersiz dosyalardan kurtulmak.

---

## 1. Temel kural

- **Canonical konum:** `docs/content/` — MkDocs `docs_dir: content` ile sadece bu klasör derlenir.
- **Backend servisleri:** `docs/content/{ServiceName}/main/` + `support/` (DOCUMENTATION_STANDARDS §3.5).
- **Diğer her şey:** Ya `content/` altına taşınmalı ya arşivlenmeli ya da silinmeli.

---

## 2. docs/ kökündeki yapı (özet)

| Konum | İçerik | Öneri |
|-------|--------|--------|
| **content/** | MkDocs kaynağı, servisler (main/support), cicd, api, Mng.Ui | Koru; tek gerçek kaynak. |
| **archive/** | MngKeeper WORK_SESSION arşivi | Koru; tarihsel notlar için. |
| **cicd/** | 14 adet GitLab/CI dokümanı | `content/cicd/` ile karşılaştır; duplicate’leri sil, eksikleri content’e taşı, sonra bu klasörü kaldır. |
| **deployment/** | 8 .md + 1 .conf (deploy, pipeline, nginx) | `content/deployment/` veya `content/cicd/` altına taşı; sonra klasörü kaldır. |
| **infrastructure/** | ~40 dosya (nginx, gitlab, mail, letsencrypt, port, mngkeeper-env) | `content/infrastructure/` oluştur, taşı; sonra klasörü kaldır. |
| **MngKeeper/** | Taşınan dosyaların geri kalanı (api, changelog, guides, licensing, setup, specs) | İçerik `content/MngKeeper/support/`’a taşındı. **Klasörü tamamen silebilirsin** (veya sadece arşiv için bir README bırak). |
| **MngAdmin/** | 2 .md (backup-configuration, DOCKER_SETUP) | `content/MngAdmin/support/guides/` veya `support/setup/` olarak taşı; sonra sil. |
| **MngDataGateway/** | 22+ .md | content/MngDataGateway zaten dolu. Karşılaştır; taşı/ birleştir; legacy’yi sil. |
| **MngDomainUI/** | 4 .md (BACKUP, current_status, QUICK_START, ROADMAP) | `content/MngDomainUI/` yoksa oluştur; bu dosyaları oraya taşı. |
| **MngGateway/** | changelog/GATEWAY_INTEGRATION_COMPLETE.md | `content/MngGateway/support/guides/` veya changelog mantığına göre main’e özet; sonra sil. |
| **MngHub/** | 4 .md | content/MngHub/support ile karşılaştır; taşı/sil. |
| **Mng.Ui/** | architecture, guides, i18n, specs, tst_book_data (root’ta) | content/Mng.Ui zaten var. Root’taki fazlalığı content’e taşı veya duplicate ise sil. |
| **MngLLM/** | 16 .md (planning, analysis, roadmap, vb.) | content/MngLLM/support/ altına taşı (guides, specs); sonra legacy’yi sil. |
| **MngMobile/** | ROADMAP.md | İleride content/MngMobile eklenirse oraya; şimdilik content’e veya archive’a. |
| **MngNotifier/** | 3 .md | content/MngNotifier/support/guides’a taşı; sil. |
| **MngScheduler/** | ROADMAP.md | content/MngScheduler/main/ROADMAP ile birleştir veya support’a taşı; sil. |
| **docs/docs/** | devops-roadmap.md, index.md | Muhtemelen eski/hatalı iç içe yapı. content’e alınacaklar alınıp bu klasör kaldırılmalı. |
| **Kök .md dosyaları** | Aşağıda listelenenler | Hepsi content veya ilgili alt klasöre taşınmalı; kök sadece mkdocs.yml, planlar, README kalsın. |

---

## 3. docs/ kökündeki tekil .md dosyaları

| Dosya | Öneri |
|-------|--------|
| CERTIFICATE_MANAGEMENT_PLAN.md | content/infrastructure/ veya content/cicd/ |
| DEVOPS_ROADMAP.md | content/cicd/ veya tek “DevOps Roadmap” ile birleştir |
| devops-roadmap.md | content/devops-roadmap.md (nav buna bakıyor olabilir) veya content/cicd/ |
| DOCKER_DEPLOYMENT.md | content/cicd/ veya deployment/ |
| HOSTING_RESOURCE_REQUIREMENTS.md | content/infrastructure/ veya content/cicd/ |
| INFRASTRUCTURE_OVERVIEW.md | content/infrastructure/ |
| INFRASTRUCTURE_TEMPLATE_STRATEGY.md | content/infrastructure/ |
| MKdocs_KULLANIM.md | content/MkDocs/ veya docs/README’e özet |
| MKDOCS_SETUP.md | content/MkDocs/ |
| NAVIGATION_CHECK.md | Geçici kontrol notu ise arşivle veya sil |
| prd.md | content/ veya docs/ kökünde kalacak “strateji” klasörü (ör. content/strategic/) |
| PYTHON_INSTALLATION_GUIDE.md | content/setup/ veya infrastructure/ |
| README.md | docs/ kökünde kalsın (proje dokümantasyon girişi) |
| restore_files.md | Arşiv veya sil (geçici not ise) |
| ROADMAP.md | Genel ürün roadmap → content/ROADMAP.md veya strategic/ |
| TEMPLATE_README.md | Şablon ise content/ veya docs/ şablon klasörüne |

---

## 4. Önerilen aşamalar

### Aşama 1 — Tamamlandı
- [x] MngKeeper: legacy içerik `content/MngKeeper/support/` altına taşındı.
- [x] Duplicate CHANGELOG ve GATEWAY_INTEGRATION silindi.
- [x] WORK_SESSION arşive alındı.

### Aşama 2 — MngKeeper legacy klasörünü kaldır
- [ ] `docs/MngKeeper/` içinde kalan tüm dosyaların content’te karşılığı var mı kontrol et.
- [ ] Varsa `docs/MngKeeper/` klasörünü tamamen sil (veya içine “Bu içerik content/MngKeeper/support/ altına taşındı” diyen bir README.md bırakıp diğer dosyaları sil).

### Aşama 3 — Diğer servis legacy’lerini content’e taşı
- [x] MngAdmin (2), MngNotifier (3), MngScheduler (1), MngGateway (1), MngHub (4): ilgili `content/.../support/` altına taşındı; eski klasörler boşaltıldı.
- [x] MngDomainUI: `content/MngDomainUI/` oluşturuldu, 4 dosya taşındı.
- [x] MngLLM: dosyalar content/MngLLM/support/ altında; legacy temizlendi, README bırakıldı.
- [x] MngDataGateway: content zaten tam; legacy silindi, README bırakıldı.
- [x] Mng.Ui: eksikler `content/Mng.Ui/support/` altına (architecture, guides, specs, i18n, tst_book_data) taşındı; legacy temizlendi, README bırakıldı.

### Aşama 4 — cicd / deployment / infrastructure — Tamamlandı (26 Ocak 2026)
- [x] `docs/cicd/` kaldırıldı (içerik zaten content/cicd’teydi).
- [x] `content/deployment/` oluşturuldu, docs/deployment içeriği taşındı, docs/deployment silindi.
- [x] `content/infrastructure/` oluşturuldu, docs/infrastructure taşındı, nav’a Infrastructure eklendi, docs/infrastructure silindi.

### Aşama 5 — Kök .md dosyaları — Tamamlandı
- [x] CERTIFICATE_MANAGEMENT_PLAN, DOCKER_DEPLOYMENT, HOSTING_*, INFRASTRUCTURE_* → content/infrastructure/ veya content/cicd/.
- [x] MkDocs ile ilgilileri content/MkDocs/ altına taşındı (MKDOCS_SETUP, MKdocs_KULLANIM, PYTHON_INSTALLATION_GUIDE, TEMPLATE_README).
- [x] prd.md, ROADMAP.md → content/; DevOps Roadmap → content/devops-roadmap.md (nav zaten buna bakıyor).
- [x] NAVIGATION_CHECK, restore_files → docs/archive/temp-notes/.

### Aşama 6 — docs/docs/ ve tekrarlar — Tamamlandı
- [x] content/devops-roadmap.md zaten mevcut; docs/docs/ kaldırıldı.
- [x] DEVOPS_ROADMAP / devops-roadmap: Tek kaynak content/devops-roadmap.md; kök kopyalar silindi.

### Aşama 7 — Son kontroller
- [ ] `mkdocs build` ve `mkdocs serve` ile derleme ve nav’ın doğru çalıştığını kontrol et.
- [ ] Tüm internal linklerin (relative path) güncel olduğunu kontrol et.
- [ ] DOCUMENTATION_STANDARDS’a uygunluk: backend servisleri main/support, diğerleri content altında mantıklı kategorilerde.

---

## 5. İleride silinebilecek / arşivlenecek tipler

- **Geçici oturum notları** (örn. “WORK_SESSION_…”, “NEXT_SESSION_TODO”)
- **“current_status” / “RUNNER_FIX_STEP2” gibi tek seferlik fix notları** — Özeti kalıcı bir rehbere alındıysa arşivlenebilir.
- **Aynı konuda birden fazla plan** (ör. “gitlab-pages-*-.md”) — Güncel olanı bırak, diğerlerini arşivle.
- **Eski “GATEWAY_INTEGRATION_COMPLETE” gibi tamamlanmış iş özetleri** — Changelog veya ROADMAP’e alındıysa arşivlenebilir.

---

## 6. Özet

| Aşama | Özet |
|-------|--------|
| 1 | MngKeeper taşıma ve arşiv (yapıldı) |
| 2 | docs/MngKeeper klasörünü kaldır |
| 3 | MngAdmin, MngDataGateway, MngDomainUI, MngGateway, MngHub, MngLLM, MngMobile, MngNotifier, MngScheduler legacy → content |
| 4 | docs/cicd, deployment, infrastructure → content |
| 5 | Kök .md’ler → content altına |
| 6 | docs/docs temizliği, roadmap birleştirme |
| 7 | Build + link kontrolü |

Bu plan, `docs/` içindeki dağınık yapıyı tek kaynak (`content/`) etrafında toplamak ve geçersiz/tekrarlayan dokümanları azaltmak için kullanılabilir. Her aşamayı sırayla uygulayıp test ederek ilerlemek en güvenli yöntemdir.
