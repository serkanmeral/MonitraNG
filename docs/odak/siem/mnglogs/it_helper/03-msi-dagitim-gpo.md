# 03 — AD / GPO ile dağıtım

MngLogs Agent MSI’si **per-machine**, **sessiz** ve LocalSystem servis olarak tasarlanmıştır. Müşteri IT’sinin tipik yöntemi: **Group Policy → Computer Configuration → Software Installation**.

## Neden Computer Configuration?

| Tercih | Gerekçe |
|--------|---------|
| Computer (makine) policy | Servis LocalSystem; tüm kullanıcılar için tek kurulum |
| User policy | Uygun değil (Program Files + servis) |
| Assigned (zorunlu) | Filo standardı |
| Published | İsteğe bağlı; saha agent için önerilmez |

## Hazırlık

1. MSI’yi DC’nin veya client’ların **okuyabildiği UNC share**’e koyun  
   Örnek: `\\fileserver\Software\MonitraNG\MngLogs.Agent-0.2.0.msi`
2. Share ACL: Domain Computers / Authenticated Users **Read & execute**
3. (Önerilir) Site / OU bazlı **MST** ile collector URL ve API key ayırın

## GPO adımları (özet)

1. Group Policy Management → ilgili OU için GPO oluştur / düzenle  
2. **Computer Configuration → Policies → Software Settings → Software installation**  
3. Sağ tık → New → Package → MSI UNC yolunu seçin  
4. Deployment method: **Assigned**  
5. (İsteğe bağlı) Modifications → `.mst` ekleyin  
6. Advanced:
   - Uninstall this application when it falls out of the scope → ortama göre
   - 32-bit / 64-bit: **x64** paket; 64-bit Windows hedeflenir
7. `gpupdate /force` veya bir sonraki reboot ile uygulanır

> Not: Software Installation çoğu ortamda **reboot** sonrası tamamlanır. Pilot OU’da önce test edin.

## MST / property stratejisi

MSI public property’leri:

- `COLLECTORURL`
- `APIKEY`
- `HOSTID`
- `LOCALUIHOST`
- `LOCALUIPORT`

### Seçenek A — MST (önerilen filo modeli)

Her site / müşteri için bir transform:

| MST | Örnek |
|-----|--------|
| `odak-merkez.mst` | `COLLECTORURL=http://192.168.20.8:5091` |
| `sube-ankara.mst` | Farklı collector veya key |

Oluşturma araçları: Orca (Windows SDK), InstEd, Advanced Installer vb.  
Property table’a yukarıdaki adlarla değer yazılır.

GPO’da paket → Modifications → ilgili MST.

### Seçenek B — Tek MSI, kurulum sonrası CLI

GPO sadece MSI kurar; config’i ayrı script / LAPS sonrası:

```powershell
& "C:\Program Files\MngLogs\Agent\MngLogs.Agent.exe" config set `
  --collector http://siem-collector:5091 `
  --api-key "..." `
  --data-dir "$env:ProgramData\MngLogs\Agent"

Restart-Service MngLogsAgent
```

### Seçenek C — msiexec startup script

Computer Startup script:

```powershell
msiexec /i "\\fileserver\Software\MngLogs.Agent-0.2.0.msi" /qn `
  COLLECTORURL=http://siem-collector:5091 APIKEY=***
```

Software Installation yerine kullanılabilir; loglamayı script içinde yapın.

## Güvenlik notları

- `APIKEY` MSI loglarında görünebilir (`/L*v`). Prod loglarını kısıtlı paylaşın; mümkünse MST + sınırlı log.
- Local UI yalnızca `127.0.0.1` — firewall’da inbound açmayın.
- Security Event Log paketleri için LocalSystem genelde yeterlidir; özel kilitli hesap kullanıyorsanız Event Log okuma haklarını doğrulayın.
- Binary imzalama (Authenticode) kurum politikasına göre ayrıca planlanır.

## Pilot kontrol listesi

- [ ] 1–2 test makinesi OU  
- [ ] Kurulum sonrası `Get-Service MngLogsAgent` = Running  
- [ ] `http://127.0.0.1:5092/health`  
- [ ] Collector’da host görünür / event gelir  
- [ ] Upgrade MSI (yeni sürüm) ProgramData’yı bozmuyor  
- [ ] Scope dışına çıkınca uninstall davranışı kabul edilebilir  

> **Lab notu (2026-07-30):** Geliştirme oturumunda admin yetkisi olmadığı için MSI / Windows Service smoke ertelendi (`msiexec` 1625). Elevated IT makinesinde tekrarlanacak.

## İlgili belgeler

- Kurulum property’leri: [02-msi-kurulum.md](02-msi-kurulum.md)  
- Kaldırma / upgrade: [04-msi-kaldirma-upgrade.md](04-msi-kaldirma-upgrade.md)
