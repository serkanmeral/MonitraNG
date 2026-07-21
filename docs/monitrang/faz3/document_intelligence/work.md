# Document Intelligence — Work

**Son güncelleme:** 13 Temmuz 2026  
**Durum:** T-0…T-2 yeşil · **DI-1 / D-N1** mail omurgası · **TG-4** Telegram kanalı (default kapalı / local `.env` ile açılabilir)

---

## Nerede kaldık

Yetki test omurgası T-2’ye kadar tamam. D-N1: üretim sonrası `document.generated` → MngNotifier mail (best-effort). Açmak için config + deploy gerekir.

## Bu oturumda yapılanlar

- [x] T-0 / T-1  
- [x] **T-2** — `test-inheritance.ps1` (12/12 PASS); runner’a bağlandı  
- [x] **DI-1 / D-N1** — `IDocumentNotificationOrchestrator` + generation hook + appsettings/compose
- [x] **TG-4** — `Channels` + Telegram send + Keeper `telegram-resolve-recipients` (DefaultTelegramChatIds / TelegramUserIds)
- [ ] D-N1: Enabled=true + DefaultTo smoke (deploy sonrası; local DI_NOTIFICATIONS_ENABLED)
- [ ] D-N: in-app / klasör abonelik (sonraki dilimler)

## Sıradaki

1. D-N1 doğrulama: `Notifications__Enabled=true` + alıcı mail; üretim smoke  
2. D-N genişletme (sürüm/upload olayları, Telegram kanal — mesajlaşma fazı)  
3. DI-2 cilalar / D-S / AI (sıraya göre)

## Blocker

- D-N1 canlı test için `mngdocument` rebuild/deploy + SMTP/Notifier ayarı  
- Telegram: ortak kanal dokümanı → [TELEGRAM.md](../../../odak/notifications/TELEGRAM.md) (karar kilidi §6)

## Commit / deploy

| Tarih | Commit | Not |
|:---|:---|:---|
| — | — | İstek üzerine |

## Test çalıştırma

```powershell
cd scripts/tests/MngDocument
pwsh .\runner.ps1 -Gateway http://localhost:5040          # T-0+T-1+T-2
pwsh .\runner.ps1 -SkipT1                                  # yalnızca T-2 (+fixture)
```
