# MngDocument — Oturum durumu

## Son Çalışılan Konu

**Birleşik ürün roadmap + Workflow sınırı:** [DI_PRODUCT_ROADMAP.md](../odak/document_intelligence/DI_PRODUCT_ROADMAP.md) (Faz D-WF dahil) · [ODAK_MO_VS_WORKFLOW_SCENARIOS.md](../odak/workflow/ODAK_MO_VS_WORKFLOW_SCENARIOS.md) — 15 Odak senaryosu, DI’da workflow’a yaslanılacak yerler, event sözleşmesi.

Önceki implementasyon oturumu: Odak **kalem belgeleri** (CoC + Activity), **LINE-ACTIVITY-STD** şablon tasarımı, belge üretimi (generation profiles), parametre uyarıları, DI deep link, şablon **görüntüle / kilidi aç** akışı.

## Tamamlanan İşler (bu oturum)

### Planlama (3 Tem 2026)
- **DI_PRODUCT_ROADMAP.md** — birleşik faz planı: P, D, **D-BR**, D-E, **D-WF**, D-P, D-S, D-N, D5, S, Pr, AI, M.
- **Faz D-BR** — paylaşımlı antet kataloğu (D-BR1) + opsiyonel kapak sayfası (D-BR2); üretimde seçim.
- **ODAK_MO_VS_WORKFLOW_SCENARIOS.md** — MO / Alarm / DI / Workflow karar matrisi (15 senaryo).
- **D-WF dilimleri:** D-WF0 event publish → D-WF1 CoC onay → D-WF2 lifecycle API → D-WF3 haftalık rapor dağıtımı → D-WF4 AI düşük güven onayı.
- **Sınır:** D-N1 tek mail Notifier’da; onay + çok adım + gecikme Workflow’da.

### Belge üretimi (backend) — önceki oturum
- `DocumentGenerationService` — merge, placeholder koruma, `HasParameterWarnings`, tanımsız/boş parametre analizi.
- Generation profilleri: `odak.coc.fromLine`, `odak.line.activity.fromLine` (`appsettings.json`).
- `DocumentContextCatalog` — kalem/paket/sevkiyat/CoC/activity alanları.
- Activity writeback: `activityDiResourceId`, `activityDocNo`, `activityGeneratedAt`, vb.
- `DocumentParameterResolver` — bool → Evet/Hayır.
- **`POST /templates/{id}/unpublish`** — üretimde aktif şablonu düzenlenebilir yapma (belge üretimi unpublish sonrası durur).

### Activity şablonu (LINE-ACTIVITY-STD)
- Profesyonel DOCX gövdesi: `build-line-activity-seed-docx.ps1` + `line-activity-docx-content.json` (UTF-8, Türkçe karakter düzeltmesi).
- Seed/deploy: `seed-designer-template-line-activity-standard.ps1`, `deploy-line-activity-design-test.ps1`, `patch-line-activity-standard-test.ps1`.
- Test sunucusunda şablon **published**; Türkçe etiketler doğrulandı.
- `styles.xml` bozukluğu giderildi (Collabora “belge yüklenemedi” hatası).

### UI — Kalem belgeleri (Odak)
- Sekme: **Kalem Belgeleri** (`documents`, `?tab=coc` alias).
- `OdakSiparisLineDocumentsPanel`, `OdakSiparisLineDocumentsCreateDialog`.
- Combobox: COC-STANDARD + LINE-ACTIVITY-STD.
- `odakSiparisLineDocumentService.ts`, deep link `/apps/document-intelligence/r/{id}`.

### UI — Belge tasarımcısı
- **Üretimde aktif** / **Düzenlenebilir** terminolojisi.
- Published şablon: **Görüntüle** (Collabora salt okunur) + **Kilidi aç** (unpublish → düzenle).
- Editör sayfası: unpublish + üretimde aktif et butonları.

### Dataset
- `odak_siparis_kalemleri` — activity writeback alanları (36 alan).

## Devam Eden / Faz 2

- Activity DOCX: sevkiyat satır tablosu, timeline, computed parametreler.
- Parametre scanner uyarısı (33 incomplete `{` — footer/header XML, fonksiyonel).
- Test deploy **bekliyor** — otomatik sync SSH kimlik doğrulama hatası (`Permission denied`); manuel çalıştırılmalı.

## Git

- **Commit:** `de245f1f` — `feat(document-intelligence): kalem belgeleri, activity sablonu ve sablon unpublish`
- **Push:** GitHub + GitLab `main` ✅ (29 Haz 2026)

## Sonraki Adımlar (yeni oturum)

1. **D-BR1 — Antet kataloğu** — paylaşımlı tanımlar, şablon varsayılanı, üretimde seçim (roadmap §8).
2. **Parametre yönetimi UI** — placeholder envanteri, context binding, incremental docNo (D2), designer parametre diyalogu iyileştirme.
3. **D-BR2** — kapak sayfası kataloğu (opsiyonel üretim seçimi).
4. **D-E1–E2** — Collabora pre-gate (home_mode 20/10 limit tamponu).
5. **D-WF0** — `document.generated` event publish; CoC için D-N1 vs D-WF1 ayrımını netleştir.
6. Odak senaryo matrisi (#12 CoC onay, #15 haftalık rapor) — paydaş doğrulaması.

## Önemli Notlar

- Test gateway: `http://192.168.20.20:5040`, WOPI: `:5095`.
- Activity deploy: `docs/odak/document_intelligence/scripts/deploy-line-activity-design-test.ps1`
- Published şablon WOPI yazılamaz; düzenleme için **unpublish** veya seed `-Replace` (ops).
- Token: `docs/odak/operationcore/scripts/load-operationcore-token.ps1`

## Son Güncelleme

**3 Temmuz 2026** — Birleşik DI roadmap (+ D-WF, **D-BR** antet/kapak) planlandı. Implementasyon sırası: antet katalog (D-BR1) + parametre → D-E → D-P → D-N/D-WF0.

## Nerede Kalmıştık

Belge tasarımı oturumu hedeflerine ulaşıldı. Yeni chat’te **antet** ve **parametre yönetimi** ile devam edilecek.

**Test deploy (henüz yapılmadı):**
```powershell
.\scripts\odak\sync-odak-source.ps1 -Server 192.168.20.20 -Paths @('MngDocument','Mng.Ui','docs/odak/document_intelligence')
.\scripts\odak\deploy-odak-apps.ps1 -Server 192.168.20.20 -Services mngdocument,mngui -NoCache
```
Unpublish API + bool Evet/Hayır + kalem belgeleri UI bu deploy sonrası test’te doğrulanmalı.
