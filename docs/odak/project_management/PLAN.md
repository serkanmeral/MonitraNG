# Teslimat Omurgası — Planlama

**Durum:** Uygulama durak noktası — Faz 1–3 + F4-1/F4-2 bitti (manifest **0.26.0**)  
**Tarih:** 2 Eylül 2026 (plan) · **3 Eylül 2026** (durak)  
**Ortam:** Odak test `192.168.20.20` · UI kontrolü lokal `npm run dev` · backend deploy serbest · UI Docker yok  
**Kaynak görüşme:** [ankarabt görüşme notları](../../ankarabt/yazilim-mimarligi-di-ve-proje-yonetimi-gorusme-notlari.md)  
**Oturum özeti:** [current_status.md](./current_status.md)

AnkaraBT şartnamesi **örnek kaynak**tır. Generic omurga kurulur; o ihalenin maddeleri hayata geçirilmez.

---

## 1. Amaç

MonitraNG içinde planı, günlük işi, resmi belgeyi ve görsel kanıtı aynı omurgada tutmak.

Tek cümle:

> Kim neyi neden yaptı, ne zaman sapıldı, hangi kayıt kapanışı ispatlar — tek yerden görünsün.

Bu, yazılım mimarına özel bir toolkit değildir. Teslimat yapan ticari şirketin ortak çalışma düzenidir. İlk alıcılar: proje yöneticisi, iş paketi sahibi, kalite (doküman kontrolü), sponsor.

Zincir:

```text
Şartname / kural (DI) → WBS (Proje) → OC Work Item → teslimat / kanıt (DI)
```

---

## 2. Modül ayrımı

| Katman | Sorumluluk | İlk pakette |
|--------|------------|-------------|
| **Document Intelligence** | Resmi bilgi, tür, sürüm, onay, diyagram, kanıt | Tür, ilişki, onay/baseline (ince), draw.io vb. görsel kayıt |
| **Proje Yönetimi** (yeni yüzey) | WBS, takvim, Gantt, bağımlılık, kilometre taşı, baseline, sapma | Faz 1 runtime: **MngOperations** içinde bounded context; yeni mikroservis yok |
| **OperationCore** | Günlük işin tek kaynağı | WBS bağlanır; görev kopyalanmaz |

İşler OC’de yaşar. Proje ekranı planı ve sapmayı gösterir. DI resmi kaydı tutar.

### 2.1 F1-4 runtime sahibi (karar — 2 Eylül 2026)

Yeni mikroservis yok. Faz 1 planlama motoru **MngOperations** içinde ayrı bounded context olur (`pm_*` dataset’ler, Gantt okuma modeli, WBS → work item rollup olayları).

Gerekçe: kritik bağ WBS–iş’tir; ilerleme olayları zaten OC hattındadır. MngDocument belge, tür ve ilişkinin sahibidir; proje planını yutmaz.

Faz 2+ ağır CPM/kaynak dengeleme gerekirse o zaman ayrı servis yeniden değerlendirilir.

---

## 3. Fazlar

### Faz 1 — Teslimat Omurgası (ilk ticari paket)

Satılabilir ilk ürün. Gantt ve draw.io **bu fazın parçasıdır**.

**Dahil**

1. **Proje katmanı (ince)** — proje, WBS, görev, kilometre taşı, FS bağımlılığı, Gantt, planlanan/gerçekleşen tarih, bir baseline, sapma.
2. **OC bağı** — WBS kalemi → workspace / work item / etiket / sorgu. İlerleme adet değil; ağırlık veya efor ile yuvarlanır.
3. **DI kontrollü kayıt** — proje çalışma alanı; türler (plan, tutanak, karar, teslimat, prosedür, şartname, kanıt); şablon; sürüm; onay/yayın; basit baseline.
4. **Görsel kanıt** — draw.io / SVG / PNG / PDF yükle, önizle, sürümle, WBS veya belgeye bağla. Mermaid sayfada kalır. Editör yazılmaz.
5. **Karar ve değişiklik (hafif)** — genel karar kaydı. Kapsam değişikliği hangi belge / WBS / işi etkiler.
6. **İzlenebilirlik (hafif)** — `belge → WBS → OC işi → kanıt`. “Planı var, işi açık, belgesi onaysız” görünsün.
7. **Durum paketi** — geciken iş, kritik kilometre taşı, eksik onay, baseline sapması. Sponsor görünümü: zaman + kapsam + belge. Maliyet yok.
8. **İki iş paketi (katalog tohumu)** — PMO (plan, tutanak, karar, durum, teslimat listesi) ve kalite ince (prosedür, form, kayıt, revizyon). Kullanıcı “bu işi şu paketten başlat” diyebilsin; App Store değil, kurulan yapı.

