# MngLogs Agent — IT Helper

Bu klasör, saha bilgisayarlarına **MngLogs Agent** kurulum / dağıtım / kaldırma ve **CLI kurtarma** işlemleri için IT ekiplerine verilen kılavuzlardır.

MkDocs veya web sitesine bağlı değildir; Word/PDF’e aktarılabilir düz Markdown dosyalarıdır.

> **Geliştirme notu (2026-07-30):** MngLogs agent geliştirme oturumu park edildi. Ürün sırası ve teknik durum: `docs/content/MngLogs/current_status.md`.

## Dosyalar

| Dosya | Konu |
|-------|------|
| [01-msi-uretim.md](01-msi-uretim.md) | MSI paketinin üretilmesi (geliştirme / release hazırlığı) |
| [02-msi-kurulum.md](02-msi-kurulum.md) | Manuel ve sessiz kurulum, MSI property’leri |
| [03-msi-dagitim-gpo.md](03-msi-dagitim-gpo.md) | AD Group Policy ile dağıtım, MST önerileri |
| [04-msi-kaldirma-upgrade.md](04-msi-kaldirma-upgrade.md) | Kaldırma, yükseltme, veri kalıcılığı |
| [05-cli-referans.md](05-cli-referans.md) | CLI komutları (config, port, PIN, status) |
| [06-sorun-giderme.md](06-sorun-giderme.md) | Sık karşılaşılan hatalar ve kontroller |
| [07-surum-guncelleme.md](07-surum-guncelleme.md) | Sürüm güncelleme stratejisi (GPO/MSI; self-update yok) |

## Ürün özeti

| Öğe | Değer |
|-----|--------|
| Ürün adı | MngLogs Agent |
| Windows servis adı | `MngLogsAgent` |
| Görünen ad | MngLogs Agent |
| Hesap | LocalSystem |
| Binary klasörü | `C:\Program Files\MngLogs\Agent` |
| Veri / config / log | `%ProgramData%\MngLogs\Agent` |
| Local UI | `http://127.0.0.1:5092/` (varsayılan) |
| MSI (örnek) | `MngLogs\artifacts\msi\MngLogs.Agent-0.2.0.msi` |

## Önemli ilkeler (GPO)

1. Kurulum **makine kapsamlı** (per-machine), kullanıcı profiline yazılmaz.
2. Dağıtım **sessiz** olmalıdır (`msiexec /qn` veya GPO Software Installation).
3. Collector URL / API key genelde **MSI property** veya **MST** ile verilir.
4. Upgrade / uninstall sonrası **ProgramData** (PIN, kuyruk, `system.json`) varsayılan olarak korunur.
5. Local UI yalnızca **loopback** dinler; uzaktan erişim tasarlanmamıştır.
6. **Self-update yok** — yeni sürüm yine MSI/GPO ile gelir ([07-surum-guncelleme.md](07-surum-guncelleme.md)).
