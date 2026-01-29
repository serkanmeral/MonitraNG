# CI/CD ve Deployment — Rehber İndeksi

**Amaç:** Hangi dokümana nerede bakacağınızı hızlıca bulmak.

---

## Ne yapmak istiyorsunuz?

| İhtiyacınız | Önerilen doküman | Açıklama |
|-------------|------------------|----------|
| **İlk kez deploy ediyorum** | [DEPLOYMENT_GUIDE](DEPLOYMENT_GUIDE.md) | Variables, job yapılandırması, süreç, sorun giderme; “İlk Deploy Denemesi ve Checklist” bölümü de burada. |
| **Pipeline / servis listesi referansı** | [DEPLOYMENT_REFERENCE](DEPLOYMENT_REFERENCE.md) | Stage’ler, build job’ları, docker-compose konumu, yeni servis checklist’i. |
| **Kurulumdan troubleshooting’e tek rehber** | [CICD_DEPLOYMENT_COMPLETE_GUIDE](CICD_DEPLOYMENT_COMPLETE_GUIDE.md) | Kapsamlı rehber, geri dönüş noktaları, troubleshooting. |
| **Sıfırdan sunucu + GitLab + Runner** | [HOSTING_CI_CD_DEPLOYMENT_ROADMAP](HOSTING_CI_CD_DEPLOYMENT_ROADMAP.md) | 7 fazlı yol haritası (sunucu, GitLab, Runner, SSL). |
| **Otomatik deployment akışı / script’ler** | [AUTOMATED_DEPLOYMENT_WORKFLOW](AUTOMATED_DEPLOYMENT_WORKFLOW.md) | Workflow, stratejiler, script örnekleri. |
| **MkDocs (dokümantasyon) Docker deploy** | [DOCKER_DEPLOYMENT](DOCKER_DEPLOYMENT.md) | Sadece **dokümantasyon sitesi** için Docker build/deploy. |
| **GitLab ilk kurulum, proje, push** | [GITLAB_SETUP_GUIDE](GITLAB_SETUP_GUIDE.md) | Proje oluşturma, push, Runner kaydı. |
| **Pipeline yapısı (stage, job)** | [GITLAB_CI_CD_GUIDE](GITLAB_CI_CD_GUIDE.md) | Build/Test/Deploy stage’leri, yapılandırma. |
| **Runner token bulma** | [GITLAB_RUNNER_TOKEN_GUIDE](GITLAB_RUNNER_TOKEN_GUIDE.md) | Token alma yöntemleri. |
| **GitLab’ı başka sunucuya taşıma** | [GITLAB_MIGRATION_GUIDE](GITLAB_MIGRATION_GUIDE.md) | Yedekleme, restore, taşıma. |
| **Runner / pipeline yapılandırma, backup, sorun giderme** | [RUNNER_AND_PIPELINE_NOTES](RUNNER_AND_PIPELINE_NOTES.md) | Birleştirilmiş notlar; bilinen sorunlar ve çözümler. |
| **Görev listesi / öncelik analizi** | [ROADMAP_ANALYSIS](ROADMAP_ANALYSIS.md) | Tamamlanan ve bekleyen görevler, öncelik sırası. |
| **CI/CD mevcut durum özeti** | [current_status](current_status.md) | Pipeline, runner, konfigürasyon durumu. |

---

## Diğer ilgili dokümanlar

- **Backup:** [BACKUP_CICD_CONFIG_GUIDE](BACKUP_CICD_CONFIG_GUIDE.md), [FIRST_BACKUP_INSTRUCTIONS](FIRST_BACKUP_INSTRUCTIONS.md)
- **Runner ve pipeline notları (birleştirilmiş):** [RUNNER_AND_PIPELINE_NOTES](RUNNER_AND_PIPELINE_NOTES.md) — yapılandırma, backup/restore, bilinen sorunlar. Ayrıca GITLAB_RUNNER_SETUP, GITLAB_RUNNER_DIAGNOSTIC_STEPS, GITLAB_PIPELINE_TROUBLESHOOTING.
- **Deployment klasörü:** Sunucu, timeline, planlama için [deployment/DEPLOYMENT_ROADMAP](../deployment/DEPLOYMENT_ROADMAP.md), [deployment/current_status](../deployment/current_status.md).

---

**Son güncelleme:** Ocak 2026
