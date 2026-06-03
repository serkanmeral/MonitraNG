# E-posta sablonlari — MngNotifier

**Son guncelleme:** 3 Haziran 2026  
**Durum:** ⏸️ Planlama tamamlandi — implementasyon sonraki oturum ([DEVAM.md](./DEVAM.md))

---

## 1. Ozet

| Konu | Nerede |
|------|--------|
| Sablon icerigi (subject, bodyHtml) | DG `@mail_templates` |
| Layout (header logo, footer) | DG `@mail_layouts` |
| Render + SMTP | **MngNotifier** |
| Hangi event → hangi templateKey | Cagiran servis (Notifier bakmaz) |

Cagiran servis gonderir: `{ templateKey, context, to }` → `POST /api/v1/notifications/send-template`

---

## 2. Dataset tasarimi

Tam sema, alanlar, indeksler, placeholder kurallari:

**[datasets/DATASETS.md](./datasets/DATASETS.md)**

Dosyalar:

- [notifier_datasets.json](./datasets/notifier_datasets.json) — 2 dataset semasi
- [notifier_mail_layouts_seed.json](./datasets/notifier_mail_layouts_seed.json)
- [notifier_mail_templates_seed.json](./datasets/notifier_mail_templates_seed.json)

---

## 3. Render akisi (Notifier)

```text
send-template { templateKey, context, to }
    → @mail_templates (templateKey)
    → @mail_layouts (layoutKey veya "default")
    → subject + bodyHtml placeholder replace
    → layout sarimi (styles + header + content + footer)
    → SMTP
```

**Logo:** Header'da `{{domain.logoUrl}}` + `{{domain.displayName}}`. `context.domain` veya Notifier domain branding cozumlemesi.

---

## 4. API (hedef)

```http
POST /api/v1/notifications/send-template
Content-Type: application/json

{
  "to": ["user@example.com"],
  "templateKey": "work-item-created",
  "context": {
    "workItem": { "key": "WI-1", "title": "..." },
    "actor": { "displayName": "..." },
    "domain": { "displayName": "...", "logoUrl": "https://..." }
  }
}
```

Ham mail (`POST /mail`) bootstrap / gecis icin kalir.

Onizleme (plan): `POST /notifications/preview-template` — SMTP yok, rendered subject/html doner.

---

## 5. Notifier uygulama backlog

- [ ] DG client: `@mail_templates`, `@mail_layouts` okuma
- [ ] `TemplateRenderService` — placeholder + HTML encode + layout birlestirme
- [ ] Bos `domain.logoUrl` → `<img>` satirini atlama
- [ ] `SendTemplate` endpoint + DTO
- [ ] `variables[]` eksik context → 400
- [ ] Dataset kurulum scripti
- [ ] Preview endpoint (opsiyonel)

---

## 6. Kararlar

| Tarih | Karar |
|-------|--------|
| 3 Haz 2026 | Render Notifier'da; cagiran `templateKey` + `context` |
| 3 Haz 2026 | Icerik DG'de; `@mail_templates` + `@mail_layouts` |
| 3 Haz 2026 | MO entegrasyonu bu planin disinda |

Ilgili: [MAIL_ARCHITECTURE.md](./MAIL_ARCHITECTURE.md) · [DEVAM.md](./DEVAM.md)
