# Teslimat Omurgası — Oturum durumu

**Son güncelleme:** 3 Eylül 2026  
**Konu:** Teslimat omurgası (DI + Proje/`pm_*` + Operation Core) — paket kataloğu durak noktası  
**Ortam:** Odak test `192.168.20.20` · UI kontrolü lokal `npm run dev` · UI Docker image/deploy yok · backend deploy serbest  
**Manifest:** `docs/odak/project_management/install/manifest.json` **0.26.0**

**Ana referans:** [PLAN.md](./PLAN.md)

> **Kaldığımız yer:** Paket omurgasının somut dilimleri bitti (**F4-2**). Marketplace ve NLP/şartname parser bilinçli sonra. AnkaraBT şartnamesi **örnek kaynak**; ihale maddeleri hayata geçirilmeyecek.

---

## Son çalışılan konu

İş paketi kataloğu kapanış dilimleri:

1. **F4-1** — Paketten ince OC workspace iskeleti (proje `workspaceId` boşsa).
2. **F4-2** — Paket sökmede boş DI klasör silme (dolu ve paylaşılan klasörler kalır).

---

## Tamamlanan işler (bu hat)

Faz 1–3 ve paket kapanışı Odak test’te smoke ile doğrulandı (UI lokal).

| Dilim | Özet |
|--------|------|
| F1-0 … F1-9 | Kurulum, DI tür/ilişki, görsel kanıt, `pm_*`, Gantt, WBS–OC, iz/durum, karar, PMO+kalite tohumu |
| F2-1 … F2-13 | İç katalog: önizleme, skip/update, sökme; kapı, RAID, kapasite, bütçe, okundu, yükümlülük, denetim, toplantı, paydaş, portföy, süreç haritası |
| F3-1 … F3-5 | Sektör rafları (aynı katalog, yeni JSON): `architecture`, `proposal`, `eco`, `onboarding`, `acceptance` |
| **F4-1** | Paket apply / `packCode` ile create → ince OC workspace (durum, akış, tip, form, pano, profil). Sökme workspace silmez. Smoke: `scripts/tests/MngOperations/smoke-f41-pack-workspace-test.ps1` |
| **F4-2** | Sökmede boş DI klasör silme (UI: `Mng.Ui/utils/pmJobPack.ts`). Hub / dolu klasör / diğer paketin paylaştığı ad silinmez. Smoke: `scripts/tests/MngOperations/smoke-f42-pack-folder-detach-test.ps1` |

**Katalog (7 paket):** `pmo` · `quality` · `architecture` · `proposal` · `eco` · `onboarding` · `acceptance`

Paket JSON: `MngOperations/Core/MngOperations.Application/Packs/*.json` (embedded).

---

## Kararlar (hatırla)

- Generic teslimat omurgası. AnkaraBT DOCX **örnek**; şartname maddelerini parse edip o ihaleyi teslim etmiyoruz.
- Yeni mikroservis yok. Proje runtime: **MngOperations** `pm_*`. İşin tek kaynağı: OC. Belge: **MngDocument**.
- Paket şema yazmaz; WBS + (UI) DI klasör/starter + (F4-1) ince workspace.
- UI image Odak’a basılmaz; kontrol `npm run dev`.
- Token: `docs/odak/operationcore/scripts/get-operationcore-token.ps1` / installer’da taze `-Token` (eski `$env:DI_TOKEN` 401 verebilir).

---

## Bilinçli ertelenen

- Marketplace (üçüncü taraf, imza, ücret, izolasyon)
- DOCX/PDF içerik araması ve genel şartname maddesi çıkarımı (NLP) — ürün kararı; AnkaraBT gerekçesi yok
- Paketten tam OC kural / SLA / dashboard kopyası
- Kapı iş kilidi; RAID Monte Carlo; kaynak dengeleme / CPM; bütçe ERP/FX
- Okundu LMS/e-imza; yükümlülük otomatik madde; denetim ZIP; toplantı takvim/Teams
- Paydaş tenant/portal; süreç editörü/BPMN
- C4/Structurizr editörü

---

## Sonraki adımlar (yeni chat)

Paket omurgasında sıradaki somut dilim yok. Yeni oturumda:

1. Bu dosya + [PLAN.md](./PLAN.md) oku.
2. Kullanıcıdan öncelik al: bilinçli ertelenenlerden hangisi, yoksa başka hat.
3. Ortam kuralları: yalnızca Odak test `192.168.20.20`; UI lokal; backend deploy serbest.

Smoke örnekleri: `scripts/tests/MngOperations/smoke-f*.ps1`. Deploy: `scripts/odak/sync-odak-source.ps1 -Paths MngOperations` sonra `scripts/odak/deploy-odak-apps.ps1 -Services mngoperations -NoCache`.