**Bu fazda yok:** kaynak dengeleme, bütçe, aşama kapısı motoru, tam RAID, tedarikçi portalı, portföy, okundu-anlaşıldı, yükümlülük motoru, denetim sihirbazı, üçüncü taraf marketplace.

### Faz 2 — Kontrol ve yaygınlaşma

Aynı omurga, yeni birimlere satış.

- Aşama kapısı
- RAID tam
- Kaynak/kapasite (kaba; dengeleme algoritması yok)
- İş paketi bütçesi (ERP değil)
- Okundu-anlaşıldı
- Yükümlülük kaydı (madde → tarih → iş → kanıt)
- Denetim / müşteri paketi
- Toplantı → aksiyon
- Dış paydaş alanı
- Portföy görünümü
- Süreç haritası kütüphanesi (draw.io resmi süreç gerçeği)
- **İç paket kataloğu** — raftan seç, önizle, kur, sürümle (üçüncü taraf yok)

### Faz 3 — Sektör paketleri

Yeni platform değil; katalogdaki iş paketleri (şablon + ilişki sözlüğü).

- Mimari çalışma alanı (C4, ADR, ICD)
- Teklif / şartname yanıt kütüphanesi
- Ürün değişiklik (ECO/ECN) — PLM değil
- Onboarding / yetkinlik
- Müşteri kabul ve kapanış

İsteğe bağlı daha sonra: imzalı üçüncü taraf paket, ücretlendirme (gerçek marketplace). İlk günden kurulmaz.

---

## 4. Bilerek dışarıda

Yapmayacağız; “henüz yok” değil, rakip olmayacağız.

| Alan | Gerekçe |
|------|---------|
| draw.io / Visio / UML / CAD **editörü** | Görseli yönetiriz, çizim motoru yazmayız |
| Tam Microsoft Project | Kaynak dengeleme, kazanılan değer, karmaşık takvim — Gantt’ın kendisi değil |
| ERP, muhasebe, stok, bordro | Kayıt sistemi değiliz |
| Tam HRIS, CRM hunisi | Başka ürün sınıfı |
| PLM / 3D / imalat BOM | Mühendislik kaydı değil, onay ve kanıt katmanıyız |
| BPMN icra motoru | Süreç çizmek ≠ süreç çalıştırmak; Workflow ayrı |
| OC’yi proje ürününün içine yutmak | Yürütme katmanı ayrı kalır |
| Üçüncü taraf marketplace (ilk günden) | Önce iç katalog ve kendi paketler |

---

## 5. Hedef kullanıcı (Faz 1)

| Rol | Birincil araç |
|-----|----------------|
| Proje yöneticisi / PMO | Gantt, WBS, sapma, durum paketi, belge eksiği |
| İş paketi sahibi | Bağlı OC işleri, teslimat / kanıt |
| Kalite | Tür, revizyon, onay, kayıt |
| Sponsor | Okuma: zaman, kilometre taşı, açık risk/eksik belge |
| (Sonraki paket) yazılım mimarı | Aynı omurga + mimari türler ve diyagramlar |

---

## 6. Dataset, seed ve yeni ortam

Faz 1 boyunca çok şema ve seed değişecek. Teste elle bırakılan hiçbir şey kanonik kuruluma girmeden kapanmaz.

**Hedef**

1. **Tek giriş noktası** — `docs/odak/project_management/scripts/install-teslimat-omurgasi.ps1` (manifest: `install/manifest.json`). Yeni ortamda DI + proje katmanı, “şu patch’i de çalıştır” listesi olmadan ayağa kalkar. `seed-document-intelligence-test.ps1` bu installer’a Odak içerik profiliyle bağlanır.
2. **Sürümlenmiş manifest** — Sıralı, idempotent: kategori → `dm_*` / proje şemaları → yamalar → çekirdek seed → isteğe bağlı paket seed.
3. **Geliştirme kuralı** — Ortamdaki her şema alanı ve seed kaydı aynı anda repo JSON + manifest adımına yazılır.
4. **Çekirdek / paket ayrımı** — Çekirdek her ortamda zorunlu (şema). İş paketleri içerik + yapıdır; yeni alan icat etmez. PMO/kalite/mimari paketler çekirdek üzerine basılır.
5. **Doğrulama** — Kurulum sonrası “eksik dataset / eksik seed” kontrolü (ör. `dm_tags` kaçması tekrarlanmasın).

