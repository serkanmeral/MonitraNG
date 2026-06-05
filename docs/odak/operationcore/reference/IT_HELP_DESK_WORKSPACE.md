# IT Destek — Workspace taslağı (Production)

**Amaç:** Kurumsal IT help desk — donanım, erişim, yazılım ve genel IT talepleri. Tüm çalışanlar talep açar; **MonitraNG Users** ekibi triyaj ve çözüm yapar.  
**Durum:** Prod’da seed tamamlandı (2026-06-05)  
**İlişki:** [IT_HELP_DESK_REFERENCE.md](./IT_HELP_DESK_REFERENCE.md) (genel referans) · [MONITRANG_FEEDBACK_WORKSPACE.md](./MONITRANG_FEEDBACK_WORKSPACE.md) (MonitraNG uygulama geri bildirimi — ayrı WS)

---

## 1. Kapsam kararları

| Konu | Karar |
|------|--------|
| Workspace adı | **IT Destek** |
| `workspaceType` | `service_desk` |
| Work item anahtarı | `HD-0001` (`workItemKeyPrefix`: `HD`) |
| Tipler | Olay · Hizmet talebi · Problem kaydı · Erişim talebi |
| Talep açan kitle | **`users`** (tüm domain kullanıcıları) |
| IT / agent ekibi | **`MonitraNG Users`** |
| Kanban | Yok (Faz 1) |
| Bildirim | Yok (şimdilik) |
| Menü | Ayrı menü girişi yok — Operasyon Merkezi workspace ağacında ikinci workspace |
| Ortam | **Yalnızca prod** (`192.168.20.8`) |

**MonitraNG Geri Bildirim ile ayrım:** Uygulama içi hata/öneri → `MNG-`; genel IT (yazıcı, VPN, erişim vb.) → `HD-`.

---

## 2. Yetki modeli

| Seviye | `viewGroups` | `editGroups` | Not |
|--------|--------------|--------------|-----|
| Workspace | `users` | `users` | Herkes workspace’i görür ve kayıt açar |
| `adminGroups` | — | — | `admins` |
| Board **Talep oluştur** | `users` | `users` | Self-service giriş |
| Board **Agent kuyruğu** | `MonitraNG Users` | `MonitraNG Users` | Tam triyaj görünümü |

**Faz 1 notu:** Transition izinleri kişi bazında ayrılamaz. Kayıt profilindeki agent aksiyonları teorik olarak `users` üyelerine de görünebilir; süreç disiplini ve ileride `op_rules` / transition `permissions.groups` ile sıkılaştırılır. `info_provided` (Müşteri bekleniyor → İşlemde) talep sahibinin yanıt vermesi için `users` tarafında da anlamlıdır.

---

## 3. Work item tipleri

| Ad | `category` | Varsayılan? |
|----|------------|-------------|
| Olay (Incident) | `incident` | Evet |
| Hizmet talebi | `service_request` | Hayır |
| Problem kaydı | `problem` | Hayır |
| Erişim talebi | `service_request` | Hayır |

Global tipler (`workspaceId` boş); workspace `enabledTypeIds` ile seçer.

---

## 4. Durumlar (`op_states`)

Prefix: `IT Destek -`

| Görünen ad | `category` | Bayraklar |
|------------|------------|-----------|
| Yeni | `open` | `isInitial`, `isStart` |
| Atandı | `in_progress` | — |
| İşlemde | `in_progress` | — |
| Müşteri bekleniyor | `on_hold` | — |
| Çözüldü | `closed` | `allowReopen` |
| Kapalı | `closed` | `isClosed`, `isTerminal` |

---

## 5. State flow — `IT Destek — Standard Flow`

`initialStateId` = **Yeni**

| `transitionKey` | Geçiş | Etiket |
|-----------------|--------|--------|
| `assign` | Yeni → Atandı | Ata |
| `start_work` | Atandı → İşlemde | İşleme al |
| `start_from_new` | Yeni → İşlemde | Doğrudan işle |
| `wait_customer` | İşlemde → Müşteri bekleniyor | Müşteriden yanıt bekle |
| `resume` | Müşteri bekleniyor → İşlemde | Devam et |
| `resolve` | İşlemde → Çözüldü | Çöz |
| `close` | Çözüldü → Kapalı | Kapat |
| `reopen` | Çözüldü → Atandı | Yeniden aç |
| `reopen_closed` | Kapalı → Atandı | Yeniden aç |

