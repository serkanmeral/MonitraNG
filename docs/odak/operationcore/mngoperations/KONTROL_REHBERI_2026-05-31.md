# Operation Core — Birleşik Manuel Kontrol Rehberi (31 May 2026)

Bu dosya, **PERF oturumu kontrol rehberi** ([PERF_KONTROL_REHBERI.md](PERF_KONTROL_REHBERI.md)) ile bu chat'te (31 May) geliştirilen **tüm yeni işleri** birleştirir. Amaç: geliştirdiğimiz ama henüz manuel kontrol etmediğiniz her şeyi tek listeden, baştan sona doğrulayabilmeniz.

- **Ortam:** Odak — UI `http://192.168.20.20:3000`, gateway `http://192.168.20.20:5040`.
- **Deploy durumu:** Bu chat'in tüm çıktıları **canlı** (`mngkeeper`+`mngoperations`+`mngui` healthy; en son UI deploy 31 May ~22:35, `ui=200`).
- **Nasıl kullanılır:** Her madde bir kutucuk. Çalışıyorsa `[x]`, sorunluysa not düşün. Bölüm **0** hızlı API teyidi (sizin yerinize sistemi yoklar); **A–G** tarayıcıda elle kontrol.
- **İlgili:** [DEVAM.md](DEVAM.md) (tüm epiklerin tam kaydı), [PERF_OPTIMIZATION.md](PERF_OPTIMIZATION.md).

> Bu chat'te yapılanlar (kontrol kapsamı): **F-T2** (geçiş zorunlu alan ön-toplama), **F-K** (Kanban DnD geçiş), **BO-5/BO-6** (silme guard + alan temizleme), **BLF-10** (tags filtre), **NP-7** ("tümünü gör" bildirim sayfası), **BL-GRP/-2/-3** (grup adı çözümü + profil + filtre), **BL-KB** (Keeper by-ids toplu uç + Redis cache), **Faz-4/B** (büyük dosya bölme refactor — davranış birebir), **Faz-4/A** (Kanban "daha fazla yükle").

> **🩹 Bulunan & düzeltilen config bug (31 May ~23:10):** Sunucu UI'sinde board liste `405`, Workspace "Genel" boş, form'da öncelik id → **tek kök neden:** `Mng.Ui/nginx.conf`'ta `/api/operations/` proxy bloğu yoktu (tüm OC runtime çağrıları index.html'e düşüyordu). Eklendi + `mngui` deploy edildi; `/api/operations/v1/health/live` → 200 JSON ile doğrulandı. Kontrole devam etmeden önce tarayıcıda **hard refresh** yap. *(Detay: [DEVAM.md](DEVAM.md) → "UI-NGINX" bölümü. nginx.conf değişikliği henüz commit edilmedi.)*

---

## 0. Hızlı otomatik smoke (repo kökünden — opsiyonel ama önerilir)

```powershell
# Token + iş oluştur + profil/SLA + DG snapshot (transition adımı dahil dene)
pwsh -NoProfile -File ".\docs\odak\operationcore\scripts\smoke-sla-faz1.ps1" -WithTransition
```

- [ ] Çıktı `SLA-1 smoke tamam - OCD-xxxx`; profil `sla` DTO dolu; DG policy id eşleşiyor.
- [ ] Health: `gateway=200 ui=200` (deploy çıktısında veya `Invoke-WebRequest http://192.168.20.20:3000/`).

---

## A. Board liste — temel (PERF rehberi B bölümü, hâlâ geçerli)

URL: `/apps/operation-core/boards/{boardId}`

- [ ] Liste açılıyor; satırlar + toplam sayı doğru (server-side sayfalama).
- [ ] Sıralama (sortable başlık) çalışıyor, yön değişiyor.
- [ ] Hızlı filtre (durum/öncelik/tip) doğru sonuç.
- [ ] Serbest metin arama (debounce) çalışıyor; **"Aramayı temizle"** butonu listeyi sıfırlıyor.
- [ ] Relation/select sütun ve filtrelerde **etiket** görünüyor (ham id değil).
- [ ] **Computed (hesaplanan) sütun** değeri doğru (display-only; sort/filter kapalı — beklenen).
- [ ] **SLA chip** doğru faz/renk; sayaç ilerliyor (~1 dk).
- [ ] Audit sütunları (createdAt / createdBy / age) + sağda **sabit actions** sütunu doğru.
- [ ] Kanban moduna geçiş (varsa) çalışıyor (ilk geçişte minik gecikme = lazy yükleme, normal).

