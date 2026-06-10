# Odak Uretim — Devam noktasi (checkpoint)

**Son guncelleme:** 11 Haziran 2026 00:10 (oturum kapanisi)  
**Durum:** Odak test (`192.168.20.20`) — liste gorunumu iyilestirmeleri **commit + push + mngui deploy** tamamlandi.  
**Git commit:** `586ceec` — `feat(oc): Odak board list view — MO fieldDisplays, UI filters, dates, icons`  
**Deploy saglik:** `gateway=200 ui=200 oc_live=200` (mngui + mngoperations Odak test)

> **KALDIĞIMIZ YER (10 Haz 2026):** Odak Uretim **Uretim panosu** liste gorunumu uzerinde calisildi. Backend (MO) relation etiketleri ve sütun label zenginlestirmesi **Odak test'e deploy edildi**. UI tarafinda filtre sadelestirme, tarih formatlama, katalog ikonlari ve relation lookup bug fix **commit + mngui deploy** ile canliya alindi. **Yarin:** tarayici/Odak test uzerinde dogrulama, gelismis arama senaryolari, gerekirse seed `listColumns` label sync.

---

## Bu oturumda tamamlananlar (10 Haz 2026)

### MngOperations (deploy edildi — Odak test)

| Konu | Aciklama |
|------|----------|
| Board list `fieldDisplays` | Relation pool alanlari (customerId, productId vb.) sunucuda cozulup liste API'sinde donuyor |
| `listColumns.label` | Board context'te `op_fields.label` ile zenginlestirme |
| Dosyalar | `RuntimeContextService.BoardList.cs`, `WorkItemCardDto.FieldDisplays`, `GetBoardAsync` / `GetBoardListAsync` |

Deploy komutu (yapildi):
```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -Command "& { .\scripts\odak\sync-odak-source.ps1 -Paths @('MngOperations','ApplicationResources/mng_apps') }"
pwsh -NoProfile -ExecutionPolicy Bypass -Command "& { .\scripts\odak\deploy-odak-apps.ps1 -Services mngoperations -NoCache }"
```

### Mng.Ui (commit + mngui deploy)

| Konu | Aciklama |
|------|----------|
| Relation lookup bug | `useOcBoardRelationLookups` — `parseOcLookupFromFieldOptions` arguman sirasi duzeltildi (liste kiriliyordu) |
| `fieldDisplays` | `OcWorkItemCard.fieldDisplays` + liste hucrelerinde MO etiketleri oncelikli |
| Hizli filtre kaldirildi | Yalnizca **gelismis arama** (kapali varsayilan, arama satirinda kompakt) |
| Filtre bug | `fetchList(true)` + gelismis arama scalar deger cikarimi |
| Tarih formatlama | Pool + sistem sütunlari `formatCellValue` (plannedDate datetime, date-only alanlar) |
| Katalog ikonlari | Durum / oncelik / tip — tanimli ikon veya category/level varsayilan Tabler ikonlari + renk |
| Yeni dosyalar | `useOcBoardRelationLookups.ts`, `ocCatalogDefaultIcons.ts` |

### Seed (script guncellendi, sync opsiyonel)

- `seed-operation-core-odak-uretim.ps1` — `listColumns` icin `label` alanlari (Musteri, Urun, Planlanan bitis vb.)
- MO zaten `op_fields`'tan label dolduruyor; seed tekrar calistirmak sadece board config icin faydali

---

## Workspace / ortam

| Alan | Deger |
|------|--------|
| Workspace | Odak Uretim — `9f9cc085-81c7-4a92-9fa2-357ad5c654cd` |
| Uretim panosu boardId | `75ec624c-a8be-4131-b072-9408ace1fd32` |
| Gateway | http://192.168.20.20:5040 |
| UI (Odak test) | http://192.168.20.20:3000 |
| Lokal UI dev | `npm run dev` (gateway Odak'a yonlendirilmeli) |

**UI:** http://192.168.20.20:3000/apps/operation-core/workspace → **Odak Uretim** → **Uretim panosu** (liste gorunumu)

---

## Bilinen / acik noktalar

1. **Gelismis arama** — backend filtre calisiyor (MO); UI fix'leri deploy sonrasi Odak'ta dogrulanmali (stateId, relation eq/in vb.)
2. **SignalR / locales 404** — lokal dev'de Hub ve keeper locale gürültüsü; listeyi engellemez
3. **op_fields 503** — ara sira DG; MO `fieldDisplays` ile relation hucreleri yine dolabilir
4. **Kanban kartlari** — ikonlar simdilik liste sütunlarinda; kanban chip'leri ayri iyilestirme olabilir
5. **Musteri geri bildirimi** — alan / durum revizyonu bekleniyor (asagidaki “sonraki adimlar”)

---

## Sonraki adimlar (yarin)

1. **Odak test dogrulama** — liste: musteri/urun adlari, tarih formati, durum/oncelik/tip ikonlari, gelismis arama (or. Durum = Kapandi)
2. Gerekirse `-SeedDemo` / seed sync (listColumns label) — MO label zenginlestirme yetiyorsa atlanabilir
3. Kapali isler (ODF-0007 vb.) board scope + filtre ile gorunurluk kontrolu
4. Musteri geri bildirimi sonrasi alan / akis revizyonu
5. Keycloak grup haritasi (`uretim`, `kalite`, `depo`) — planli
6. Opsiyonel: kanban kartlarinda katalog ikonlari

---

## Kurulum (Odak test — ilk kurulum / yeniden seed)

```powershell
# Repo kokunden
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\is_surecleri\scripts\run-odak-uretim-full-setup.ps1
```

Deploy (UI):
```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -Command "& { .\scripts\odak\sync-odak-source.ps1 -Paths @('Mng.Ui') }"
pwsh -NoProfile -ExecutionPolicy Bypass -Command "& { .\scripts\odak\deploy-odak-apps.ps1 -Services mngui }"
```

---

## Onceden tamamlananlar (ozet)

| # | Cikti |
|---|--------|
| 1 | [referans/ODAK_URETIM_WORKSPACE_TASLAK.md](./referans/ODAK_URETIM_WORKSPACE_TASLAK.md) |
| 2 | Master dataset + seed scriptleri (`setup-odak-master-datasets`, `seed-odak-master-data`) |
| 3 | `seed-operation-core-odak-uretim.ps1` — OC metadata + demo ODF kayitlari |
| 4 | Board modeli: Uretim panosu (liste) · Kalite kuyrugu · Depo sevkiyat |

---

## Ilgili dokumanlar

- [README.md](./README.md)
- [referans/ODAK_URETIM_WORKSPACE_TASLAK.md](./referans/ODAK_URETIM_WORKSPACE_TASLAK.md)
- [../operationcore/mngoperations/DEVAM.md](../operationcore/mngoperations/DEVAM.md)
- [../deploy/README.md](../deploy/README.md)
