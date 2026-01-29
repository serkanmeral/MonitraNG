# Aynı / Benzer Amaçlı Dokümanlar — Tespit Raporu

**Tarih:** 26 Ocak 2026  
**Amaç:** Aynı veya benzer konuyu ele alan, birleştirilebilecek veya netleştirilmesi gereken dokümanları listelemek.

---

## 1. Yapılan temizlik (boş klasörler)

- **docs kökü:** `MngAdmin`, `MngDomainUI`, `MngNotifier`, `MngScheduler` tamamen boş oldukları için kaldırıldı.
- **content altı:** Servislerde kullanılmayan boş stub klasörler kaldırıldı:
  - `MngAdmin/api`, `architecture`, `guides`
  - `MngDataGateway/api`, `architecture`, `guides`
  - `MngEngine`, `MngGateway`, `MngHub`, `MngKeeper`, `MngLLM`, `MngNotifier`, `MngReactor`, `MngScheduler` altındaki boş `api`, `architecture`, `guides`
  - `MngKeeper/changelog`, `MngKeeper/guides`

Gerçek içerik zaten `main/` ve `support/` (support/architecture, support/guides vb.) altında olduğu için bu boş dallar gereksizdi.

---

## 2. Deployment / CI/CD — Çakışan veya örtüşen rehberler

Aynı “deploy / pipeline / GitLab” alanına dokunan birden fazla rehber var. Hepsi farklı detay seviyesi veya farklı okuyucu (ilk kurulum vs referans) için olabilir; ilişkileri netleştirmek faydalı.

| Dosya | Konu | Öneri |
|-------|------|------|
| **cicd/DEPLOYMENT_GUIDE.md** | GitLab CI/CD deployment, variables, SSH, job adımları | Ana “nasıl deploy ederim?” rehberi olarak bırak; diğerlerine “Detay için DEPLOYMENT_REFERENCE / CICD_DEPLOYMENT_COMPLETE_GUIDE” linki ekle. |
| **cicd/DEPLOYMENT_REFERENCE.md** | Pipeline yapısı, servis listesi, docker-compose, checklist | Referans / “tek doğru kaynak” olarak kalsın; DEPLOYMENT_GUIDE ve CICD_DEPLOYMENT_COMPLETE_GUIDE bu dosyaya atıfta bulunsun. |
| **cicd/DEPLOYMENT_TRIAL_PLAN.md** | ~~Deploy denemesi, tetikleme, doğrulama, rollback~~ | **Yapıldı:** İçerik [DEPLOYMENT_GUIDE](cicd/DEPLOYMENT_GUIDE.md) içinde “İlk Deploy Denemesi ve Checklist” bölümüne birleştirildi; dosya kaldırıldı. |
| **cicd/CICD_DEPLOYMENT_COMPLETE_GUIDE.md** | Kapsamlı rehber, konfig, troubleshooting, geri dönüş noktaları | Uzun “her şey bir arada” rehber; girişte “Hızlı başlangıç için DEPLOYMENT_GUIDE, referans için DEPLOYMENT_REFERENCE” yazılabilir. |
| **cicd/HOSTING_CI_CD_DEPLOYMENT_ROADMAP.md** | 7 fazlı hosting + GitLab + Runner + SSL yol haritası | “Sıfırdan sunucu + GitLab + CI/CD” için ana rehber; GITLAB_SETUP_GUIDE ve GITLAB_CI_CD_GUIDE ile konu dağılımı bir paragrafla açıklanabilir. |
| **cicd/AUTOMATED_DEPLOYMENT_WORKFLOW.md** | Otomatik deployment akışı, script’ler, stratejiler | “Workflow / otomasyon” odağı; DEPLOYMENT_GUIDE veya CICD_DEPLOYMENT_COMPLETE_GUIDE’da “Otomasyon: AUTOMATED_DEPLOYMENT_WORKFLOW” linki verilebilir. |
| **cicd/DOCKER_DEPLOYMENT.md** | **MkDocs** Docker build/deploy (docs sitesi) | Konu farklı (dokümantasyon deploy); başlıkta “MkDocs” vurgulansın, “uygulama deployment” ile karışmasın. |
| **deployment/DEPLOYMENT_ROADMAP.md** | Sunucu, ortam, SSL, Nginx, backup, timeline | “Deployment süreci ve planlama” için ana doküman; cicd rehberleri “teknik pipeline” odaklı, bu “ne yapacağız, sırası ne” odaklı. Girişte “Pipeline tarafı için cicd/…” denebilir. |

