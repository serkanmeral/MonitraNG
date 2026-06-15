# Mimari karar — Sipariş modülü (MonitraNG)

**Durum:** Onaylı taslak · 15 Haziran 2026 (Faz 1a: DG-only geçici model uygulandı)  
**Bağlam:** Eski Kalite analizi · müşteri UX direnci · OC workspace süreç yeteneği

> **Faz 1a uygulama notu (15 Haziran 2026):** Migrasyon ve hub listesi için **birincil üst kayıt `odak_is_paketleri` (DG)** kullanıldı; MO `op_work_items` entegrasyonu bilinçli olarak **sonraki faza** bırakıldı. Kalemler `parentPackageId` ile bağlı. MO POC (3 paket ODF) referans olarak kalır.

---

## 1. Karar özeti

MonitraNG sipariş/planlama modülü **üç katmanlı hibrit** model ile inşa edilecek:

```
┌─────────────────────────────────────────────────────────┐
│  KATMAN A — Kullanıcı arayüzü (tanıdık kabuk)          │
│  Hub listeler · sekmeli detay · arama · export          │
│  Route: Odak Sipariş modülü (OC menüsünde ayrı giriş)   │
└───────────────────────────┬─────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────┐
│  KATMAN B — Süreç motoru (Operation Core workspace)     │
│  İş paketi = work item · durum akışı · NCR/CAPA         │
│  Otomasyon · board (opsiyonel) · atama · termin         │
└───────────────────────────┬─────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────┐
│  KATMAN C — İş verisi (MngDataGateway datasets)         │
│  Kalemler · sevkiyat hareketleri · stok (ileride)       │
│  Master: müşteri · ürün · malzeme                       │
└─────────────────────────────────────────────────────────┘
```

**Reddedilen alternatifler:**

| Alternatif | Neden red |
|------------|-----------|
| Tüm veriyi yalnızca `op_work_items` | PO kalemleri tablo verisi; board şişer |
| Yalnızca OC workspace tree/kanban | Müşteri alışkanlığı liste+sekmeler; direnç |
| Eski Kalite’yi fork/wrap | Bakım maliyeti · PHP 3.10 teknik borç |
| Pixel-perfect UI klonu | OC kazancını yok eder |

---

## 2. Workspace rolü

### Workspace’te yaşayan

| Kavram | OC yapısı |
|--------|-----------|
| İş paketi (üst süreç) | Work item · tip: **İş Paketi** — **Faz 1a:** `odak_is_paketleri` (DG) · MO link sonraki faz |
| Yaşam döngüsü | `op_states` + `op_state_flows` (planlandı → üretim → kalite → sevk) |
| NCR / CAPA | Mevcut tipler · `parentItemId` → iş paketi |
| Atama, termin, öncelik | OC native |
| Otomasyon | `op_workspace_automations` (planlı) |

### Workspace dışında (dataset)

| Kavram | Dataset (taslak ad) |
|--------|---------------------|
| **İş paketi üst kayıt (Faz 1a)** | **`odak_is_paketleri`** ✅ birincil |
| Sipariş kalemleri | `odak_siparis_kalemleri` |
| Sevkiyat header / satır | `odak_sevkiyatlar` · `odak_sevkiyat_kalemleri` |
| Malzeme / stok (ileride) | `odak_malzemeler` · hareket defteri |
| Müşteri master | `odak_musteriler` (mevcut) |
| Ürün master | `odak_urunler` (mevcut) |

**Bağlantı:** Kalemlerde `parentPackageId` → `odak_is_paketleri` (Faz 1a). İleride opsiyonel `workItemId` → OC work item.

---

## 3. Workspace seçimi

| Seçenek | Artı | Eksi | Öneri |
|---------|------|------|-------|
| **A) Odak Üretim genişlet** | NCR/CAPA hazır · tek workspace | Sipariş + üretim karışık board | Kısa vadede POC |
| **B) Yeni Odak Sipariş workspace** | Net modül ayrımı | İkinci seed · NCR link cross-ws | Orta vadede üretim |

**Faz 1 POC:** **A** — mevcut Odak Üretim workspace; UI’da “İş Paketi” terminolojisi.  
**Faz 2:** Müşteri hacmine göre **B** değerlendirilir.