---

## B. Board liste — gelişmiş arama, grup & tags filtreleri (bu chat: BLF-10, BL-GRP-3)

Gelişmiş arama panelini açın (çok satırlı `[Alan][Operatör][Değer]`).

- [ ] **Sayısal/tarih alanlar** (BLF-8): `gt / gte / lt / lte / eq / ne`; tarih girişi datetime, sonuç doğru.
- [ ] **Relation alanlar** (BLF-9): filtre v-select (option = ad), `in / nin / eq / ne` doğru süzüyor.
- [ ] **Tags alanları** (BLF-10): filtre **v-combobox** (serbest giriş + chip), çoklu değer; `in / nin / eq / ne` ile dizi üyeliği doğru süzüyor; öneriler yüklü satırlardan geliyor.
- [ ] **Grup alanları** (BL-GRP-3): `assignmentGroups` + `personGroups`/`group` pool alanları filtrede **select** olarak (ham metin değil); `in / nin / eq / ne` doğru.
- [ ] Aynı alana **çoklu koşul** (AND) ezilmiyor; kullanıcı `stateId` filtresi board akış kapsamıyla **kesişiyor** (kapsam dışı state görünmüyor).

### Grup adı çözümü (BL-GRP / BL-GRP-2)

- [ ] Liste hücresinde grup alanları **grup adı** gösteriyor (ham grup id değil).
- [ ] Profil sayfasında (readonly form) grup alanları **grup adı** gösteriyor.

---

## C. Board liste — operasyonel aksiyonlar (bu chat: BO-5, BO-6)

URL: `/apps/operation-core/boards/{boardId}` → satır actions (canEdit gerekli).

### BO-5 — silmede ilişki guard'ı
- [ ] **Bağlı linki veya alt kaydı olan** bir işi silmeyi dene → uyarı (409 `WORK_ITEM_HAS_RELATIONS`) + **"Yine de sil"** butonu çıkıyor.
- [ ] "Yine de sil" (force) → kayıt siliniyor ve ilgili linkler temizleniyor.
- [ ] İlişkisiz işte normal silme tek adımda çalışıyor.

### BO-6 — edit'te alan temizleme
- [ ] Bir işi düzenle, **dolu bir alanı boşalt** (Açıklama / Atanan / Öncelik / Board) ve kaydet → alan **gerçekten temizleniyor** (eski değer geri gelmiyor).
- [ ] Değiştirilmeyen alanlar olduğu gibi kalıyor (yanlışlıkla temizlenmiyor).

---

## D. Durum geçişleri (bu chat: F-T2 profil, F-K Kanban)

### F-T2 — profilde geçiş + zorunlu alan ön-toplama
URL: `/apps/operation-core/work-items/{id}/profile`

- [ ] Header'da mevcut duruma göre **uygulanabilir geçiş butonları** görünüyor.
- [ ] `requiredFields` olan bir geçişe bas → onay dialog'unda **zorunlu alanlar inline** çıkıyor; mevcut değerlerle ön-doluyor.
- [ ] Zorunlu alanların hepsi dolmadan **"Uygula" pasif**; doldurunca aktif.
- [ ] Uygula → durum + timeline **yenileniyor**; girilen alan değerleri kayda işleniyor (400 hata yok).
- [ ] `requiredFields` olmayan geçiş → opsiyonel yorumla tek adımda uygulanıyor.

### F-K — Kanban DnD ile geçiş
Kanban modunda board (canEdit).

- [ ] Kartı uygun bir kolona sürükle-bırak → ilgili **transition uygulanıyor**, kolon yenileniyor.
- [ ] Çok-girişli kolonda **kaynak state'e göre doğru** geçiş seçiliyor.
- [ ] **Geçersiz** from→to (giriş geçişi yok / `dropEligible=false`) → bırakma reddediliyor veya kart **geri alınıyor** + uyarı.
- [ ] Hedef geçişin `requiredFields`'i varsa → kart **geri alınıyor** + "Profilde aç" snackbar (profil F-T2 ile alan topluyor).
- [ ] **Salt-okunur** board'da (canEdit=false) DnD kapalı.

