# Offline/Local Network Dil Desteği

## Kısa Cevap

✅ **Evet, offline/local network kullanıcıları dil desteğini kullanabilir!**

Çeviri dosyaları **statik JSON dosyaları** olarak kod ile birlikte deploy edilir ve runtime'da internet bağlantısı **GEREKMEZ**.

---

## Nasıl Çalışır?

### 1. Çeviri Dosyaları Statik Dosyalar

Çeviri dosyaları (`tr.json`, `en.json`, `zh.json`, `ar.json`) **kod deposunda (repository)** tutulur ve build time'da JavaScript bundle'ına dahil edilir.

**Yapı:**
```
Mng.Ui/
├── utils/locales/
│   ├── tr.json          # Türkçe çeviriler (kod deposunda)
│   ├── en.json          # İngilizce çeviriler (kod deposunda)
│   ├── zh.json          # Çince çeviriler (kod deposunda)
│   ├── ar.json          # Arapça çeviriler (kod deposunda)
│   └── messages.ts      # Import dosyası
└── plugins/
    └── vuetify.ts       # messages.ts'i import eder
```

### 2. Build Time'da Bundle'a Dahil Edilir

**Build Süreci:**
1. `npm run build` çalıştırıldığında
2. `messages.ts` dosyası `tr.json`, `en.json`, vb. dosyaları import eder
3. Bu dosyalar JavaScript bundle'ına compile edilir
4. Bundle, `dist/` klasörüne çıkar
5. Bundle, sunucuya deploy edilir

**Sonuç:**
- Tüm çeviriler JavaScript bundle'ının içinde
- Runtime'da ek dosya yükleme yok
- Internet bağlantısı GEREKMEZ

### 3. Runtime'da Nasıl Çalışır?

**Kullanıcı tarafında:**
1. Browser, JavaScript bundle'ını yükler (statik dosya)
2. Bundle içinde tüm çeviri dosyaları zaten var
3. `vue-i18n` kütüphanesi çevirileri bundle'dan alır
4. Dil değiştirme anında çalışır (internet gerekmez)

**Kod Örneği:**
```typescript
// plugins/vuetify.ts
import messages from "@/utils/locales/messages"; // Build time'da import edilir

const i18n = createI18n({
  locale: "tr",
  messages: messages, // Tüm çeviriler burada (bundle içinde)
});

// Runtime'da:
const { t } = useI18n();
t('common.save'); // Bundle içinden direkt alınır, internet gerekmez
```

---

## Online Çeviri Araçları (Crowdin, Lokalise vb.)

### Ne İçin Kullanılır?

Online çeviri araçları (Crowdin, Lokalise, Transifex vb.) **SADECE ÇEVİRİ YÖNETİMİ** için kullanılır:

1. **Çeviri Sürecini Kolaylaştırma:**
   - Çevirmenlerin web arayüzünden çeviri yapması
   - Çeviri review süreçleri
   - Çeviri versiyonlama

2. **Çeviri Export:**
   - Online araçtan JSON dosyaları export edilir
   - Export edilen dosyalar kod deposuna commit edilir
   - Build süreci normal şekilde devam eder

### Çalışma Akışı (Online Araç Kullanılırsa):

```
1. Çevirmen → Online Araç (Crowdin/Lokalise) → Çeviri yapar
2. Export → JSON dosyaları export edilir
3. Git Commit → JSON dosyaları kod deposuna commit edilir
4. Build → npm run build (normal süreç)
5. Deploy → Bundle deploy edilir (tüm çeviriler içinde)
6. Kullanıcı → Bundle'ı yükler (offline çalışır)
```

**ÖNEMLİ:** Online araç kullanılsa bile, çeviriler kod deposuna commit edilir ve build time'da bundle'a dahil edilir. Runtime'da online araca bağlanılmaz.

---

## Offline/Local Network Senaryoları

