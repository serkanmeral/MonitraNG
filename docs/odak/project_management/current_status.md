# Teslimat Omurgası — Oturum durumu

**Son güncelleme:** 17 Eylül 2026 (gece)  
**Konu:** Paket diyagram dosyası + kur/sök/yeni proje ilerleme + OC öneki  
**Ortam:** TEST `192.168.20.20` (`mngoperations` no-cache). UI local `npm run dev`; **mngui image yok.** Prod `192.168.20.8` draw.io kuruldu; **prod UI kontrolü hâlâ kullanıcıda.**  
**Manifest:** `docs/odak/project_management/install/manifest.json` **0.38.0** (şema değişmedi)  
**Git:** `origin` = GitHub `https://github.com/serkanmeral/MonitraNG.git` — `main`

**Ana referans:** [PLAN.md](./PLAN.md)

> **Kaldığımız yer:** Yedi paket UI turu bitti. Diyagram boşluğu, sök/yeni proje ilerleme penceresi ve yeni workspace öneki kapandı (Onboarding’de `Onboarding akışı.drawio` doğrulandı). **SEED-PMO dokunulmadı.** Sonraki: prod süreç duman testi veya kalan boşluklar (örnek akış şablonu, eski lab önekleri).

---

## Süreç + draw.io — kilit (duruyor)

- Süreç sekmesi **sicil**; BPMN / OC motoru yok. Çizmek ≠ Resmi yap.
- Resmi gerçek: DI draw.io dosyası + sicil (`pm_process_maps`).
- Kendi editör yok: `jgraph/drawio:31.4.1`, host **8088**.
- TEST iframe `192.168.20.20:8088`. Prod HTTP `:8088` 200; UI origin pişmiş.
- Eğitim `18-surec.md` güncellendi ve TEST DI Eğitim→Project→Süreç’e yayınlandı.

---

## Paket — kilit

Aynı motor, farklı JSON. Paket şema değil; WBS + DI klasör/starter/diyagram + ince OC.

| Proje | Id | Paket | Not |
|---|---|---|---|
| Ornek — PMO | `027bdf17-6741-4001-835f-9c1412c42f21` | `pmo` | **SEED-PMO — silinmedi / dokunulmadı** |
| Kalite deneme | `cf6fb7cd-f3f9-4d62-9cec-75d07e341063` | `quality` | PMO eklendi sonra söküldü; Kalite kaldı |
| LAB-ACCEPTANCE | `86bc7d24-1a00-496c-9264-8c4541bd8266` | `acceptance` | |
| LAB-PROPOSAL | `542e1059-beac-4133-873f-8d1ed88c0295` | `proposal` | |
| LAB-ARCHITECTURE | `b156bccf-7b6a-44ce-9b52-c0ffbc7b0ca2` | `architecture` | |
| LAB-ECO | `87a0f4b3-3b74-49b3-92c8-bd9da8452cd2` | `eco` | ECO = Engineering Change Order; ECN = Engineering Change Notice |
| LAB-ONBOARDING | `eee8251f-89ff-47bb-9210-754b71043572` | `onboarding` | Eksikleri tamamla → `Onboarding akışı.drawio` |

Silinen eski demo’lar (SEED-PMO hariç): F51 smoke, SEED-ACCEPTANCE/ARCHITECTURE/ECO/ONBOARDING/PROPOSAL/QUALITY, TST1.

**Birlikte kur / sök (Kalite):** PMO WBS 4–7 eklendi; sök 4–7 ve WI 0010–0021’i aldı. Kalite kaldı. Dolu klasörler ve paylaşılan Diyagram silinmedi.

**İlerleme:** Backend tek POST (WBS/workspace/OC). DI adımları kalıcı listede (`PmPackApplyProgressDialog.vue`): klasör, sayfa, diyagram. Aynı pencere **Paketler sekmesi**, **yeni proje + paket** ve **sök** için. Sahte yüzde yok.

**Diyagram:** `applyJobPackDocuments` boş draw.io basar (`diCreateBlankDrawio`). Varsa atlar. Örnek akış XML’i yok.

**OC öneki (yeni workspace):** proje kodu, tire korunur, en fazla 64 (`LAB-ONBOARDING-0001`). Eski lab’ler kesik önekte kaldı (`LABONBOARDIN-0001`).

**Takvim dili:** FullCalendar `tr` + Vuetify `locale: tr`. Native `type="date"` OS dilini izler.

---

## Tamamlanan

- Toplantı sicilleri — `73f031f4`
- Self-host draw.io + süreç çizim — `b48d21c2`
- Paket kurulum ilerleme + takvim TR — `c0b4d80f`
- Yedi paket UI turu + Kalite üzerinde PMO kur/sök
- Paket `diagram` dosyası, sök/yeni proje ilerleme, yeni workspace öneki (kod local + TEST OC; commit yok)

---

## Kalan ürün boşlukları

1. Paket diyagramı **boş tuval**; örnek akış şablonu yok.
2. Eski lab workspace önekleri migrate edilmedi (`LABONBOARDIN-…`).
3. Wiki / Kararlar / Yüklemeler / Toplantı notları paket JSON’dan değil; `pmProjectLibrary` kromu.
4. Paket sürüm yükseltince mevcut proje yapısı nasıl evrilir (PLAN soru 8).
5. **Prod UI kontrolü:** süreç → yerinde önizleme → Edit diagram; iframe `192.168.20.8:8088`, CDN değil.
6. Native tarih kutusu OS dili (bilinçli).

Eğitim 00–07 ve 09 repo `egitim/` altında yok (yalnız `.tmp-di-egitim-project`). `08-paketler.md` TEST DI’ya son haliyle basılmadı.

---

## Devam eden / sonraki chat

1. Prod süreç/editör duman testi (kullanıcı).
2. İstenirse örnek draw.io şablonu / eski önek migrasyonu / paket sürüm evrimi.
3. İstenirse eğitim 00–07 repo + DI yeniden yayın.
4. Odak Sipariş uncommitted — bu hatta karışmaz.

---

## Önemli notlar

- Token TEST: `docs/odak/operationcore/scripts/load-operationcore-token.ps1` (odak / odak_admin).
- UI deploy kullanıcı talebi olmadan yok.
- Eğitim paket sayfası: [egitim/08-paketler.md](./egitim/08-paketler.md).