**Özet:** **Yapıldı:** [cicd/INDEX.md](cicd/INDEX.md) eklendi; “İlk kez deploy / Referans / Kurulum / MkDocs / GitLab / Görev analizi” ayrımı bu indeksten yapılıyor. **Runner notları:** GITLAB_RUNNER_SUCCESS, SUCCESSFUL_RUNNER_CONFIGURATION, RUNNER_FIX_STEP2, RUNNER_ISSUES_FOUND → [RUNNER_AND_PIPELINE_NOTES](cicd/RUNNER_AND_PIPELINE_NOTES.md) içinde birleştirildi; eski 4 dosya kaldırıldı.

---

## 3. Roadmap / yol haritası dokümanları

Farklı kapsamda roadmap’ler var; hepsi bilinçli olabilir, ancak isim ve yer tutarlılığı iyi olmalı.

| Dosya | Kapsam | Öneri |
|-------|--------|--------|
| **content/ROADMAP.md** (nav: “Product Roadmap”) | **İçerik şu an MngNotifier roadmap.** | **Hata:** Nav “Product Roadmap” gösteriyor, dosya MngNotifier’a ait. Ya bu dosya `MngNotifier/main/ROADMAP.md` ile değiştirilir / yönlendirilir ya da buraya gerçek “ürün roadmap” içeriği konur. |
| **content/devops-roadmap.md** | DevOps (MkDocs, CI/CD, deploy, SonarQube, K8s) | Ürün genelindeki “DevOps roadmap” olarak net; olduğu gibi kalabilir. |
| **deployment/DEPLOYMENT_ROADMAP.md** | Deployment süreci, ortam, SSL, Nginx, zamanlama | Deployment planlama roadmap’i; isim ve konum uyumlu. |
| **cicd/HOSTING_CI_CD_DEPLOYMENT_ROADMAP.md** | Hosting + GitLab + Runner + pipeline + SSL | CI/CD/hosting roadmap’i; devops-roadmap veya DEPLOYMENT_ROADMAP ile “hangi roadmap nerede” bir cümleyle açıklanabilir. |
| **cicd/ROADMAP_ANALYSIS.md** | Tamamlanan / bekleyen görevler, öncelikler | Görev listesi / analiz; “roadmap” yerine “Görev analizi” vb. isim düşünülebilir. |
| **MngMobile/ROADMAP.md**, **MngDomainUI/guides/ROADMAP.md**, **Mng.Ui/support/guides/i18n/ROADMAP.md** | Servis/alan roadmap’leri | Tasarlanmış; değişiklik gerekmez. |
| **Servis main/ROADMAP.md** (MngKeeper, MngHub, …) | Servis bazlı | Standart yapı; dokunulmayabilir. |

**Kritik:** `content/ROADMAP.md` nav’da “Product Roadmap” ama içerik MngNotifier. Bu mutlaka düzeltilmeli (ya içerik ya da nav hedefi).

---

## 4. GitLab rehberleri

Hepsi “GitLab” konulu; amaç ve okuyucuya göre bölünmüş. Çakışma yerleri kısa açıklamalarla netleştirilebilir.

| Dosya | Odak | Öneri |
|-------|------|--------|
| **GITLAB_SETUP_GUIDE.md** | İlk kurulum, proje oluşturma, push, runner kaydı | “İlk kez GitLab kuran” için ana giriş. |
| **GITLAB_CI_CD_GUIDE.md** | Pipeline yapısı, stage’ler, job’lar | “Pipeline’ı anlamak / değiştirmek” için. |
| **GITLAB_RUNNER_TOKEN_GUIDE.md** | Runner token bulma yöntemleri | Token odaklı; GITLAB_SETUP veya runner bölümünde “Token: GITLAB_RUNNER_TOKEN_GUIDE” linki. |
| **GITLAB_MIGRATION_GUIDE.md** | Hosting’e taşıma, yedekleme/restore | Taşıma senaryosu; diğerleriyle çakışmıyor. |
| **HOSTING_CI_CD_DEPLOYMENT_ROADMAP.md** | GitLab + Runner + SSL dahil 7 fazlı kurulum | “Sıfırdan sunucu + GitLab” için yol haritası; GITLAB_SETUP “proje/push/runner”, bu “sunucu + GitLab kurulumu” ayrımı girişte yazılabilir. |