---

## E. Bildirimler — "Tümünü gör" sayfası (bu chat: NP-7)

- [ ] Header bildirim dropdown footer'ında **"Tümünü gör"** linki var.
- [ ] Açılan sayfa (`/apps/operation-core/notifications`): server-side sayfalama (`v-pagination` + sayfa boyutu) çalışıyor.
- [ ] `Tümü | Yalnızca okunmamış` filtresi doğru.
- [ ] Tekil okundu + **toplu "tümünü okundu"** çalışıyor; rozet/sayı güncelleniyor.
- [ ] Kayda tıkla → ilgili iş kaydı **profiline** gidiyor.
- [ ] Tip ikon/renk (örn. `CommentMention` = @) + göreli zaman doğru.

---

## F. Person/grup dizin çözümü — Keeper by-ids + Redis cache (bu chat: BL-KB)

> Çoğunlukla şeffaf (backend). Görünür etki: adlar doğru ve hızlı çözülür; Keeper'da değişen ad MO'da yansır.

- [ ] Board liste + profil + Kanban kartlarında **person ve grup adları** doğru görünüyor (N+1 yerine toplu çözüm, davranış aynı).
- [ ] Keeper'da bir **kullanıcı/grup adını güncelle** → kısa süre sonra (cache TTL ~10 dk veya CRUD invalidation sonrası) MO tarafında yeni ad görünüyor.
- [ ] Keeper'da bir kullanıcı/grup **sil** → artık ad çözülmüyor (ham id'ye düşüyor), sistem patlamıyor.
- [ ] (Opsiyonel) Redis erişilemez olsa bile board/profil açılıyor (**fail-open** — Mongo'dan çözer).

---

## G. Faz-4 — refactor & ölçek (bu chat: B refactor, A "daha fazla yükle")

### Faz-4/A — Kanban "daha fazla yükle"
Bir Kanban kolonunda **`suggestedPageSize`'dan (vars. 50) fazla** iş olduğu bir board aç.

- [ ] Kolon altında **"Daha fazla yükle (n/total)"** butonu görünüyor (yüklü < toplam iken).
- [ ] Butona bas → sonraki sayfa **ekleniyor** (mevcutlar kaybolmuyor; tekrar/duplike kart yok), `n` artıyor.
- [ ] Tüm kartlar yüklenince buton kayboluyor.
- [ ] Yükleme sırasında buton spinner gösteriyor; **DnD ve mevcut kartlar bozulmuyor**.

### Faz-4/B — büyük dosya bölme refactor (regresyon kontrolü)
> Hedef: **davranış birebir aynı**. Refactor sadece `RuntimeContextService.cs` (C# partial) ve `operationCoreService.ts` (TS barrel) dosyalarını böldü. Aşağıdakiler taşınan kod yollarını yokluyor:

- [ ] **Profil** açılıyor (Detay/Aktivite/Ekler sekmeleri + sidebar) — `*.Form.cs` / `*.Directory.cs` yolları.
- [ ] **Form create/edit** modalı (tip/öncelik/state seçenekleri + initial state) doğru — `*.Form.cs`.
- [ ] **Dashboard widget** (varsa) doğru render — `*.Dashboard.cs`.
- [ ] TS taşınan domain'ler CRUD çalışıyor: **bildirimler**, **kurallar (rules)**, **SLA politikaları**, **zamanlanmış işler (schedules)**, **akışlar (state flows)** — listeleme/oluştur/güncelle/sil.

---

## Ek: ölçüm & geri alma

- **Perf ölçümünü tekrar açma** (gerekirse): `PERF_KONTROL_REHBERI.md` D bölümü (`PerfDiagnostics=true` → deploy → `docker logs ... | grep OC_PERF` → tekrar `false`).
- **Geri alma:** ilgili commit'i `git revert`. Bu chat commit'leri: BL-KB `f0f64cc`, Faz-4/B refactor `5c1d7fb`+`f98889b`, "daha fazla yükle" `90374ce`. Önceki backlog/PERF commit'leri DEVAM.md'de.