Proje dataset’leri DI bootstrap’ından ayrı unutulmaz; aynı kapıdan kurulur.

---

## 7. Faz 1 iş kırılımı

Sıra bilinçli: önce kurulum disiplini, sonra nesneler, sonra Gantt ve bağ, sonra paket seed.

| ID | İş | Çıktı | Not |
|----|----|--------|-----|
| **F1-0** | Kurulum iskeleti | Manifest, tek bootstrap, create-or-merge şema, doğrulama | **İskelet hazır (0.1.0)** — `install-teslimat-omurgasi.ps1` |
| **F1-1** | DI tür ve ilişki | Belge türleri, ilişki tipleri (`derivedFrom`, `implements`, `dependsOn`, `supersedes`, `conflictsWith`, kanıt/plan bağları) | **Devam** — katalog + kaynak-kaynak link |
| **F1-2** | Onay ve baseline (ince) | Taslak → inceleme → onay/yayın; belge ve proje için basit baseline | Tam CCB yok |
| **F1-3** | Görsel kanıt | draw.io ve görsel uzantılar: yükle, önizle, sürüm, bağla | Editör yok |
| **F1-4** | Proje nesneleri | Proje, WBS, görev, kilometre taşı, FS, tarihler, bir baseline | Runtime: **MngOperations** (`pm_*`); yeni servis yok |
| **F1-5** | Gantt UI | Zaman ekseni, bağımlılık, sapma, kilometre taşı | Planlamanın yüzü |
| **F1-6** | OC bağ ve ilerleme | WBS → OC; olay ile rollup; Gantt’ta iş durumu | Görev kopyalanmaz |
| **F1-7** | İz ve durum | Belge–WBS–iş–kanıt görünümü; sponsor / eksik onay | |
| **F1-8** | Karar kaydı (hafif) | Karar türü + kapsam değişikliği etkisi | RAID tam değil |
| **F1-9** | PMO + kalite iş paketi | Paket manifest’i: klasör ağacı, türler, şablonlar, örnek WBS/diyagram, “bu projeyi paketten başlat” | **Bitti** — katalog tohumu |

Açık tasarım: F1-4 runtime **MngOperations** (2 Eylül 2026). Yeni mikroservis yok.

Faz 1 kırılımının uygulama durumu (3 Eylül 2026): **F1-0 … F1-9 bitti** (Odak smoke + lokal UI). Ayrıntı: [current_status.md](./current_status.md).

---

## 8. Varsayılan teknik ilkeler

- Metadata-first: tür ve ilişki kodda `if (architect)` ile şişmez.
- OC work item yürütmenin tek kaynağıdır.
- Gantt gösterimdir; kaynak dengeleme ve tam CPM Faz 1’de yoktur (FS + tarihler yeter).
- draw.io dosyası birinci sınıf DI kaynağıdır (`type=file` + tür/metadata); özel çizim motoru yoktur.
- UI doğrulaması lokal `npm run dev`; backend test sunucusuna deploy edilebilir; UI image ancak talep ile.
- Her şema/seed değişikliği F1-0 manifest’ine yazılır.
- İş paketi şema değil yapı kurar; tekrar kurulum idempotent olur (atla / güncelle / önizle).

---

## 9. İş paketi kataloğu (yol haritası)

Amaç: proje yöneticisi, mimar, kalite gibi kullanıcılar boş sistemle başlamasın; **o işe özel varsayılan düzeni bir paketten kursun.**

Bu, aynı motorun ticari SKU halidir. Satış cümlesi “Gantt’ımız var” değil, “PMO / mimari / kalite açılışta kendi düzenini kurar.”

### 9.1 Paket ne kurar?

Rol dünyası değil, **iş paketi**:

- DI klasör ağacı, belge türleri, şablonlar, örnek draw.io
- WBS iskeleti, kilometre taşları, karar / tutanak türleri
- OC workspace, pano, form, durumlar
- Hazır ilişkiler: plan → iş → kanıt

