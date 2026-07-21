# Faz 3 — Agent Başlangıç Kuralları

> **Yeni chat için giriş dosyası.**  
> Kullanıcı bu dosyayı gösterdiğinde veya “faz3 agent start / bootstrap oku” dediğinde agent **önce bu dosyayı okur** ve aşağıdaki kurallara uyarak çalışır.

**Konum:** `docs/monitrang/faz3/AGENT_START.md`  
**Kapsam:** Odak Kompozit teklifi (ODK-FT-2026-001) geliştirme — Faz 3  
**Son güncelleme:** 13 Temmuz 2026 (§6 lokal Docker backend / UI `npm run dev` ayrımı)

---

## 0. Yeni chat — kopyala-yapıştır prompt

```text
docs/monitrang/faz3/AGENT_START.md dosyasını oku ve kurallarına uy.
Paket: [ai_platform | document_intelligence | reporting | monitoring | production_operations | package_module | survey_portal]
Görev: [...]
```

İsteğe bağlı: ilgili paketin `Roadmap.md` + `work.md` dosyalarını da oku.
AI backend omurgası için paket: `ai_platform`.

---

## 1. Bu faz nedir?

Odak Kompozit fiyat teklifindeki hizmet paketlerinin **geliştirme planı ve iş takibi**.

| Okunacak | Rol |
|:---|:---|
| [README.md](./README.md) | Paket indeksi, bağımlılık, **önerilen geliştirme sırası**, ortam akışı |
| [MIGRATION.md](./MIGRATION.md) | Dataset / seed / deploy checklist (tek kaynak) |
| `{paket}/Roadmap.md` | Major plan (fazlar, kapsam, kabul) |
| `{paket}/work.md` | Yapılanlar, kalan iş, blocker, commit |
| `docs/odak/commercial/Odak_Kompozit_Fiyat_Teklifi_MUSTERI.md` | Müşteri teklifi (kapsam kaynağı) |
| `docs/odak/commercial/Odak_Kompozit_Teklif_IC_CALISMA_NOTLARI.md` | İç kararlar (müşteriye verilmez) |

### Paket klasörleri

| Klasör | Teklif | Öncelik |
|:---|:---|:---:|
| `ai_platform/` | Çapraz AI omurgası (MngLLM / Ollama / embed) | P1 |
| `document_intelligence/` | §4.1 | **P1 — Faz 3 başlangıç** (ürün olgun; gap + AI) |
| `reporting/` | §4.2 | P1 |
| `monitoring/` | §4.3 | P1 |
| `production_operations/` | §4.4 | P1 |
| `package_module/` | §4.6 | P1 |
| `survey_portal/` | §4.5 | **P3 — en son** (barındırma A/B kararsız; şimdilik doküman) |

**Dil:** Kullanıcıyla Türkçe. Kod/yorum İngilizce olabilir.

---

## 2. Dosya rolleri (karıştırma)

| Dosya | Ne zaman güncellenir | Ne yazılır |
|:---|:---|:---|
| **Roadmap.md** | Kapsam, faz, kabul veya major plan değişince | Faz tablosu, bağımlılık, bilinçli dışarıda kalanlar |
| **work.md** | Her anlamlı geliştirme / planlama oturumunda | “Nerede kaldık”, yapılanlar, sıradaki, blocker, commit tablosu |
| **MIGRATION.md** | Dataset, seed, menü patch, deploy adımı oluşunca veya değişince | Dilim satırı + komutlar; test/prod checkbox |
| **Teklif MUSTERI.md** | Yalnızca kullanıcı **açıkça** teklif güncellemesi isterse | Ticari kapsam; teknik implementasyon detayı değil |
| **IC çalışma notları** | İç karar değişince (opsiyonel, talep veya “kaydet”) | Müşteriye gitmeyen notlar |

- `Roadmap.md` = seyrek, karar dokümanı.  
- `work.md` = sık, operasyon günlüğü (Odak’taki `DEVAM.md` benzeri).  
- Script’lerin kendisi `scripts/tests/…` veya `docs/odak/…/scripts/` altında kalır; faz3’te **işletme referansı** tutulur.

---

