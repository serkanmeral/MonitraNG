---
title: MngLogs Local UI ve CLI
service: MngLogs
category: guides
tags: [local-ui, cli, pin, port, event-log]
---

# Local UI ve CLI kullanım rehberi

## Local UI sayfaları

| Sayfa | Rol |
|-------|-----|
| Durum | Metrik / Event Log / İzleme / Aktivite sekmeleri |
| Kuyruk | Disk outbound backlog |
| Kaynaklar | Salt-okunur config + paket detayı |
| Loglar | Son produced/shipped; satır detay modal |
| Politika | PIN ile korunan sistem + politika (sekmeler) |

## Politika PIN

1. İlk girişte PIN oluştur (min 4 karakter).
2. Sonraki girişlerde unlock; oturum ~20 dk (kayar).
3. **Kilitle** oturumu kapatır.
4. PIN unutulursa CLI: `pin reset --yes` → UI’da yeniden setup.

Yazma korumalı: policy/system kaydı, servis listesi, exe gözat, katalog sync.

## Event Log paketleri

- **Sunucu paketleri:** Katalog önbelleği (şimdilik builtin); agent’ta aç/kapa (`disabledServerPackages`).
- **Agent override:** Aynı isim değiştirir, yeni isim ekler.
- **Efektif:** `sunucu ⊕ override (− kapalı)`.
- Eski tam `packages` listesi varsa UI’da **Override modeline aktar**.

## CLI kurtarma

```powershell
cd MngLogs\Presentation\MngLogs.Agent\bin\Release\net9.0-windows

.\MngLogs.Agent.exe status
.\MngLogs.Agent.exe port check
.\MngLogs.Agent.exe port set 5093
.\MngLogs.Agent.exe pin reset --yes
.\MngLogs.Agent.exe pin set
```

`--data-dir` ile pilot/data dizinini verin. Port veya PIN değişince **agent’ı yeniden başlatın**.

Port 5092 doluysa agent başlamaz; konsol/log CLI komutlarını yazar.
