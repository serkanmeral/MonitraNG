# 07 — Sürüm güncelleme stratejisi

## Karar (güncel)

MngLogs Agent **kendi kendini güncellemez**.

Yeni sürümler **merkezi paket dağıtımı** ile gelir:

1. Yeni MSI üretilir  
2. Share / GPO ile dağıtılır  
3. WiX **MajorUpgrade** eski binary’yi değiştirir  
4. `%ProgramData%\MngLogs\Agent` (config, PIN, kuyruk) korunur  

Bu model, müşteri IT’sinin **AD Group Policy / Software Installation** ile denetimli filo yönetimine uyumludur.

## Neden self-update yok?

| Konu | Gerekçe |
|------|---------|
| Denetim | Sürümü IT belirler; agent serbestçe binary indirmez |
| Güvenlik | İmza, kaynak URL, rollback IT süreçlerinde kalır |
| GPO uyumu | Assigned package + upgrade zaten kurumsal standart |
| Basitlik | Sidecar updater / in-place swap karmaşıklığı yok |

## Operasyonel akış

```text
Release MSI (yeni AgentVersion)
        ↓
UNC share / GPO paket güncelleme (+ isteğe bağlı MST)
        ↓
msiexec veya GPO Software Installation (Assigned)
        ↓
MajorUpgrade → yeni Program Files, servis devam
        ↓
ProgramData aynı → config/PIN/kuyruk korunur
        ↓
Gerekirse Restart-Service MngLogsAgent
```

Ayrıntılı komutlar: [04-msi-kaldirma-upgrade.md](04-msi-kaldirma-upgrade.md)

## Bilinçli olarak yapılmayanlar

- Collector’dan otomatik sürüm indirme  
- Agent içi “güncelle” düğmesi  
- Sessiz arka plan self-update  

## İleride (düşük öncelik, isteğe bağlı)

Self-update **şu an planlanmıyor**. İhtiyaç olursa önce şunlar düşünülebilir (uygulama kararı ayrı alınır):

| Seçenek | Açıklama |
|---------|----------|
| **B — Desired version uyarısı** | Collector/policy “hedef sürüm” söyler; agent sadece “eskiyim” loglar / UI uyarır; güncellemeyi yine GPO yapar |
| **C — Self-update** | Agent paketi indirip kurar — güvenlik ve operasyon maliyeti yüksek; GPO modeliyle çakışabilir |

Varsayılan ve önerilen yol: **A — GPO/MSI MajorUpgrade** (bu belge).

## IT checklist (yeni sürüm)

- [ ] Yeni MSI share’de  
- [ ] Pilot OU’da Assigned upgrade  
- [ ] `Get-Service MngLogsAgent` = Running  
- [ ] `config show` / Local UI health  
- [ ] Collector’da host hâlâ veri gönderiyor  
- [ ] ProgramData wipe edilmedi (beklenen)  