## 3. “Dökümanları güncelle” / “docs güncelle” denince

Kullanıcı ilgili iş için dokümantasyon güncellemesi istediğinde agent **otomatik olarak**:

1. **Hangi paket?** Konuşma bağlamından veya kullanıcıdan netleştir. Belirsizse sor.  
2. **`{paket}/work.md` güncelle** (zorunlu):
   - Son güncelleme tarihi  
   - Nerede kaldık  
   - Bu oturumda yapılanlar (checklist)  
   - Sıradaki  
   - Blocker  
   - Commit tablosu (hash varsa)  
3. **`{paket}/Roadmap.md` güncelle** (gerekirse):
   - Faz durumu değiştiyse (ör. DI-1 ✅)  
   - Yeni faz / kapsam / kabul / bağımlılık eklendiyse  
   - Sadece “bugün şunu yaptık” için Roadmap’e roman yazma — o `work.md` işi  
4. **Migration var mı?** Aşağıdaki §4.  
5. Kullanıcı istemeden **commit/push yapma**. Doküman güncellemesi ≠ git commit.  
6. İsteğe bağlı: “Doküman güncellendi; commit ister misiniz?” diye sor.

---

## 4. Migration kuralları

Tek dosya: [MIGRATION.md](./MIGRATION.md).

Aşağıdakilerden **biri** oluştuysa dilim satırı ekle / güncelle:

- Yeni veya değişen DG dataset / şema / index  
- Seed, setup, patch script (menü, workspace, şablon, …)  
- Müşteri test/prod’da çalıştırılması gereken komut  
- Deploy edilmesi gereken servis listesi değişimi (`mngui`, `mngdocument`, …)

**Satırda olmalı:** Dilim id · tarih · paket · dataset/şema · script path + örnek komut · deploy servisleri · Test ☐ · Prod ☐ · commit · not.

**Akış hatırlatması (müşteri ortamı):**

```text
git pull → docker deploy → MIGRATION sırası → smoke → (sonra) prod
```

`survey_portal` için deploy/migration satırı **barındırma kararı olmadan** yazılmaz (bilinçli).

---

## 5. Commit ve push

Proje kuralı ile aynı; bu fazda da geçerlidir:

| Kullanıcı derse | Agent yapar |
|:---|:---|
| Sadece kod/docs değiştir | Commit **yok** |
| “kaydet” / durum kaydet | İlgili `work.md` (± Roadmap); commit **yok** (ayrıca istenmedikçe) |
| “commit yap” | Önce özet + mesaj öner; onaydan sonra commit. Conventional Commits (`feat:`, `fix:`, `docs:`, …) |
| “push yap” / “commit ve push” | Açık talep + onay sonrası; `required_permissions` ile network/all |
| Belirsiz | Sor; tahminle push etme |

**Commit’e girmez (uyarma):** `.env`, credential, müşteri PII export, `commercial/output/` PDF build artıkları (gitignore’da olabilir).

**Commit mesajı:** Neden odaklı, kısa; Faz 3 paket adını geçirmek faydalı (`docs(faz3): …`, `feat(reporting): …`).

Commit sonrası ilgili `work.md` commit tablosuna hash not edilebilir (kullanıcı “docs güncelle” demişse veya commit talebiyle birlikte uygunsa).

---

## 6. Lokal çalıştırma: Docker (backend) vs UI

Bu fazda lokal doğrulama şöyle ayrılır:

| Katman | Kim çalıştırır | Agent ne yapabilir |
|:---|:---|:---|
| **Backend servisler** | Lokal **Docker Desktop** | Kullanıcı geliştirme/test bağlamında (veya açıkça “docker’a al / deploy et” dediğinde) ilgili backend imajlarını **otomatik** build/up edebilir (`docker compose`, servis deploy script’leri). Sandbox için tam yetki (`all`) iste. |
| **UI (`Mng.Ui`)** | **Kullanıcı** — Cursor’daki terminalde `npm run dev` | Agent **kullanıcı talebi olmadan** UI deploy etmez (`mngui` docker build/up, UI container restart, prod/test UI deploy **yok**). `npm run dev`’i agent kendisi başlatmaz / öldürmez. |

