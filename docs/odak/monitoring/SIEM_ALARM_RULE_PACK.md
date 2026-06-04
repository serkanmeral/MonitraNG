# SIEM B3 — Hazır alarm kural paketi (`siem-mvp-v1`)

**Durum:** ✅ MVP paket + seed script  
**Son güncelleme:** 4 Haziran 2026

---

## 1. Amaç

U1–U7 SIEM senaryolarını **MITRE ATT&CK** ve **ISO 27001** etiketleriyle birlikte domain'e tek seferde yüklemek. E2E testleri geçici kurallar oluşturmaya devam eder; paket **operasyonel varsayılan** kuralları sağlar.

---

## 2. Paket konumu

| Dosya | Açıklama |
|-------|----------|
| `tests/fixtures/siem/alarm_rules/packages/siem-mvp-v1/manifest.json` | Paket tanımı + senaryo→MITRE/ISO eşlemesi |
| `tests/fixtures/siem/alarm_rules/u*.json` | Kural gövdeleri (eşik, groupBy, sequence) |

**Paket ID:** `siem-mvp-v1` · **Sürüm:** `1.0.0`

---

## 3. MITRE eşlemesi (özet)

| Senaryo | Technique | Tactic |
|---------|-----------|--------|
| U1 Brute force | T1110.001 Password Guessing | TA0006 Credential Access |
| U2 Fail→success | T1078 Valid Accounts | TA0001 Initial Access |
| U3 Privileged (bakım dışı) | T1078.002 Domain Accounts | TA0004 Privilege Escalation |
| U4 Deny spike | T1046 Network Service Discovery | TA0007 Discovery |
| U5 Trafik sıçraması | T1048 Exfiltration Over Alternative Protocol | TA0010 Exfiltration |
| U6 Kural değişikliği | T1562.004 Disable or Modify System Firewall | TA0005 Defense Evasion |
| U7 Yeni akış | T1021 Remote Services | TA0008 Lateral Movement |

ISO etiketleri: `ISO27001:A.*` (manifest içinde `complianceTags`).

---

## 4. Kural metadata modeli

`mon_alarm_rules` belgesinde opsiyonel `metadata` alanı:

```json
{
  "metadata": {
    "packageId": "siem-mvp-v1",
    "packageVersion": "1.0.0",
    "scenarioId": "U1",
    "description": "...",
    "threatTacticId": "TA0006",
    "threatTechniqueId": "T1110.001",
    "complianceTags": ["ISO27001:A.8.5", "ISO27001:A.12.4"]
  }
}
```

Alarm tetiklendiğinde `context` içine `scenarioId`, `threatTechniqueId`, `complianceTags` kopyalanır (operatör / workflow için).

---

## 5. Odak'ta yükleme

```powershell
pwsh scripts/odak/seed-siem-alarm-rule-pack.ps1
pwsh scripts/odak/test-siem-alarm-rule-pack-seed.ps1
```

| Parametre | Açıklama |
|-----------|----------|
| `-Replace` | Mevcut paket kurallarını sil + yeniden oluştur |
| `-DryRun` | Yalnızca listele, POST yok |
| `-PackageId` | Varsayılan `siem-mvp-v1` |

**Idempotent:** Aynı `packageId` + `scenarioId` varsa atlanır (`-Replace` olmadan).

**Deploy:** `MngAlarm` değişikliği sonrası `mngalarm` + `mngalarm-worker` rebuild gerekir.

---

## 6. Referanslar

- [SIEM_PLANNING.md §14](./SIEM_PLANNING.md) — MITRE konumlandırma
- [SEC_EVENT_OBSERVATION_MAP.md](./SEC_EVENT_OBSERVATION_MAP.md) — matchKey eşlemesi
- [tests/fixtures/siem/alarm_rules/README.md](../../tests/fixtures/siem/alarm_rules/README.md)
