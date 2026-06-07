# E-posta şablonları — MngNotifier

**Son güncelleme:** 7 Haziran 2026  
**Durum:** Faz 0 implementasyon — [DEVAM.md](./DEVAM.md)

---

## 1. Özet

| Konu | Nerede |
|------|--------|
| Şablon içeriği (`subject`, `bodyHtml`) | DG `@mail_templates` |
| Layout (header logo, footer, CSS) | DG `@mail_layouts` |
| Render + SMTP | **MngNotifier** |
| Hangi geçiş → hangi template | **MO** `op_notification_policies` |
| Template UI | DG CRUD ekranı (Faz 3) |

Çağıran gönderir: `{ templateKey, subject?, context, to }` → `POST /api/v1/notifications/send-template`

---

## 2. Body modeli

- `bodyHtml` = **içerik fragment** (`<h1>`, `<p>`, `<div class="info-box">` …)
- Layout = tam e-posta çerçevesi (`<html>`, header, footer, CSS)
- UI editörü fragment düzenler; layout system seed ile başlar

---

## 3. Placeholder

- Format: `{{path.to.field}}` — `context` JSON nokta notasyonu
- Değerler HTML-encode edilir (body/subject)
- `variables[]` eksik context → **400**
- Subject override (policy `emailSubject`) aynı motorla render edilir

---

## 4. API

### send-template

```http
POST /api/v1/notifications/send-template
Authorization: Bearer <token>   # DG okuma için zorunlu
```

```json
{
  "to": ["user@example.com"],
  "templateKey": "work-item-transitioned",
  "subject": null,
  "context": {
    "workItem": { "key": "WI-42", "title": "..." },
    "transition": { "key": "resolve", "fromState": "Open", "toState": "Done" },
    "actor": { "displayName": "..." },
    "domain": { "displayName": "...", "logoUrl": "https://..." }
  }
}
```

**Subject önceliği:** request `subject` → template `subject` (her ikisi de placeholder render).

### preview-template

SMTP yok; rendered `subject` + `htmlBody` döner.

### mail (legacy)

`POST /notifications/mail` — bootstrap; yeni entegrasyonlar `send-template` kullanmalı.

---

## 5. Render akışı

```text
templateKey → @mail_templates
layoutKey (veya default) → @mail_layouts
subject + bodyHtml placeholder replace
layout wrap (styles + header + content + footer)
boş domain.logoUrl → <img> satırı atlanır
SMTP
```

---

## 6. Backlog

### Faz 0 (Notifier)

- [ ] DG client
- [ ] TemplateRenderService
- [ ] send-template + preview-template
- [ ] Dataset kurulum scripti

### Faz 1+ (MO / UI)

- [ ] MO orchestrator entegrasyonu — [MO_MAIL_POLICIES.md](./MO_MAIL_POLICIES.md)
- [ ] Template admin UI

---

## 7. Kararlar

| Tarih | Karar |
|-------|--------|
| 3 Haz 2026 | Render Notifier'da; içerik DG'de |
| 7 Haz 2026 | Body fragment + layout |
| 7 Haz 2026 | Subject override (policy + request) |

İlgili: [MAIL_ARCHITECTURE.md](./MAIL_ARCHITECTURE.md) · [datasets/DATASETS.md](./datasets/DATASETS.md)