Bir “GitLab / CI/CD dokümanları” kısa listesi (cicd/README veya DEPLOYMENT_GUIDE sonunda) eklenirse, “hangi durumda hangi dosyaya bakayım” netleşir.

---

## 5. “current_status” / “CURRENT_STATUS” dosyaları

Alan bazlı durum özeti; çakışma yok, sadece konum ve naming tutarlılığı.

| Dosya | Alan |
|-------|------|
| **cicd/current_status.md** | CI/CD pipeline, runner, konfig |
| **deployment/current_status.md** | Deployment süreci, sunucu, pipeline |
| **infrastructure/current_status.md** | Altyapı, nginx, port, vb. |
| **MngHub/support/guides/CURRENT_STATUS.md** | MngHub |
| **Mng.Ui/support/guides/current_status.md** | Mng.Ui |
| **MngDomainUI/guides/current_status.md** | MngDomainUI |
| **MngDataGateway/support/guides/CURRENT_STATUS.md** | MngDataGateway |

**Öneri:** Hepsi “o alanın anlık durumu” için mantıklı. İsterseniz tümünde küçük/orta başlık şu kalıba çekilebilir: “\[Alan\] mevcut durum özeti” ve üstte “Güncelleme: …” tarihi yazılır.

---

## 6. GATEWAY_INTEGRATION dokümanları

Her servisin kendi **GATEWAY_INTEGRATION.md** dosyası var (MngKeeper, MngHub, MngDataGateway, Mng.Ui, MngAdmin, MngEngine, MngNotifier, MngScheduler, MngLLM, MngReactor). Bu, servis bazlı tasarım; çoğaltma değil.

- **MngKeeper:** Ek olarak `GATEWAY_INTEGRATION_CHANGES.md` (“yapılan değişiklikler”) ve `GATEWAY_TROUBLESHOOTING.md` var; bunlar Keeper’a özel, bırakılabilir.
- **MngGateway:** `GATEWAY_INTEGRATION_COMPLETE.md` – gateway servisi perspektifinden “entegrasyon tamamlandı” özeti; diğerleriyle çakışmıyor.

Bu grupta birleştirme önerilmez; sadece “Gateway genel bakış” isterseniz api/overview veya teknik spec’lerde kısa bir “Gateway entegrasyonu” bölümü eklenebilir.

---

## 7. Özet — Hemen yapılabilecekler

1. **content/ROADMAP.md:**  
   Nav “Product Roadmap” ise içerik ürün roadmap’i olmalı. Şu an MngNotifier içeriği var; ya bu dosya MngNotifier içeriğinden arındırılıp gerçek ürün roadmap’i yazılır ya da nav “MngNotifier Roadmap” yapılıp sayfa `MngNotifier/main/ROADMAP.md`’e yönlendirilir.

2. **Deployment / CI/CD:**  
   - cicd/ içinde kısa bir “Deployment ve CI/CD rehberleri” listesi (hangi dosya ne için) eklenmesi.  
   - **DOCKER_DEPLOYMENT.md** başlıkta “MkDocs” vurgulansın ki uygulama deployment rehberleriyle karışmasın.

3. **Roadmap dokümanları:**  
   - content/ROADMAP.md düzeltildikten sonra, devops-roadmap / DEPLOYMENT_ROADMAP / HOSTING_CI_CD_DEPLOYMENT_ROADMAP’in “hangi roadmap nerede” olduğu tek bir yerde (ör. devops-roadmap veya index) 1–2 cümleyle yazılabilir.

Bu rapor, birleştirme / silme / taşıma kararlarını tek tek alırken referans olarak kullanılabilir; her madde ileride “yapıldı” şeklinde işaretlenebilir.
