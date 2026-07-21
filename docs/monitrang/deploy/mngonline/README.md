# monitrang.com — Online deploy

**Sunucu:** `monitrang-server` (`45.141.151.52`)  
**Strateji:** Lokal repo → sync (tar/scp) → sunucuda `docker compose` build/up — **GitLab Runner deploy birincil yol değil**

| Doküman | İçerik |
|---------|--------|
| [ACCESS.md](./ACCESS.md) | SSH, URL’ler, dizinler |
| [DEPLOY_STRATEGY.md](./DEPLOY_STRATEGY.md) | Neden bu model, eski CI hattı ile ilişki |
| [DEPLOY.md](./DEPLOY.md) | Günlük komutlar ve checklist |
| [RUNNER_RESOURCES.md](./RUNNER_RESOURCES.md) | GitLab Runner idle/aktif kaynak; stop/kaldır seçenekleri |
| [USERS_AND_DATA.md](./USERS_AND_DATA.md) | Odak user/group anonimizasyon + online veri aktarımı |
| [landing/](./landing/) | www landing statik site (ayrı deploy) |

## Hızlı akış

```powershell
# Repo kökünden, pwsh veya PowerShell
.\scripts\mngonline\sync-mngonline-source.ps1 -Paths Mng.Ui
.\scripts\mngonline\deploy-mngonline-apps.ps1 -Services mngui
```

Ön koşul: `ssh root@monitrang-server` çalışıyor olmalı ([ACCESS.md](./ACCESS.md)).