### UI kuralı (sıkı)

- “Deploy et”, “docker’a al”, “ortama bas” → **yalnızca backend** (aksi açıkça “UI dahil” / “mngui de” denmedikçe).  
- UI değişikliği sonrası: kullanıcıya “UI’yi kendi terminalinde `npm run dev` ile kontrol edin” demek yeterli.  
- Müşteri Odak test/prod’a `mngui` deploy **yalnızca kullanıcı açıkça isterse**.

### “Terminali incele” / “terminale bak”

Kullanıcı **terminali incele**, **terminale bak**, **dev loguna bak**, **npm çıktısına bak** vb. dediğinde:

1. Kasdettiği yer: **bu Cursor oturumundaki kullanıcı terminali** (genelde `npm run dev` çalışan panel) — uzak sunucu SSH terminali değil (aksi belirtilmedikçe).  
2. Agent, terminals klasöründeki ilgili oturum dosyasını okur (`cwd`, `last_command`, aktif komut, stdout/stderr).  
3. UI hata/ayrıntı için önce bu lokal `npm run dev` çıktısına bakılır.

---

## 7. Kodlama ve kapsam disiplini

1. Kullanıcı açıkça implementasyon istemedikçe **sadece konuş / planla**; dosya değiştirme.  
2. “Kodla / implement et / ekle / uygula” → kod.  
3. Teklif kapsamı dışına taşma: Monitoring’de **SIEM yok**; Üretim’de **tam MES yok**; Markdown Sayfa bu teklif kalemi değil.  
4. `survey_portal` geliştirmeyi **en sona** bırak; kullanıcı özellikle istemedikçe P1 paketlerinden önce başlama.  
5. Geliştirme sırası belirsizse → [README.md](./README.md) **Önerilen geliştirme sırası** (**başlangıç: DI**).  
6. Ortak AI omurgası → `ai_platform`; DI/Monitoring/Üretim AI UI detayı kendi paketinde, backend sözleşme `ai_platform`’da.  
7. Fiyat/ödeme müşteri teklifinde boş olabilir; iç nottaki eski tutarı müşteri MD’ye yazma (toplantı kararı).  
8. Lokal backend → Docker Desktop (§6). UI → kullanıcının `npm run dev` terminali; izinsiz UI deploy yok.  
9. Test script’lerini kullanıcı istemeden çalıştırma.  
10. DI baseline için `docs/odak/document_intelligence/` (`DI_PRODUCT_ROADMAP`, `DEVAM`) — Faz 3 DI sıfırdan değil, gap odaklı.  
11. DI yetki/regresyon: `document_intelligence` **DI-T** (T-0/T-1 P0); yetkiyi etkileyen dilimde T-1 yeşil olmadan kapanmış sayma.

---

## 8. Oturum kapanışı (önerilen)

Kullanıcı “kaydet”, “durumu yaz”, “work güncelle” derse veya anlamlı bir dilim bittiyse:

1. `{paket}/work.md` güncel  
2. Gerekirse `Roadmap.md` faz işareti  
3. Gerekirse `MIGRATION.md` satırı  
4. Commit isteyip istemediğini sor  

---

## 9. Hızlı kontrol listesi (her görev öncesi)

- [ ] Bu görev hangi **paket** klasörüne ait?  
- [ ] Önce `Roadmap.md` + `work.md` okundu mu?  
- [ ] Çıktı doküman güncellemesi mi, kod mu, ikisi mi?  
- [ ] Dataset/seed/deploy etkisi → `MIGRATION.md`?  
- [ ] Commit/push için **açık kullanıcı talebi** var mı?  
- [ ] Deploy isteği varsa: backend Docker OK; **UI deploy istenmiş mi?** (yoksa UI’ye dokunma)  
- [ ] “Terminali incele” → Cursor’daki kullanıcı `npm run dev` terminali mi?

---

*Bu dosya Faz 3 agent davranışının kaynağıdır. Çelişkide önce proje `.cursorrules`, sonra bu dosya; teklif kapsamı için müşteri teklif MD’si. Lokal UI/Docker ayrımı için §6 bu dosyada önceliklidir.*
