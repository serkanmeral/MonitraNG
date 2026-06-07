# Odak — Bildirim ve Alarm Kontrol Listesi

**Son guncelleme:** 7 Haziran 2026  
**Amac:** MO in-app toaster + Alarm bildirim politikaları gelistirmeleri tamamlandiktan sonra tek oturumda manuel dogrulama.

**Test kullanicisi:** `odak_admin` · personId: `6a0f8fd13d6ba5d774ee37c7` · e-posta: `serkan.meral@outlook.com`

**On kosullar:**
- Odak deploy: `mnghub`, `mngoperations`, `mngui`, `mngalarm`, `mngalarm-worker`
- Token: `docs/odak/operationcore/scripts/get-operationcore-token.ps1`
- Notifier seed (e-posta icin): `docs/odak/notifications/scripts/setup-notifier-datasets.ps1`
- Alarm menu (istege bagli): `docs/odak/alarm/scripts/patch-alarm-center-side-menu.ps1`

---

## A. MO In-App Toaster (T1-T5)

| # | Adim | Beklenen | Script / not |
|---|------|----------|----------------|
| A1 | `odak_admin` ile giris, Ctrl+F5 | Hub baglantisi kurulur | |
| A2 | `smoke-inapp-toast.ps1` calistir | Hub 202, WI olusturulur | `docs/odak/notifications/scripts/smoke-inapp-toast.ps1` |
| A3 | Sag ust toaster | Baslik + mesaj gorunur, kapanir | |
| A4 | Zil ikonu badge | Okunmamis sayi artar | |
| A5 | OC Demo workspace: WI olustur / gecis | Policy'ye gore toaster + inbox | `audit-oc-demo-workspace.ps1` |
| A6 | Policy UI: `pushToast: false` | Inbox yazilir, toaster cikmaz | `/apps/operation-core/...` mail policies |
| A7 | Iki kullanici izolasyonu | B kullanicisi A'nin toaster'ini gormez | Ikinci test hesabi gerekir |
| A8 | Hub kapali senaryo (opsiyonel) | Poll ile inbox guncellenir | mnghub durdur + beklenen davranis |

---

## B. Alarm Bildirim Politikalari UI (AN-3)

| # | Adim | Beklenen |
|---|------|----------|
| B1 | `/apps/alarm-center/notification-policies` ac | Manager erisimi, liste yuklenir |
| B2 | Sekme: Bildirim politikaları | Nav'da 3. sekme gorunur |
| B3 | Yeni politika | Ad, olay, kanal, alici zorunlu validasyon |
| B4 | Keeper kullanici aramasi | Coklu alici secimi (chips) |
| B5 | E-posta kanali | `alarm-raised` / `alarm-resolved` combobox |
| B6 | Uygulama ici + toaster | `pushToast` + severity secimi |
| B7 | Kural + onem araligi | Opsiyonel daraltma kaydedilir |
| B8 | Duzenle / sil | CRUD API ile senkron |

---

## C. Alarm Dispatch E2E (AN-2 + AN-4 + AN-5)

| # | Adim | Beklenen | Script / not |
|---|------|----------|----------------|
| C1 | Mail sablonlari seed | `alarm-raised`, `alarm-resolved` DG'de | `setup-notifier-datasets.ps1` |
| C2 | Aktif politika olustur | `AlarmRaised`, inApp+email, odak_admin alici | UI veya smoke script |
| C3 | `smoke-alarm-notification-policy.ps1 -KeepPolicy` | API CRUD OK, observation tetiklenir | `docs/odak/alarm/scripts/smoke-alarm-notification-policy.ps1` |
| C4 | Observation: cpu_usage > esik | Alarm raise | lifecycle script de kullanilabilir |
| C5 | Toaster | Uyari/hata severity policy'ye uygun | |
| C6 | Inbox (`op_notifications`) | Alarm basligi + deep link | |
| C7 | E-posta | `alarm-raised` subject + govde | Notifier log / gelen kutu |
| C8 | Alarm resolve | `AlarmResolved` policy varsa cozulme bildirimi | `test-alarm-lifecycle-e2e.ps1` |
| C9 | Cooldown | Ayni alarm icin tekrar gonderim engellenir | policy cooldown > 0 |

---

## D. Regresyon (kisa)

| # | Alan | Kontrol |
|---|------|---------|
| D1 | Alarm Merkezi alarmlar/kurallar | Mevcut CRUD ve lifecycle bozulmamis |
| D2 | MO mail policies | OC workspace bildirimleri calisiyor |
| D3 | Workflow alarm trigger | `alarm.raised` instance aciliyor (mevcut E2E) |

---

## E. Bilinen sinirlar (beklenen)

- Gateway `/hub/...` 404: MO/MngAlarm dogrudan `mnghub:5020` kullanir
- `excludeAcknowledgedBy`: alan var, tam davranis ileri faz
- RMQ diagnostics: ayri oturum (`PLATFORM_HANDOFF.md` RMQ-DIAG)

---

## Hizli komut ozeti

```powershell
# Token
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1

# MO toaster smoke
.\docs\odak\notifications\scripts\smoke-inapp-toast.ps1

# Notifier seed (alarm mail sablonlari dahil)
.\docs\odak\notifications\scripts\setup-notifier-datasets.ps1

# Alarm policy API + dispatch smoke
.\docs\odak\alarm\scripts\smoke-alarm-notification-policy.ps1 -KeepPolicy

# Alarm lifecycle (resolve dahil)
.\scripts\odak\test-alarm-lifecycle-e2e.ps1

# UI deploy (gerekirse)
.\scripts\odak\_run-toast-deploy.ps1
```
