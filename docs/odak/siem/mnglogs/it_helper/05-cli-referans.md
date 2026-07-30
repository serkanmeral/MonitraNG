# 05 — CLI referansı

Agent exe hem Windows Service hem CLI olarak çalışır. CLI modunda **web host / Local UI başlamaz**.

## Exe yolu

```text
C:\Program Files\MngLogs\Agent\MngLogs.Agent.exe
```

Geliştirme / publish:

```text
MngLogs\artifacts\agent\win-x64\MngLogs.Agent.exe
```

## Ortak parametre

| Parametre | Açıklama |
|-----------|----------|
| `--data-dir <path>` veya `-d <path>` | Config / PIN kökü. Yoksa `%ProgramData%\MngLogs\Agent` (veya ayardaki DataDirectory) |

Örnek:

```powershell
$exe = "C:\Program Files\MngLogs\Agent\MngLogs.Agent.exe"
& $exe status
& $exe status --data-dir "$env:ProgramData\MngLogs\Agent"
```

## Komutlar

### `status`

Özet: data dir, host id, UI URL, port durumu, PIN configured/unlocked.

```powershell
& $exe status
```

### `config show`

`system.json` özeti (API key maskeli).

```powershell
& $exe config show
```

### `config set` (MSI / GPO / sessiz)

Sadece verilen alanlar güncellenir. Etkileşim istemez.

```powershell
& $exe config set `
  --collector http://siem-collector:5091 `
  --api-key "secret" `
  --host-id "SUBE-01-PC" `
  --ui-host 127.0.0.1 `
  --ui-port 5092 `
  --data-dir "$env:ProgramData\MngLogs\Agent"

Restart-Service MngLogsAgent
```

| Bayrak | Anlam |
|--------|--------|
| `--collector` | CollectorBaseUrl |
| `--api-key` | ApiKey (bayrak varsa boş string de yazılabilir) |
| `--host-id` | HostId |
| `--ui-host` | LocalUiHost |
| `--ui-port` | LocalUiPort (1–65535) |

**Önemli:** Çalışan servis eski değeri bellekten kullanmaya devam eder → `Restart-Service MngLogsAgent`.

### `catalog show` / `catalog sync`

Collector paket kataloğu önbelleği.

```powershell
& $exe catalog show
& $exe catalog sync
```

`sync` collector'a gider; başarısızsa builtin / son cache. Çalışan servis bir sonraki poll'da aynı cache dosyasını okur (gerekirse servisi restart edin).

### `port show` / `port check` / `port set`

```powershell
& $exe port show
& $exe port check
& $exe port check 5093
& $exe port set 5093
Restart-Service MngLogsAgent
```

Port doluysa agent açılışta çıkış kodu **3** ile durur ve CLI ipucu yazar. Sessiz rastgele porta geçmez.

### `pin status` / `pin reset` / `pin set`

Local UI Politika yazma PIN’i.

```powershell
& $exe pin status
& $exe pin reset --yes          # PIN’i siler → UI’da yeniden setup
& $exe pin set --pin 1234 --confirm 1234
# veya etkileşimli:
& $exe pin set
```

PIN değişince açık UI oturumları geçersizleşir; agent restart önerilir.

### `help`

```powershell
& $exe help
```

## Çıkış kodları (özet)

| Kod | Anlam |
|-----|--------|
| 0 | Başarılı |
| 1 | Genel hata |
| 2 | Kullanım / argüman hatası |
| 3 | Port dolu / port check başarısız |

## Tipik IT senaryoları

### Yanlış collector URL

```powershell
& $exe config set --collector http://dogru-collector:5091
Restart-Service MngLogsAgent
```

### Port 5092 çakışması

```powershell
& $exe port check
& $exe port set 5093
Restart-Service MngLogsAgent
# Local UI: http://127.0.0.1:5093/
```

### PIN unutuldu

```powershell
& $exe pin reset --yes
# Tarayıcıda Politika → yeni PIN oluştur
```

### Teşhis paketi

```powershell
& $exe status
& $exe config show
& $exe port check
Get-Service MngLogsAgent
Get-Content "$env:ProgramData\MngLogs\Agent\logs\agent-$((Get-Date).ToString('yyyyMMdd')).log" -Tail 80
```

## Local UI ile ilişki

- CLI, PIN ve config’i dosyadan yönetir; UI aynı `system.json` / `ui-auth.json` dosyalarını kullanır.
- UI’dan yazma işlemleri PIN oturumu ister; CLI kurtarma yolu **yönetici konsoludur**.
- Exe “Gözat” (OpenFileDialog) Windows Service oturumunda çalışmaz; path’i elle veya CLI/policy ile verin.