Kullanıcı içeriği doldurur; iskeleti her seferinde kurmaz. Aynı kişi birden fazla paket açabilir. Rol yalnızca öneriyi süzer.

### 9.2 Üç kademe

| Kademe | Ne | Durum (3 Eylül 2026) |
|--------|----|----------------------|
| **Tohum (Faz 1 / F1-9)** | PMO ve kalite paketleri; “bu işi şu paketten başlat”; repo seed + isteğe bağlı demo | **Bitti** |
| **İç katalog (Faz 2)** | Raftan seç, önizle, sürüm, skip/update, sök (F2-13); F2 kontrol birimleri | **Bitti** (F2-1…F2-13) |
| **Sektör rafları (Faz 3)** | Mimari, teklif, ECO, onboarding, kabul — aynı katalog, yeni içerik | **Bitti** (F3-1…F3-5) |
| **Paket kapanışı** | F4-1 ince OC workspace; F4-2 sökmede boş DI klasör | **Bitti** |
| **Marketplace (bilinçli sonra)** | Üçüncü taraf, imza, ücret, izolasyon | Katalog dolmadan yok |

Bugünkü kırıntılar (DI pack export/import, OC demo seed) bu modelin parçasıdır; hedef onları tek paket biçiminde birleştirmektir.

### 9.3 Kurallar

- Paket **şema değil, içerik + yapı** basar. Yeni dataset alanı paketle gelmez; F1-0 çekirdeğine girer.
- Kurulum önizlemeli ve idempotent’tir; ortam paket mezarlığı olmaz.
- Çekirdek ortam kurulumu zorunlu, paket müşteri/iş seçimidir.
- Mevcut DI pack (şablon / antet / kapak) genişler: proje + OC + görsel + ilişki aynı manifest’te durur.

---

## 10. Bilinçli olarak ertelenen (Faz 1 notu)

Görüşmede geçen, ilk pakette **yok** sayılanlar:

- DOCX/PDF içerik araması ve şartname maddesi çıkarımı
- Yapay zekâ asistanı
- Kritik yol motoru ve otomatik zamanlama
- Kaynak, maliyet, portföy
- Genel DOCX/PDF şartname maddesi çıkarımı (NLP) — ayrı ürün kararı; AnkaraBT ihalesini teslim etmek için açılmaz
- Üçüncü taraf marketplace (imza, ücret, izolasyon)

`docs/ankarabt/` altındaki teknik şartname ve personel nitelikleri **örnek kaynak**tır. Generic omurgayı tasarlarken kullanıldı; o şartnamenin maddeleri parse edilmez, WBS/yükümlülük olarak doldurulmaz, ihale hayata geçirilmez.

---

## 11. Açık sorular

1. Proje katmanının runtime sahibi: **MngOperations** (Faz 1, 2 Eylül 2026). Yeni servis yok.
2. WBS–OC bağının ilk sürümü: tek work item mi, yoksa etiket/sorgu da mi?
3. İlerleme formülü: efor, süre, manuel ağırlık — hangisi varsayılan?
4. Proje çalışma alanı DI klasörü mü, yoksa ayrı “project hub” mu?
5. draw.io önizleme: sunucu tarafı render mi, tarayıcı/iframe mi?
6. Yetki modeli: DI klasör yetkisi + OC workspace yetkisi + proje rolü nasıl katmanlanır?
7. İş paketi kimliği: paket bir kez ortama mı kurulur, yoksa her yeni proje örneklenir mi?
8. Paket sürümü yükseltince mevcut proje yapıları nasıl evrilir?

---

## 12. Sonraki adım

**Durak (3 Eylül 2026):** Paket omurgası F4-2 ile kapandı. Manifest 0.26.0. Yeni somut dilim yok; öncelik kullanıcıdan.

Biten kademeler: F1-0…F1-9, F2-1…F2-13, F3-1…F3-5, F4-1, F4-2. Oturum notu: [current_status.md](./current_status.md).

Sırada (bilinçli sonra, seçilince): marketplace; genel NLP/şartname parser; paketten tam OC kural/SLA/dashboard.

Tarihsel not — plan onayından sonraki ilk iş **F1-0 + F1-4 sahiplik kararı**ydı; ikisi de uygulandı.

O sırada onaylananlar (artık uygulandı):

- Faz 1 kapsamı (Gantt ve draw.io içeride, editör/tam Project dışarıda)
- Seed/manifest disiplini
- Kodlamaya F1-0 ile başlamak