### Senaryo 1: Tam Offline (Internet Yok)

✅ **Çalışır:**
- Uygulama build edilmiş ve deploy edilmiş durumda
- JavaScript bundle içinde tüm çeviriler var
- Kullanıcı dil değiştirebilir
- Tüm mesajlar çevrilmiş görünür

❌ **Çalışmaz:**
- Build sırasında (çeviri dosyaları kod deposunda olmalı)
- Yeni çeviri eklemek için (kod deposuna commit gerekir)

### Senaryo 2: Local Network (Intranet)

✅ **Çalışır:**
- Uygulama local network'teki sunucuda çalışıyor
- JavaScript bundle local network'ten yüklenir
- Tüm çeviriler bundle içinde
- Internet'e çıkmaya gerek yok

### Senaryo 3: Air-Gapped Sistem (Tamamen İzole)

✅ **Çalışır:**
- Sistem tamamen internet'ten izole
- Build, local network'te yapılır
- Deploy, local network'te yapılır
- Çeviriler bundle içinde olduğu için çalışır

---

## Karşılaştırma

### ❌ Runtime API'den Çeviri Yükleme (İnternet Gerekir)

```typescript
// KÖTÜ ÖRNEK (bizim yaklaşımımız değil):
async function loadTranslations(locale: string) {
  const response = await fetch(`https://api.example.com/translations/${locale}.json`);
  return await response.json(); // Internet gerekir!
}
```

### ✅ Build Time'da Bundle'a Dahil Etme (Bizim Yaklaşım)

```typescript
// İYİ ÖRNEK (bizim yaklaşımımız):
import messages from "@/utils/locales/messages"; // Build time'da import

const i18n = createI18n({
  messages: messages // Bundle içinde, internet gerekmez
});
```

---

## Özet

| Özellik | Açıklama |
|---------|----------|
| **Çeviri Dosyaları** | Statik JSON dosyaları (kod deposunda) |
| **Build Süreci** | Çeviriler bundle'a dahil edilir |
| **Runtime** | Bundle içinden çalışır |
| **Internet Bağlantısı** | Runtime'da **GEREKMEZ** |
| **Offline Çalışma** | ✅ **ÇALIŞIR** |
| **Local Network** | ✅ **ÇALIŞIR** |
| **Online Araçlar** | Sadece çeviri yönetimi için (opsiyonel) |

---

## Sonuç

**Kullanıcılarınız için endişelenmenize gerek yok!**

- ✅ Çeviriler kod ile birlikte deploy edilir
- ✅ Runtime'da internet bağlantısı gerekmez
- ✅ Offline/local network'te çalışır
- ✅ Dil değiştirme anında çalışır
- ✅ Performanslı (bundle içinde, ek yükleme yok)

**Online çeviri araçları sadece çeviri sürecini kolaylaştırmak için (opsiyonel). Çeviriler kod deposuna commit edilir ve normal build sürecinden geçer.**

---

## Ek Notlar

### Lazy Loading (Gelecekte)

Eğer çeviri dosyaları çok büyükse (ki genellikle değil), lazy loading yapılabilir:

```typescript
// Lazy loading örneği (şu an gerekli değil):
const messages = {
  tr: () => import('@/utils/locales/tr.json'),
  en: () => import('@/utils/locales/en.json'),
}
```

Bu durumda bile, çeviriler bundle içinde kalır ve runtime'da internet gerekmez (sadece ilk yüklemede küçük bir dosya yüklemesi olur, ama bu da local network'ten yüklenir).

### Çeviri Güncelleme

Yeni çeviri eklemek için:
1. `tr.json`, `en.json`, vb. dosyalarını güncelle
2. Git commit yap
3. Build yap (`npm run build`)
4. Deploy yap
5. Kullanıcılar yeni bundle'ı alır (normal deployment süreci)

**Offline sistemlerde:** Build ve deploy local network'te yapılır, internet gerekmez.
