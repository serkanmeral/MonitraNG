# DLP — Güncel durum

**Son güncelleme:** 2 Eylül 2026  
**Kapsam:** Origin sınıflandırma DLP (Odak)  
**Durum:** Dilim 0 + Dilim 1 motor sahada; Outlook eklentisi kuruldu ama **Office lisansı yüzünden yükleme doğrulanamadı** — IT aktivasyonu bekleniyor.  
**Detay:** [DEVAM.md](./DEVAM.md) · [DLP_PLANNING.md](./DLP_PLANNING.md) · [POLICY.md](./POLICY.md) · [LAB.md](./LAB.md)

---

## Son çalışılan konu

Dilim 1 son adımı: Classic Outlook COM eklentisi (`MngLogs DLP`) + Click-to-Run kaydı. Outlook, grace süresi dolmuş Office yüzünden **Sign in to set up Office** / hesap sihirbazında takıldı. Grace `ospp.vbs /rearm` ile ~5 gün yenilendi; **kalıcı aktivasyon IT’de**. `noreply@odakkompozit.com.tr` ile Office **aktive edilmeyecek**.

---

## Tamamlanan işler

### Dilim 0

- Damga + `classificationTagId` (MngDocument); Odak DG şema PATCH (`dm_tags.kind/sensitivity/persistToFile`)
- Test DG: `192.168.20.20:5040`

### Dilim 1 motor

- Collector: `GET/PUT /api/v1/policy/dlp`, `POST .../publish`, seed `enforcementMode: auditOnly`
- Odak test: `mngdocument` + `mnglogcollector` (`192.168.20.20:5091`) — `GET :5091/api/v1/policy/dlp` yeşil
- Windows agent **1.0.11** yerinde yükseltme (`C:\Program Files\MngLogs\Agent`), config korundu:
  - `collectorBaseUrl` `http://192.168.20.20:5091`
  - `hostId` `TERMINAL-pilot`
  - local UI `0.0.0.0:5092`
- `POST /dlp/evaluate` localhost + `X-MngLogs-DlpKey` (`%ProgramData%\MngLogs\Agent\dlp-local.key`)
- Lab: `scripts/tests/MngLogs/dlp/test-dlp-evaluate.ps1` yeşil  
  (`cl-gizli` + `dis@gmail.com` → `allowSend: true`, `wouldBlock: true`)

### Outlook eklentisi (kod + kurulum; yükleme doğrulanmadı)

- Proje: `MngLogs/Presentation/MngLogs.OutlookAddin/` (net48 x64 COM, `IDTExtensibility2`, `ItemSend`)
- Dilim 1: fail-open; `allowSend=false` olmadıkça iptal yok; `wouldBlock` → MessageBox
- Kurulum: `MngLogs/scripts/install-outlook-addin.ps1` (UAC: Click-to-Run sanal HKLM + `Program Files`)
- CLSID `{E7B2C4A1-9F18-4D6E-8A3B-1C5E9D0F2B44}`, ProgId `MngLogs.OutlookAddin`
- Kurulu kopya: `C:\Program Files\MngLogs\OutlookAddin\`
- Log (yüklenirse): `%LOCALAPPDATA%\MngLogs\OutlookAddin\addin.log`
- Birim test: `DlpSendGateTests` (7)

### Bu PC Outlook hesabı

- Profil: **Outlook** (varsayılan); hesap `noreply@odakkompozit.com.tr` **POP/SMTP**
- IMAP 993 ve SMTP **587 STARTTLS** bu PC’den AUTH yeşil; Outlook “test gönder” **465** ile kesildi
- Lab tercihi kullanıcı kararı: notifier kutusu (smtp4dev rehberi [LAB.md](./LAB.md) içinde duruyor)

---

## Devam eden / park

- **Office M365 Apps aktivasyonu (IT)** — eklenti doğrulaması ve ItemSend lab’ı buna bağlı
- Outlook add-in hâlâ UI’da **Inactive** göründü; COM Add-ins onay kutusu Click-to-Run’da yetmiyor
- C2R + Program Files kaydı yazıldı; Event 45 / `addin.log` ile **Active** teyidi yapılmadı
- SIEM DLP kural paneli yok
- Keeper kimlik: `unresolved` (grup cache sonraki)
- DI sınıflandırma kataloğu boş
- Dilim 2 `enforce` yok
- Prod collector `192.168.20.8` bu turda yok

---

## Sonraki adımlar (IT sonrası)

1. Outlook’u aç; **Sign in to set up Office** gelirse atla — `noreply` ile Office’e girme
2. **Email Account Setup** gelirse İptal (hesap zaten profilde)
3. Dosya → Seçenekler → Eklentiler: **MngLogs DLP** = Active  
   - Event ID 45 içinde `MngLogs.OutlookAddin`  
   - `addin.log`: `assembly loaded` / `connected`
4. Dilim 1 deneme maili (auditOnly; gönderim kesilmez)
5. Gönderim takılırsa SMTP **587 + STARTTLS**; POP→IMAP isteğe bağlı
6. SIEM kural UI; Keeper grup; Dilim 2 enforce

---

## Önemli notlar

### Click-to-Run COM

Outlook eklentileri `HKCU\...\Outlook\Addins` listesinden **boot etmez**. Çalışan eklentiler:

`HKLM\SOFTWARE\Microsoft\Office\ClickToRun\REGISTRY\MACHINE\SOFTWARE\Microsoft\Office\Outlook\Addins`

DLL **Program Files** altında olmalı (AppData .NET COM çoğu C2R’da yüklenmez). Location sütununda `mscoree.dll` .NET COM için normaldir.

`LoadBehavior=2` = bu oturumda boşaltılmış (Inactive). Hedef `3`.

İlk .NET COM yükü Outlook resiliency eşiğini (~1s) aşabilir → Slow and Disabled: 30 gün izleme.

### Lisans

- SKU: `Office16O365BusinessR_Grace` — süre dolmuştu (`0xC004F009`); 2 Eyl 2026 **rearm** → ~5 gün `OOB_GRACE`
- Rearm aktivasyon değildir; IT iş hesabıyla M365 Apps basacak
- `noreply@…` posta kutusu, Apps lisansı değil

### Park — backend .NET 10 (bu kapsamda değil)

DLP fazlarında değerlendirilmez.

---

## Nerede kalmıştık

Motor ve eklenti **kod + test DG + agent 1.0.11 + C2R kurulum** tamam. Durma nedeni: **Office aktivasyonu**. IT bitince madde 1–4 (yukarıda) ile eklentinin gerçekten yüklendiğini doğrula; sonra Dilim 1 gönderim lab’ı.
