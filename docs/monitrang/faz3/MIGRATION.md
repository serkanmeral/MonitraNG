# Faz 3 — Migration / Deploy Checklist

**Tek kaynak:** Müşteri **test** ve **prod** ortamlarında dataset, seed, menü patch ve servis deploy sırası burada tutulur.

**Kural:** Kod `git pull` yetmez. Bu listedeki adımlar (varsa) aynı dilimde çalıştırılmadan ortam “güncel” sayılmaz.  
**Sıra:** Her zaman önce **test**, yeşil smoke sonrası **prod**.

**Son güncelleme:** 13 Temmuz 2026

---

## 1. Genel pull → deploy akışı

```text
1. git pull (müşteri sunucu / ilgili branch)
2. Etkilenen servisler: docker build / compose up (NoCache gerekirse)
3. Bu dosyadaki dilim satırlarını sırayla uygula
4. Smoke checklist
5. Prod’a aynı komutlar (test OK ise)
```

| Ortam | Gateway / not |
|:---|:---|
| Odak test | örn. `192.168.20.20:5040` — güncel envanter müşteri dokümanında |
| Odak prod | müşteri prod envanteri |

---

## 2. Dilim günlüğü

Her geliştirme diliminde **yeni satır ekleyin** (en üstte veya kronolojik altta — tutarlı olun: **yeniler üstte**).

| Dilim | Tarih | Paket | Dataset / şema | Seed / script | Deploy servisleri | Test | Prod | Commit | Not |
|:---|:---|:---|:---|:---|:---|:---:|:---:|:---|:---|
| — | — | — | — | — | — | ☐ | ☐ | — | İskelet; henüz dilim yok |

### Satır doldurma rehberi

| Kolon | Ne yazılır |
|:---|:---|
| **Dilim** | Kısa id (`DI-AI1`, `RPT-HTTP`, `MON-ANOM` …) |
| **Paket** | `document_intelligence` / `reporting` / … |
| **Dataset / şema** | Dataset adları, yeni alanlar, index |
| **Seed / script** | Tam path + örnek komut (`pwsh -File …`) |
| **Deploy servisleri** | `mngui`, `mngdocument`, `mngoperations`, … |
| **Test / Prod** | Uygulandı mı |
| **Commit** | Kısa hash veya PR |

---

## 3. Komut şablonu (örnek — dilime göre doldurun)

```powershell
# 1) Pull (müşteri sunucu)
git pull origin main

# 2) Deploy (örnek)
# .\scripts\odak\deploy-odak-apps.ps1 -Services "mngui,mngdocument" -NoCache

# 3) Dataset / seed (örnek — gerçek script dilimde yazılır)
# pwsh -File .\docs\odak\...\scripts\setup-....ps1 -BaseUrl http://...:5040

# 4) Smoke
# ... paket work.md veya scripts/tests altındaki smoke
```

---

## 4. Ortak dikkatler

- Seed **idempotent** olmalı veya “sadece bir kez” notu satırda belirtilmeli.  
- Person / group id remap, domain adı (`odak`) ortamdan ortama değişebilir.  
- Anket (`survey_portal`) barındırma A/B kararı yokken prod deploy planı yazılmaz.  
- SIEM bu fazın Monitoring kapsamına dahil değil.

---

## 5. Paket → olası artefakt tipleri (hatırlatma)

| Paket | Tipik migration içeriği |
|:---|:---|
| ai_platform | MngLLM/Ollama config, `dm_resource_ai` / job dataset, embedding koleksiyon |
| document_intelligence | `dm_*` dataset, şablon seed, letterhead/cover, menü; **DI-T:** test persona/grup (yalnız test — prod’a fixture yok) |
| reporting | `@reporting_*`, rapor seed, menü |
| monitoring | `mon_*` / asset, alarm kuralları, paneller |
| production_operations | OC workspace/board/flow seed, otomasyon |
| package_module | İş paketi dataset / UI / DI bağları |
| survey_portal | Portal config, tenant, mail — **sonra** |
