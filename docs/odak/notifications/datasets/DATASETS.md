# MngNotifier — DG dataset tasarimi

**Son guncelleme:** 3 Haziran 2026  
**Kapsam:** Yalnizca Notifier (MO entegrasyonu ayri calisma)

---

## 1. Genel

| Dataset | Amac |
|---------|------|
| `@mail_templates` | Subject + body fragment; `templateKey` ile Notifier render |
| `@mail_layouts` | Header/footer + CSS; domain logosu ve adi icin placeholder |

**Konum:** Tenant domain Mongo (DG); domain basina bir set.  
**Okuyucu:** MngNotifier (`send-template` sirasinda DG API).  
**Yonetici:** Domain admin (Automated Forms / ileride Notifier admin UI).

**publish_mode:** `none` — CRUD sonrasi RabbitMQ gerekmez; Notifier HTTP ile okur.

---

## 2. `@mail_templates`

### 2.1 Alanlar

| Alan | Tip | Zorunlu | Aciklama |
|------|-----|---------|----------|
| `templateKey` | text | Evet | Benzersiz anahtar; cagiran servis bunu gonderir |
| `name` | text | Evet | Admin listesi |
| `description` | text | Hayir | Aciklama |
| `subject` | text | Evet | Mail konusu; `{{...}}` placeholder |
| `bodyHtml` | text | Evet | **Icerik fragment** (layout disi orta blok) |
| `variables` | text[] | Hayir | Zorunlu context yollari (render validasyonu) |
| `layoutKey` | text | Hayir | `@mail_layouts.layoutKey`; bos = `default` |
| `locale` | text | Hayir | `tr` (varsayilan), `en`, … |
| `category` | text | Evet | `system` \| `custom` — system silinemez (UI kurali) |
| `tags` | text[] | Hayir | Filtre / gruplama (`operation-core`, `keeper`, …) |
| `sampleContext` | object | Hayir | Onizleme JSON (Notifier preview endpoint) |
| `isActive` | bool | Evet | Pasif sablon render edilmez |

### 2.2 Indeksler

- `templateKey` — **unique**
- `isActive` + `templateKey` — listeleme
- `category` — system/custom filtre

### 2.3 Placeholder sozlesmesi

- Format: `{{path.to.field}}` — `context` JSON icinde nokta notasyonu
- Ornek yollar: `workItem.key`, `domain.displayName`, `actor.email`
- Notifier: bilinmeyen placeholder uyarisi; `variables[]` icindeki eksik alan → **400**

### 2.4 `bodyHtml` kurallari

- Tam HTML dokuman **degil**; `<h1>`, `<p>`, `<div class="info-box">` gibi fragment
- Layout (`@mail_layouts`) `<html>`, header tablosu, footer'i sarar
- Script tag yasak (render sirasinda strip)

---

## 3. `@mail_layouts`

Domain markasi ve ortak cerceve. Content sablonu `bodyHtml` layout **icine** yerlesir.

### 3.1 Alanlar

| Alan | Tip | Zorunlu | Aciklama |
|------|-----|---------|----------|
| `layoutKey` | text | Evet | Benzersiz (`default`, `minimal`, …) |
| `name` | text | Evet | Admin listesi |
| `description` | text | Hayir | |
| `stylesCss` | text | Hayir | `<style>` blogu (inline email CSS) |
| `headerHtml` | text | Evet | Ust blok; `{{domain.logoUrl}}`, `{{domain.displayName}}` |
| `footerHtml` | text | Evet | Alt blok |
| `isDefault` | bool | Evet | Tenant'ta tek varsayilan layout |
| `isActive` | bool | Evet | |
| `category` | text | Evet | `system` \| `custom` |

### 3.2 Indeksler

- `layoutKey` — **unique**
- `isDefault` — varsayilan layout secimi

### 3.3 Logo

- Header'da `{{domain.logoUrl}}` kullanilir (img `src`)
- `context.domain.logoUrl` yoksa logo satiri atlanir veya metin-only header
- Base64 logo **desteklenmez** (email istemci / boyut)

---

## 4. Notifier render birlestirme

```text
1. templateKey → @mail_templates kaydi
2. layoutKey (sablondan veya "default") → @mail_layouts
3. subject ← template.subject + placeholder replace
4. content ← template.bodyHtml + placeholder replace
5. fullHtml ← layout wrapper(styles + header + content + footer)
6. SMTP
```

Slot: layout `headerHtml` / `footerHtml` icinde de `{{domain.*}}` replace edilir.

---

## 5. Dosyalar

| Dosya | Icerik |
|-------|--------|
| [notifier_dataset_category.json](./notifier_dataset_category.json) | DG kategori |
| [notifier_datasets.json](./notifier_datasets.json) | Dataset semalari (2 adet) |
| [notifier_mail_layouts_seed.json](./notifier_mail_layouts_seed.json) | Ornek layout kayitlari |
| [notifier_mail_templates_seed.json](./notifier_mail_templates_seed.json) | Ornek sablon kayitlari |

Kurulum scripti: *(sonraki adim — `docs/odak/notifications/scripts/`)*

---

## 6. Cagiran servis sozlesmesi (referans)

Notifier disinda; MO ayri calisma. Cagiran yalnizca gonderir:

```json
{
  "to": ["..."],
  "templateKey": "work-item-created",
  "context": { "workItem": {}, "domain": {}, "actor": {} }
}
```

Hangi `templateKey` ne zaman kullanilir — **cagiran servisin** konfigurasyonu (Notifier bakmaz).

---

**Ilgili:** [../MAIL_TEMPLATES.md](../MAIL_TEMPLATES.md) · [../MAIL_ARCHITECTURE.md](../MAIL_ARCHITECTURE.md)
