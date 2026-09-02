# DLP — Geliştirme lab’ı (bu PC)

**Durum:** Kurulum rehberi (31 Ağustos 2026)  
**Amaç:** Sınıflı ek + iç/dış alıcıyı internete çıkarmadan denemek  
**Plan:** [DLP_PLANNING.md](./DLP_PLANNING.md) §11 · Sözleşme: [POLICY.md](./POLICY.md) §4

Bu makine: **Classic Outlook** (Office 16 / Microsoft 365 Apps) yüklü. Yeni Outlook yok — COM eklentisi için uygun.

---

## 0. Neyi kullanmayın

- Asıl M365 / Outlook.com hesabından sınıflı ek göndermek
- Gmail SMTP, Odak `mail.kurumsaleposta.com`, Mailu `mail.monitrang.com` (bunlar **bildirim** kuyusu)

Lab postası yalnızca `127.0.0.1` SMTP kuyusuna gider.

---

## 1. Katman B — smtp4dev

Catch-all SMTP; web UI’da biriken postalar. Dışarı gitmez.

Bu PC’de **.NET 9** var. NuGet’deki son smtp4dev (3.15+) **.NET 10** ister; `dotnet tool install -g` sürümsüz **başarısız** olur. Pin:

```powershell
# Repo kökünde global.json SDK’yı 9’a kilitleyebilir; TEMP’ten kurun
Set-Location $env:TEMP
dotnet tool install -g Rnwood.Smtp4dev --version 3.8.1
```

**31 Ağu 2026:** `smtp4dev` 3.8.1 bu makineye kuruldu.

Çalıştırma:

```powershell
smtp4dev --urls http://127.0.0.1:5088 --smtpport 2525 --imapport 3143 --tlsmode None
```

| Ne | Adres |
|----|--------|
| SMTP | `127.0.0.1:2525` (auth yok, TLS yok) |
| IMAP | `127.0.0.1:3143` (Outlook hesabı için) |
| Web UI | http://127.0.0.1:5088 |

`--urls` 5088: agent local UI 5092 ve smtp4dev varsayılan 5000 çakışmasın diye sabitlenir.

Durdurmak: o pencerede Ctrl+C. Kalıcı servis gerekmez; DLP denemesinde açılır.

Kurulum kontrolü:

```powershell
dotnet tool list -g | Select-String smtp4dev
```

---

## 2. Katman C — Outlook lab hesabı

**From** olarak M365 hesabı kullanmayın (posta Microsoft bulutuna gider).

Classic Outlook:

1. Dosya → Hesap ayarları → Hesap ayarları → Yeni  
2. Elle kurulum / **IMAP**  
3. Ad: `DLP Lab`  
4. E-posta: `tester@dlp.internal`  
5. Gelen (IMAP): `127.0.0.1`, port **3143**, şifreleme **yok**  
6. Giden (SMTP): `127.0.0.1`, port **2525**, şifreleme **yok**, kimlik doğrulama **yok**  
7. Kullanıcı adı: `tester@dlp.internal` — Outlook zorunlu kılarsa parola: `dlp-lab` (smtp4dev yok sayar)

Gönderirken hesaptan **DLP Lab / tester@dlp.internal** seçili olsun.

Outlook “sertifika / güvenli bağlantı” uyarısı verirse lab için şifrelemeyi kapalı bırakın.

---

## 3. İç / dış alıcı (sahte)

Politika sözlüğü (örnek):

```json
"internalEmailDomains": ["dlp.internal", "odak.local"]
```

| Kime yazın | Motor | Gerçek teslimat |
|------------|--------|-----------------|
| `ali@dlp.internal` | iç | smtp4dev |
| `dis@gmail.com` | dış | yine smtp4dev (Google yok) |

smtp4dev her alıcıyı yutar. DLP MX’e bakmaz, adres domain’ine bakar.

---

## 4. Test sırası

Motor (`POST /dlp/evaluate`) eklentiden **önce** yeşil olmalı — [POLICY.md](./POLICY.md) §4.4.

| # | Senaryo | Beklenen (Dilim 1 `auditOnly`) |
|---|---------|--------------------------------|
| 1 | `classificationId=cl-gizli` + `ali@dlp.internal` | `emailScope: internal`, `allowSend: true`, `wouldBlock: false` (iç kural audit) |
| 2 | `cl-gizli` + `dis@gmail.com` | `external`, `allowSend: true`, `wouldBlock: true` (dış kural block ama kesilmez) |
| 3 | ek yok / damgasız | `unclassified`, allow + audit |
| 4 | (Eklenti sonrası) Outlook’tan damgalı docx + dış adres | smtp4dev’de mail **var** (auditOnly); SIEM’de `wouldBlock: true` |
| 5 | Dilim 2 `enforce` + aynı senaryo | smtp4dev **boş**; eklenti iptal |

---

## 5. Katman A — Outlook’suz (PowerShell taslağı)

Agent ayağa kalkıp `dlp-local.key` ürettikten sonra (implementasyon sonrası):

```powershell
$key = Get-Content "$env:ProgramData\MngLogs\Agent\dlp-local.key" -Raw
$body = @{
  action = "email.send"
  windowsUser = "$env:USERDOMAIN\$env:USERNAME"
  recipients = @("dis@gmail.com")
  attachments = @(@{ classificationId = "cl-gizli" })
  client = @{ kind = "simulate"; version = "lab" }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Uri "http://127.0.0.1:5092/dlp/evaluate" -Method POST `
  -Headers @{ "X-MngLogs-DlpKey" = $key.Trim() } `
  -ContentType "application/json" -Body $body
```

**2 Eyl 2026:** agent 1.0.11 + collector test `192.168.20.20:5091` ile `scripts/tests/MngLogs/dlp/test-dlp-evaluate.ps1` yeşil.

---

## 6. Kurulum durumu (bu PC)

| Adım | Durum |
|------|--------|
| Classic Outlook | Var (Office 16 Click-to-Run, Microsoft 365 Apps) |
| smtp4dev 3.8.1 global tool | Kurulu (izole lab; bu turda kullanılmadı) |
| `/dlp/evaluate` | Yeşil (localhost 5092, Dilim 1 auditOnly) |
| Outlook COM add-in | Kurulu (`Program Files` + C2R HKLM); **Active teyidi yok** — Office IT |
| Office lisansı | Grace rearm ~5 gün (2 Eyl 2026); kalıcı aktivasyon IT |

### 6.1 Bu turda kullanılan Outlook hesabı (notifier)

Kullanıcı smtp4dev yerine Odak notifier kutusunu istedi.

- Profil: **Outlook** (varsayılan)
- Hesap: `noreply@odakkompozit.com.tr` — **POP/SMTP** (IMAP sihirbazı otomasyonda takıldı)
- SMTP 587 STARTTLS AUTH bu PC’den yeşil; Outlook test gönderimi 465 ile kesildi
- **Office’e bu adresle giriş yok** (posta kutusu ≠ M365 Apps lisansı)

İzole lab (smtp4dev + `tester@dlp.internal`) hâlâ tercih edilen yol; §1–3.

### 6.2 Add-in kurulumu (aktivasyon sonrası)

```powershell
pwsh -File .\MngLogs\scripts\install-outlook-addin.ps1 -CloseOutlook
```

UAC gerekir (Click-to-Run sanal HKLM). COM Add-ins → Go kutusu yetmez. Doğrulama: Event 45’te `MngLogs.OutlookAddin`; log `%LOCALAPPDATA%\MngLogs\OutlookAddin\addin.log`.