`resolve` öncesi `resolutionSummary` zorunlu (`op_rules`).

**Varsayılan atama (`op_rules`):** `WorkItemCreated` → `assignee` boşsa **serkan.meral** (`6a2262026723c2bd54eec3c9`).

---

## 6. Pool alanları

| `key` | Etiket | `fieldType` |
|-------|--------|-------------|
| `requestCategory` | Talep kategorisi | `text` |
| `affectedUser` | Etkilenen kullanıcı | `persons` |
| `affectedAsset` | Etkilenen varlık | `text` |
| `resolutionSummary` | Çözüm özeti | `text` |

Core: `title`, `description`, `typeId`, `priorityId`, `impact`, `urgency`, `assignee`.

---

## 7. Form — `IT Destek — Yeni kayıt`

| Alan | Davranış |
|------|----------|
| `title`, `description`, `typeId` | Zorunlu |
| `priorityId` | Varsayılan **Orta (P3)** |
| `impact`, `urgency` | Görünür |
| `requestCategory`, `affectedUser`, `affectedAsset` | Görünür |
| `labels` | Görünür; katalog **`op_tags`** (workspace etiketleri); varsayılan ön-dolum yok |
| `assignee` | **Gizli** — IT atar |

**Etiketler (`op_tags`):** Şifre değişimi · Hesap / Erişim · Donanım · Yazılım · Ağ / VPN · E-posta · Yazıcı · Güvenlik · Mobil / Telefon · Genel

Agent kuyruğu board’unda **Etiketler** kolonu gösterilir.

---

## 8. Board’lar

| Board | `viewType` | Kolonlar | Hedef |
|-------|------------|----------|--------|
| **Talep oluştur** | `list` | Yeni, Atandı | Self-service talep + kısa takip |
| **Agent kuyruğu** | `list` | Yeni … Kapalı (6) | IT triyaj ve çözüm |

Kanban yok.

---

## 9. Özet pano (`op_dashboards`)

**Ad:** `IT Destek — Özet pano` · `isDefault: true` · `scope: workspace`

| Satır | Widget | Tip |
|-------|--------|-----|
| 1 | Yeni talepler | summaryCard |
| 1 | Atandı | summaryCard |
| 1 | İşlemde | summaryCard |
| 1 | Müşteri bekleniyor | summaryCard |
| 2 | SLA yanıt ihlali | summaryCard |
| 2 | Yeni talepler — tip | chart (donut) |
| 3 | İşlemde — öncelik | chart (bar) |
| 3 | Son yeni talepler | list |
| 3 | Bana atanan açık | list |

**SLA:** Olay (Incident) P1 — yanıt 15 dk / çözüm 4 saat (demo değerleri).

---

## 10. UI erişimi

- **Operasyon Merkezi** → workspace seçici / ağaç → **IT Destek** (MonitraNG Geri Bildirim’in yanında)
- Ayrı yan menü öğesi **yok**
- `users` grubundaki kullanıcı: workspace + **Talep oluştur** board
- `MonitraNG Users`: ayrıca **Agent kuyruğu** board

---

## 11. Seed

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
.\docs\odak\operationcore\scripts\setup-operation-core-datasets-prod.ps1   # ilk kurulum
.\docs\odak\operationcore\scripts\seed-operation-core-helpdesk-prod.ps1
```

**Özet:** [operationcore-helpdesk-prod-seed.json](../scripts/operationcore-helpdesk-prod-seed.json)

---

## 12. Doğrulama checklist

- [ ] `users` üyesi Operasyon Merkezi ağacında **IT Destek** görür
- [ ] `users` üyesi **Talep oluştur** board’unda kayıt açar → `HD-0001`
- [ ] `users` üyesi **Agent kuyruğu** board’unu **görmez**
- [ ] `MonitraNG Users` her iki board’u görür
- [ ] Varsayılan öncelik **Orta (P3)**; `assignee` formda gizli
- [ ] `resolve` geçişinde `resolutionSummary` zorunlu
- [ ] Pano sekmesi kart ve listeleri doldurur
- [ ] MonitraNG uygulama geri bildirimi hâlâ **MonitraNG Geri Bildirim** WS’inde
