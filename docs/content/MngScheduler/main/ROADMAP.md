# MngScheduler Roadmap

Yaptıklarımız, yapacaklarımız ve kararlarımız bu dosyada güncellenecektir.

## Yapılanlar

- **Proje yapısı** — Clean Architecture; Domain (ScheduledJob, JobExecution, JobType), Application, Infrastructure, Persistence, Presentation.
- **System Job** — mng_keeper.@scheduled_jobs; SystemJobRepository; Admin only CRUD; cron, endpoint URL, HTTP GET/POST, retry, timeout.
- **User Job** — Domain @scheduled_jobs (MngDataGateway dataset); UserJobRepository; domain izolasyonu; MngDataGatewayClient.
- **HttpJob (Quartz)** — Generic HTTP GET/POST, JobDataMap, execution kaydı, RabbitMQ event (job.execution.completed).
- **JobSyncService** — BackgroundService; periyodik + immediate sync; System + User job birleştirme; Quartz ile senkron.
- **Job Execution** — JobExecutionRepository (mng_keeper.@job_executions, domain @job_executions); TTL/retention; response truncation.
- **DomainLookupService** — Aktif domain listesi, cache; User job’lar için domain bazlı okuma.
- **Gateway, Health, Version** — API Gateway entegrasyonu, health/version endpoint’leri.

Detaylı sürüm geçmişi için [Changelog](CHANGELOG.md) dosyasına bakınız.

## Yapılacaklar

- **User Job E2E testi** — Domain dataset üzerinden oluşturma/çalıştırma/silme akışının doğrulanması.
- **Retry ve monitoring** — Job hatası sonrası retry politikası, execution dashboard/raporlama.
- **Job UI** — System/User job yönetimi için arayüz (planlanan).

## Kararlar

- **Sorumluluk** — MngScheduler işi yapmaz; sadece zamanında ilgili endpoint’e HTTP çağrısı yapar.
- **System Job** — mng_keeper veritabanında @scheduled_jobs; sadece Admin erişir.
- **User Job** — Her domain’in kendi @scheduled_jobs dataset’inde; MngDataGateway API ile CRUD.

---

Detaylı geliştirme roadmap’i ve fazlar için [Roadmap (ek)](../support/guides/ROADMAP_LEGACY.md) dosyasına bakılabilir.
