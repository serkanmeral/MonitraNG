# DLP — Kaldığımız yer

**Son güncelleme:** 2 Eylül 2026  
**Durum:** Dilim 0 + Dilim 1 motor sahada; Outlook COM kuruldu, **yükleme IT Office aktivasyonuna park**  
**Ana plan:** [DLP_PLANNING.md](./DLP_PLANNING.md) · [POLICY.md](./POLICY.md) · [LAB.md](./LAB.md) · **Durum:** [current_status.md](./current_status.md)

---

## Dilim 0 (bitti)

- Kod + Odak DG PATCH (`dm_tags` 11 field, `dm_resources.classificationTagId`)
- Damga: Office `MngDlp.*` / PDF `% MngDlp:` — [POLICY.md](./POLICY.md) §7

## Dilim 1 (motor bitti; eklenti kuruldu, doğrulanmadı)

- Collector `GET /api/v1/policy/dlp` (yayımlanmış snapshot, ETag) · `PUT /dlp` taslak · `POST /dlp/publish`
- Seed: POLICY.md örnek kurallar, `enforcementMode: auditOnly`
- Agent 1.0.11: `dlp-policy.json`, `%ProgramData%\MngLogs\Agent\dlp-local.key`, `POST http://127.0.0.1:5092/dlp/evaluate`
- Lab PowerShell yeşil: `scripts/tests/MngLogs/dlp/test-dlp-evaluate.ps1`
- Identity: `unresolved` (Keeper grup cache sonraki)
- UI kural ekranı yok; BFF allowlist `policy/dlp*` hazır
- Outlook add-in: `MngLogs/Presentation/MngLogs.OutlookAddin/` + `MngLogs/scripts/install-outlook-addin.ps1`
  - C2R sanal HKLM + `C:\Program Files\MngLogs\OutlookAddin\` yazıldı
  - COM Add-ins kutusunu işaretlemek yetmez (Click-to-Run HKCU’yu boot etmez)
  - Active / Event 45 / `addin.log` **teyit edilmedi** (lisans sihirbazı)

## Park (şimdi)

IT: Microsoft 365 Apps aktivasyonu (bu PC, Classic Outlook).  
`noreply@odakkompozit.com.tr` ile Office’e giriş yok.

## Sıradaki (aktivasyon sonrası)

1. Outlook aç → lisans/hesap sihirbazını atla → **MngLogs DLP = Active**
2. Dilim 1 ItemSend lab (auditOnly)
3. SIEM DLP kural paneli (opsiyonel)
4. Dilim 2 `enforce`

Plan açık: ortak `DlpEngine` collector simülasyonuna paylaşılmadı (agent içi).

---

## Okuma sırası (yeni chat)

1. [README.md](./README.md)
2. [current_status.md](./current_status.md) — nerede kaldık
3. [DLP_PLANNING.md](./DLP_PLANNING.md) §2
4. [POLICY.md](./POLICY.md)
5. [LAB.md](./LAB.md)
