# Teslimat Omurgası — Oturum durumu

**Son güncelleme:** 17 Eylül 2026 (akşam)  
**Konu:** Proje Dashboard (chart) + Gantt açılış + sol sekme + pulse + ilk boya + DI Gezgin kök yükleme  
**Ortam:** TEST `192.168.20.20` (`mngoperations` no-cache). UI local `npm run dev`; **mngui image yok.**  
**Manifest:** `docs/odak/project_management/install/manifest.json` **0.38.0** (şema değişmedi)  
**Git:** `origin` = GitHub `https://github.com/serkanmeral/MonitraNG.git` — `main`

**Ana referans:** [PLAN.md](./PLAN.md)

> **Kaldığımız yer:** Proje Dashboard grafik duvarı (ApexCharts) Durum’dan ayrı. Açılış **Gantt**. Sekmeler solda. `GET /projects/{id}/pulse` belgesiz; Durum hâlâ tam tarama. DI Gezgin kök klasörleri `?folderId` olmadan bootstrap eder. **SEED-PMO WBS/WS dokunulmadı** (paket kurulum satırı 1.1.1 damgalı).

---

## Proje yüzeyi — kilit (bu oturum)

- **Açılış:** Gantt (iskelet WBS; iş/kanıt hydrate arka planda).
- **Dashboard:** Plan grubunda Durum’un üstünde. Kart + donut/bar; aksiyon listesi yok. Grafik ilgili sekmeye gider.
- **Durum:** satır satır tarama (kanıt/plan/onay). Dashboard’un kopyası değil.
- **Pulse:** `GET /projects/{id}/pulse` — extras var, `LoadDocumentsByWorkItem` yok. WBS kovaları (yaprak), kapı, açık RAID, bütçe plan/gerçek.
- **Sekmeler:** masaüstünde sol dikey (OC workspace tanımları ile aynı); dar ekranda üstte yatay.
- **Liste/detay boya:** `ListProjects` WBS taramaz; `GetProject` hydrate:false; `GET .../wbs` zenginleştirir; Durum hâlâ ~7 sn (DG extras).

## DI Gezgin

Kök `/apps/document-intelligence` (`folderId` yok) artık bootstrap atlar. Yenile butonu gerekmez.

## Paket / SLA (önceki kilit, duruyor)

Paketler **v1.1.1**, `slaPolicies` yok. TEST’te üç PMO kurulum satırı 1.1.1 (ApplyPack yok). SEED-PMO eski OC SLA politikası durabilir.

| Proje | Id | Not |
|---|---|---|
| Ornek — PMO | `027bdf17-6741-4001-835f-9c1412c42f21` | SEED-PMO — WBS/WS dokunulmadı |
| LAB-WSDEFAULT | `e33659a5-ba4f-405c-bfb1-8922148607c2` | |
| TST-PRJ | `587f5b2d-0708-4b9c-8e66-6152f0affaeb` | |

---

## Tamamlanan (bu oturum)

- Dashboard chart yüzeyi + pulse DTO/uç
- Gantt varsayılan; sekmeler sol
- Liste / detay / rollup ilk boya
- DI Tüm kaynaklar ilk yükleme
- TEST `mngoperations` yenilendi (`oc_live=200`); pulse SEED-PMO chart serileri doğrulandı

---

## Kalan ürün boşlukları

1. Paket diyagramı **boş tuval**; örnek akış şablonu yok.
2. Eski lab workspace önekleri migrate edilmedi.
3. Wiki / Kararlar / Yüklemeler / Toplantı notları paket JSON’dan değil.
4. Paket sürüm yükseltince mevcut proje yapısı (PLAN soru 8).
5. **Prod UI kontrolü:** süreç iframe.
6. Durum sekmesi DG extras (~7 sn).
7. Eğitim 00–07 ve 09 repo `egitim/` altında yok. Dashboard cümlesi 08/18’e işlendi; DI Eğitim yeniden yayın yok.

---

## Devam eden / sonraki chat

1. Prod süreç/editör duman testi (kullanıcı).
2. İstenirse örnek draw.io şablonu / önek migrasyonu / paket sürüm evrimi.
3. İstenirse Durum DG maliyeti.
4. Odak Sipariş uncommitted — bu hatta karışmaz.

---

## Önemli notlar

- Token TEST: `docs/odak/operationcore/scripts/load-operationcore-token.ps1` (odak / odak_admin).
- UI deploy kullanıcı talebi olmadan yok.
- Sahte burndown yok (tarihsel snapshot yok).
