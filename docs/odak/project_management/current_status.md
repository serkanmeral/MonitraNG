# Teslimat Omurgası — Oturum durumu

**Son güncelleme:** 17 Eylül 2026 (akşam, paket turu)  
**Konu:** Yedi iş paketi TEST UI turu + kurulum ilerleme penceresi + takvim TR locale  
**Ortam:** TEST `192.168.20.20`. Prod `192.168.20.8` draw.io kuruldu; **prod UI kontrolü hâlâ kullanıcıda.**  
**Manifest:** `docs/odak/project_management/install/manifest.json` **0.38.0** (şema değişmedi)  
**Git:** `origin` = GitHub `https://github.com/serkanmeral/MonitraNG.git`

**Ana referans:** [PLAN.md](./PLAN.md)

> **Kaldığımız yer:** Raftaki yedi paket TEST’te tek tek kuruldu (kullanıcı UI **Kur**, agent API doğruladı). Kalite üzerinde PMO birlikte kur/sök de görüldü. **SEED-PMO dokunulmadı.** Sonraki chat: prod süreç duman testi veya paket Diyagram boşluğu.

---

## Süreç + draw.io — kilit (önceki oturum, duruyor)

- Süreç sekmesi **sicil**; BPMN / OC motoru yok. Çizmek ≠ Resmi yap.
- Resmi gerçek: DI draw.io dosyası + sicil (`pm_process_maps`).
- Editör yazılmadı: resmi `jgraph/drawio:31.4.1` konteyner, host **8088**.
- TEST iframe `192.168.20.20:8088`. Prod HTTP `:8088` 200; UI origin pişmiş.
- Eğitim `18-surec.md` güncellendi (Çiz / Diyagramı düzenle / Resmi yap) ve TEST DI Eğitim→Project→Süreç’e yayınlandı.

---

## Paket turu — kilit (bu oturum)

Aynı motor, farklı JSON. Paket şema değil; WBS + DI klasör/starter + ince OC.

| Proje | Id | Paket | Not |
|---|---|---|---|
| Ornek — PMO | `027bdf17-6741-4001-835f-9c1412c42f21` | `pmo` | **SEED-PMO — silinmedi / dokunulmadı** |
| Kalite deneme | `cf6fb7cd-f3f9-4d62-9cec-75d07e341063` | `quality` | PMO eklendi sonra söküldü; Kalite kaldı |
| LAB-ACCEPTANCE | `86bc7d24-1a00-496c-9264-8c4541bd8266` | `acceptance` | |
| LAB-PROPOSAL | `542e1059-beac-4133-873f-8d1ed88c0295` | `proposal` | |
| LAB-ARCHITECTURE | `b156bccf-7b6a-44ce-9b52-c0ffbc7b0ca2` | `architecture` | |
| LAB-ECO | `87a0f4b3-3b74-49b3-92c8-bd9da8452cd2` | `eco` | ECO = Engineering Change Order; ECN = Engineering Change Notice |
| LAB-ONBOARDING | `eee8251f-89ff-47bb-9210-754b71043572` | `onboarding` | |

Silinen eski demo’lar (SEED-PMO hariç): F51 smoke, SEED-ACCEPTANCE/ARCHITECTURE/ECO/ONBOARDING/PROPOSAL/QUALITY, TST1.

**Birlikte kur / sök (Kalite):** PMO WBS 4–7 eklendi; tekrar kur hepsini atladı; sök 4–7 ve WI 0010–0021’i aldı. Kalite WBS 1–3 ve işleri kaldı. Dolu PMO klasörleri ve paylaşılan boş Diyagram silinmedi.

**Kurulum ilerleme:** Backend tek POST (WBS/workspace/OC). Asıl adım adım ilerleme DI klasör/starter. UI: kalıcı kontrol listesi (`PmPackApplyProgressDialog.vue`); sahte yüzde yok.

**Takvim dili:** FullCalendar varsayılanı İngilizceydi; `tr` locale + Vuetify `locale: tr` + `v-locale-provider`. Native `type="date"` tarayıcı/OS dilini izler (bilinçli).

---

## Tamamlanan

- Toplantı sicilleri — `73f031f4`
- Self-host draw.io + süreç çizim — `b48d21c2`
- Eğitim `18-surec.md` gerçek UX’e çekildi
- Paket kurulum ilerleme penceresi
- Toplantı takvimi / Vuetify TR locale
- Yedi paket UI turu + Kalite üzerinde PMO kur/sök

---

## Bilinen boşluklar (ürün, tur başarısız değil)

1. Paket JSON’daki `diagram` (`*.drawio`) `applyJobPackDocuments` basmaz; yalnızca klasör + markdown starter. **Diyagram klasörü boş kalır.**
2. OC iş anahtarı proje kodunun ilk 12 alfasayısını alır (`LABONBOARDIN-0001`, `LABACCEPTANC-*`, `LABARCHITECT-*`).
3. Bazı lab’lerde Kararlar / Wiki / Yüklemeler / Toplantı notları klasörleri paket JSON’dan değil PM kütüphane kromundan gelir.
4. Sök için ilerleme penceresi yok. Proje oluştururken paket seçiminde aynı modal yok.

---

## Devam eden / sonraki chat

1. **Prod UI kontrolü** (kullanıcı ileri bıraktı): süreç → yerinde önizleme → Edit diagram; iframe `192.168.20.8:8088`, CDN değil.
2. İstenirse paket `diagram` starter’ını bas (draw.io dosyası).
3. İstenirse OC key kısaltması / sök ilerleme / create-with-pack modal.
4. Odak Sipariş uncommitted duruyor — bu hatta karışmaz.
5. SIEM/alarm vs. backend’ler prod’da eski; PM için gerekmedi.

---

## Önemli notlar

- Token TEST: `docs/odak/operationcore/scripts/load-operationcore-token.ps1` (odak / odak_admin).
- UI deploy kullanıcı talebi olmadan yok; bu hatta TEST/prod draw.io için önceki oturumda onay vardı. Paket turu UI’si local `npm run dev` ile doğrulandı.
- Eğitim paket sayfası: [egitim/08-paketler.md](./egitim/08-paketler.md).