---

## 4. UI stratejisi — “görünür” vs “gizli” OC

| Kullanıcı görür | Arka planda |
|-----------------|-------------|
| Odak Sipariş menüsü | OC workspace id |
| İş Paketleri listesi | DG query (`odak_is_paketleri`) · MO board değil |
| Detay · Kalemler sekmesi | Dataset CRUD API |
| Durum: “Üretimde”, “Sevk edildi” | OC state transition |
| (Opsiyonel) Kanban görünümü linki | OC board |

Kullanıcıya **workspace hub tree** dayatılmaz (U6 — [UX_UYUMLULUK_HARITASI.md](./UX_UYUMLULUK_HARITASI.md)).

---

## 5. Süreç tipi testi (gelecek modüller)

Yeni özellik eklerken:

> *“İnsan üzerinde bekleyen, durumu olan, kapanması gereken bir iş mi?”*

| Örnek | Workspace | Dataset / hub |
|-------|-----------|---------------|
| Sipariş yürütme | ✅ | Kalemler |
| Stok talebi onayı | ✅ | Stok hareketi |
| Stok bakiye | ❌ | Dataset |
| Zimmet teslim | ✅ | Zimmet kaydı |
| Demirbaş sicil | ❌ | Dataset |
| Envanter sayım oturumu | ✅ | Sayım satırları |

Bu kalıp stok/envanter/demirbaş fazlarında **yeniden kullanılır**.

---

## 6. API / servis sınırları (taslak)

| İşlem | Yol |
|-------|-----|
| Liste / arama | UI → DG query (`odak_is_paketleri`) |
| İş paketi CRUD | DG dataset · AF `odak-is-paketleri-form` (Faz 1a) · MO sonraki faz |
| Kalem CRUD | UI → DG `/data/api/v1/datasets/odak_siparis_kalemleri` |
| Durum geçişi | MO transition API (MO entegrasyonu sonrası) |
| Export | MO veya UI xlsx (Faz 1b) |
| PO dosya | Document Intelligence / object storage (Faz 1b) |
| Döküman paketi | Paket kataloğu + iş kaydına otomatik link (Faz 1b+ — taslak) |

### 6.1 Döküman paketi (taslak — detay sonra)

Eski Kalite’de NAS/klasör convention ile yapılan tekrarlı dosya ilişkilendirme yerine:

- **Döküman paketi:** önceden tanımlı dosya seti
- Kullanıcı iş açarken **tek tıkla paket seçer**; paket üyeleri ilgili WI/dataset/NCR kaydına **otomatik linklenir**
- Merkezi depolama (DI / object storage); NAS manuel kopya akışının yerini hedefler

Tam spesifikasyon: [DOKUMAN_PAKETI_NOTU.md](./DOKUMAN_PAKETI_NOTU.md)

---

## 7. Güvenlik ve roller

- Keycloak grupları ↔ eski `roles` eşlemesi ayrı tablo (ileride)
- Fiyat alanları: OC `canEdit` / field policy
- PO PDF: tam vs redacted (eski `polink` / `porlink` mantığı)

---

## 8. Faz planı

| Faz | Kapsam |
|-----|--------|
| **0** | Eski app referans (Docker / 192.168.20.30) |
| **1a** | ✅ DG-only: `odak_is_paketleri` + kalem dataset + migrasyon (824 paket) |
| **1** | Hub/detay UI (MVP checklist) · MO `workItemId` link |
| **1b** | PO PDF · export · fiyat alanları |
| **2** | Sevkiyat dataset + sekmeler |
| **3** | Migrasyon toplu · cutover |
| **4** | Stok / envanter (aynı platform deseni) |

---

## 9. İlgili dokümanlar

- UX detay: [UX_UYUMLULUK_HARITASI.md](./UX_UYUMLULUK_HARITASI.md)
- Veri: [VERI_MIGRASYON_PLANI.md](./VERI_MIGRASYON_PLANI.md)
- OC taslak: [../is_surecleri/referans/ODAK_URETIM_WORKSPACE_TASLAK.md](../is_surecleri/referans/ODAK_URETIM_WORKSPACE_TASLAK.md)
