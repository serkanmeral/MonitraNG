# MngMobile - Mobil Uygulama Geliştirme Roadmap

**Oluşturulma Tarihi:** 30 Aralık 2025  
**Durum:** 📋 Planlama Aşaması  
**Teknoloji:** React Native (TypeScript) + React Native Paper

---

## 📋 İçindekiler

1. [Genel Bakış](#genel-bakış)
2. [Teknoloji Stack](#teknoloji-stack)
3. [Proje Yapısı](#proje-yapısı)
4. [Geliştirme Aşamaları](#geliştirme-aşamaları)
5. [API Entegrasyonu](#api-entegrasyonu)
6. [UI/UX Tasarım](#uiux-tasarım)
7. [Test Stratejisi](#test-stratejisi)
8. [Yayınlama Süreci](#yayınlama-süreci)
9. [Maliyet Analizi](#maliyet-analizi)
10. [Sonraki Adımlar](#sonraki-adımlar)

---

## 🎯 Genel Bakış

**MngMobile**, MonitraNG ekosisteminin mobil uygulamasıdır. Kullanıcıların cep telefonlarından sistem özelliklerine erişmesini sağlar.

### Amaç

- Kullanıcıların mobil cihazlardan sisteme erişimi
- Web UI (Mng.Ui) ile tutarlı kullanıcı deneyimi
- Material Design prensiplerine uygun arayüz
- Offline mod desteği (gelecekte)
- Push notification desteği (gelecekte)

### Hedef Platformlar

- ✅ **Android** (Google Play Store)
- 🔜 **iOS** (Apple App Store - gelecekte)

---

## 🛠️ Teknoloji Stack

### Core Framework

- **React Native** - Cross-platform mobil framework
- **TypeScript** - Type-safe development
- **React Native Paper** - Material Design 3 component library

### State Management

- **Redux Toolkit** veya **Zustand** - Global state management
- **React Query** (TanStack Query) - Server state management (opsiyonel)

### API & Networking

- **Axios** - HTTP client (mevcut web UI ile tutarlı)
- **@react-native-async-storage/async-storage** - Local storage (token, cache)

### UI Components

- **React Native Paper** - Material Design components
  - Button, Card, TextInput, Dialog, Snackbar, List, Chip, Avatar, vb.
- **React Navigation** - Navigation library
- **React Native Vector Icons** - Icon library (Material Icons)

### Form & Validation

- **React Hook Form** - Form management
- **Yup** veya **Zod** - Schema validation

### Utilities

- **date-fns** - Date manipulation (mevcut web UI ile tutarlı)
- **lodash** - Utility functions (opsiyonel)

---

## 📁 Proje Yapısı

```
MngMobile/
├── src/
│   ├── api/                    # API servisleri
│   │   ├── client.ts           # Axios instance, interceptors
│   │   ├── auth.ts             # Authentication endpoints
│   │   ├── datasets.ts         # Dataset endpoints
│   │   ├── data.ts             # Data CRUD endpoints
│   │   ├── users.ts            # User management endpoints
│   │   └── types.ts            # API response types
│   │
│   ├── stores/                 # State management
│   │   ├── authStore.ts        # Authentication state
│   │   ├── datasetStore.ts     # Dataset state
│   │   ├── dataStore.ts        # Data state
│   │   └── userStore.ts        # User state
│   │
│   ├── screens/                # Ekranlar
│   │   ├── auth/
│   │   │   ├── LoginScreen.tsx
│   │   │   └── RegisterScreen.tsx
│   │   ├── dashboard/
│   │   │   └── DashboardScreen.tsx
│   │   ├── datasets/
│   │   │   ├── DatasetListScreen.tsx
│   │   │   └── DatasetDetailScreen.tsx
│   │   ├── data/
│   │   │   ├── DataListScreen.tsx
│   │   │   ├── DataDetailScreen.tsx
│   │   │   └── DataFormScreen.tsx
│   │   └── profile/
│   │       └── ProfileScreen.tsx
│   │
│   ├── components/             # Reusable components
│   │   ├── common/
│   │   │   ├── Button.tsx
│   │   │   ├── Card.tsx
│   │   │   ├── Input.tsx
│   │   │   ├── LoadingSpinner.tsx
│   │   │   └── ErrorMessage.tsx
│   │   ├── data/
│   │   │   ├── DataTable.tsx
│   │   │   ├── DataForm.tsx
│   │   │   └── FilterPanel.tsx
│   │   └── layout/
│   │       ├── Header.tsx
│   │       ├── Drawer.tsx
│   │       └── TabBar.tsx
│   │
│   ├── navigation/             # Navigation configuration
│   │   ├── AppNavigator.tsx
│   │   ├── AuthNavigator.tsx
│   │   └── types.ts
│   │
│   ├── types/                  # TypeScript type definitions
│   │   ├── api.ts
│   │   ├── dataset.ts
│   │   ├── data.ts
│   │   └── user.ts
│   │
│   ├── utils/                  # Utility functions
│   │   ├── storage.ts          # AsyncStorage helpers
│   │   ├── validation.ts       # Validation helpers
│   │   ├── formatting.ts       # Data formatting
│   │   └── constants.ts         # App constants
│   │
│   ├── theme/                  # Theme configuration
│   │   ├── colors.ts           # Color palette (MaterialPro uyumlu)
│   │   ├── typography.ts       # Typography settings
│   │   └── spacing.ts          # Spacing system
│   │
│   └── hooks/                  # Custom React hooks
│       ├── useAuth.ts
│       ├── useApi.ts
│       └── useStorage.ts
│
├── android/                    # Android native code
├── ios/                        # iOS native code (gelecekte)
├── assets/                     # Images, fonts, vb.
├── package.json
├── tsconfig.json
└── README.md
```

---

## 🚀 Geliştirme Aşamaları

### Phase 1: Temel Altyapı (1-2 hafta)

**Hedef:** Proje kurulumu ve temel yapı

- [ ] React Native proje kurulumu
- [ ] TypeScript konfigürasyonu
- [ ] React Native Paper kurulumu ve tema yapılandırması
- [ ] React Navigation kurulumu
- [ ] Axios instance ve interceptor'lar
- [ ] AsyncStorage yapılandırması
- [ ] Environment variables yapılandırması
- [ ] Temel klasör yapısı oluşturma

**Deliverables:**
- Çalışan React Native projesi
- Temel navigation yapısı
- API client hazır

---

### Phase 2: Authentication (1 hafta)

**Hedef:** Kullanıcı girişi ve token yönetimi

- [ ] Login ekranı (Material Design)
- [ ] Register ekranı (opsiyonel)
- [ ] JWT token yönetimi (storage, refresh)
- [ ] Auth store (Redux/Zustand)
- [ ] Protected route yapısı
- [ ] Auto-logout (token expire)
- [ ] Domain seçimi (multi-tenant)

**API Endpoints:**
- `POST /api/auth/token` (MngKeeper)
- Token refresh mekanizması

**Deliverables:**
- Çalışan login/register akışı
- Token yönetimi
- Protected navigation

---

### Phase 3: Dashboard (1 hafta)

**Hedef:** Ana dashboard ekranı

- [ ] Dashboard ekranı tasarımı
- [ ] İstatistik kartları
- [ ] Hızlı erişim widget'ları
- [ ] Son aktiviteler listesi
- [ ] Pull-to-refresh
- [ ] Loading states

**Deliverables:**
- Çalışan dashboard ekranı
- İstatistik gösterimi

---

### Phase 4: Dataset Yönetimi (2 hafta)

**Hedef:** Dataset listesi ve detay ekranları

- [ ] Dataset listesi ekranı
- [ ] Dataset detay ekranı
- [ ] Dataset arama ve filtreleme
- [ ] Pagination
- [ ] Dataset store
- [ ] Error handling

**API Endpoints:**
- `GET /api/v1/datasets` (MngDataGateway)
- `GET /api/v1/datasets/{name}`

**Deliverables:**
- Dataset listesi ve detay ekranları
- Arama ve filtreleme

---

### Phase 5: Data CRUD İşlemleri (3-4 hafta)

**Hedef:** Dataset verilerinin yönetimi

- [ ] Data listesi ekranı
  - Pagination
  - Sorting
  - Filtering
  - Field selection
  - Relation expansion
- [ ] Data detay ekranı
- [ ] Data oluşturma formu (dinamik, schema-based)
- [ ] Data düzenleme formu
- [ ] Data silme (confirmation dialog)
- [ ] Field type'a göre input component'leri
  - Text, Number, Boolean, DateTime
  - Object (JSON editor)
  - Relation (lookup)
  - Persons, PersonGroups (user/group selector)
- [ ] Form validation
- [ ] Data store

**API Endpoints:**
- `GET /api/v1/data/{datasetName}`
- `GET /api/v1/data/{datasetName}/{id}`
- `POST /api/v1/data/{datasetName}`
- `PUT /api/v1/data/{datasetName}/{id}`
- `DELETE /api/v1/data/{datasetName}/{id}`

**Deliverables:**
- Tam CRUD işlemleri
- Dinamik form sistemi
- Validation

---

### Phase 6: UI/UX İyileştirmeleri (1-2 hafta)

**Hedef:** Kullanıcı deneyimi iyileştirmeleri

- [ ] Loading states (skeleton screens)
- [ ] Error handling ve user feedback
- [ ] Toast notifications (Snackbar)
- [ ] Confirmation dialogs
- [ ] Empty states
- [ ] Pull-to-refresh
- [ ] Infinite scroll (opsiyonel)
- [ ] Animasyonlar
- [ ] Dark mode desteği (opsiyonel)

**Deliverables:**
- Polisajlanmış UI
- İyi kullanıcı deneyimi

---

### Phase 7: Offline Mod (Gelecek - 2-3 hafta)

**Hedef:** İnternet bağlantısı olmadan çalışma

- [ ] Offline data storage (SQLite veya Realm)
- [ ] Sync mekanizması
- [ ] Conflict resolution
- [ ] Offline indicator
- [ ] Queue system (pending operations)

**Deliverables:**
- Offline çalışma desteği
- Sync mekanizması

---

### Phase 8: Push Notifications (Gelecek - 1-2 hafta)

**Hedef:** Bildirim sistemi

- [ ] Firebase Cloud Messaging (FCM) entegrasyonu
- [ ] Notification handling
- [ ] Notification settings
- [ ] Badge management

**Deliverables:**
- Push notification desteği

---

## 🔌 API Entegrasyonu

### Backend Servisler

**MngGateway** (API Gateway)
- Base URL: `https://api.monitra.local` (production)
- Tüm istekler gateway üzerinden

**MngKeeper** (Authentication)
- `POST /api/auth/token` - Login
- `GET /api/user/me` - User info

**MngDataGateway** (Data Management)
- `GET /api/v1/datasets` - Dataset listesi
- `GET /api/v1/datasets/{name}` - Dataset detayı
- `GET /api/v1/data/{datasetName}` - Data listesi
- `POST /api/v1/data/{datasetName}` - Data oluşturma
- `PUT /api/v1/data/{datasetName}/{id}` - Data güncelleme
- `DELETE /api/v1/data/{datasetName}/{id}` - Data silme

### Authentication Flow

1. Kullanıcı login yapar → `POST /api/auth/token`
2. JWT token alınır
3. Token AsyncStorage'a kaydedilir
4. Her API isteğinde token header'a eklenir
5. Token expire olursa refresh token ile yenilenir
6. Refresh token da expire olursa login ekranına yönlendirilir

### Error Handling

- Network errors → Retry mekanizması
- 401 Unauthorized → Token refresh veya login
- 403 Forbidden → Permission error mesajı
- 404 Not Found → Not found mesajı
- 500 Server Error → Generic error mesajı

---

## 🎨 UI/UX Tasarım

### Tasarım Sistemi

**Material Design 3** prensiplerine uygun

**Renk Paleti:**
- MaterialPro web UI'daki renklerle uyumlu
- Primary, Secondary, Error, Success, Warning renkleri
- Dark mode desteği (gelecekte)

**Typography:**
- Material Design typography scale
- Font: System default (Roboto Android'de, San Francisco iOS'ta)

**Spacing:**
- Material Design spacing system (8dp grid)
- Consistent padding ve margin

**Component Library:**
- React Native Paper component'leri
- Material Design 3 uyumlu
- Customizable theme

### Ekran Tasarımları

**Login Screen:**
- Material Design login form
- Domain seçimi (multi-tenant)
- Remember me checkbox
- Forgot password link (gelecekte)

**Dashboard:**
- İstatistik kartları (Card component)
- Hızlı erişim butonları
- Son aktiviteler listesi
- Pull-to-refresh

**Dataset List:**
- List view (React Native Paper List)
- Search bar
- Filter button
- FAB (Floating Action Button) - yeni dataset (opsiyonel)

**Data List:**
- Table view (FlatList)
- Pagination controls
- Sort ve filter options
- FAB - yeni data ekleme

**Data Form:**
- Dynamic form (schema-based)
- Field type'a göre input component'leri
- Validation messages
- Save/Cancel buttons

---

## 🧪 Test Stratejisi

### Test Türleri

**Unit Tests:**
- Utility functions
- Store logic
- API service functions

**Component Tests:**
- React Native Testing Library
- Component rendering
- User interactions

**Integration Tests:**
- API integration
- Navigation flow
- Authentication flow

**E2E Tests:**
- Detox veya Appium
- Tam kullanıcı senaryoları

### Test Araçları

- **Jest** - Unit testing framework
- **React Native Testing Library** - Component testing
- **Detox** - E2E testing (opsiyonel)

---

## 📱 Yayınlama Süreci

### Google Play Store

**1. Developer Hesabı:**
- Google Play Console hesabı oluşturma
- $25 tek seferlik ödeme

**2. Uygulama Hazırlığı:**
- App Bundle (AAB) oluşturma
- Release key ile imzalama
- Version code ve version name

**3. Store Listing:**
- Uygulama adı (50 karakter)
- Kısa açıklama (80 karakter)
- Uzun açıklama (4000 karakter)
- Uygulama ikonu (512x512 px)
- Feature graphic (1024x500 px)
- Ekran görüntüleri (en az 2, en fazla 8)
- Kategori seçimi
- İçerik derecelendirmesi

**4. Gerekli Dokümanlar:**
- Privacy Policy URL (zorunlu)
- Data safety formu

**5. Yayınlama:**
- Internal testing → Closed testing → Open testing → Production
- Staged rollout önerilir (%5 → %50 → %100)

**Süre:** İlk yükleme 1-3 gün (Google incelemesi), güncellemeler birkaç saat

### Apple App Store (Gelecek)

- Apple Developer Program: $99/yıl
- App Store Connect hesabı
- Xcode ile build
- App Store review süreci

---

## 💰 Maliyet Analizi

### Geliştirme Maliyetleri

- **Geliştirme araçları:** Ücretsiz
  - React Native CLI
  - Android Studio
  - VS Code
  - Xcode (macOS'ta, iOS için)

- **Test cihazları:** İsteğe bağlı
  - Emulator kullanılabilir
  - Fiziksel cihaz önerilir (test için)

### Yayınlama Maliyetleri

- **Google Play Developer:** $25 (tek seferlik, ömür boyu)
- **Apple Developer (iOS için):** $99/yıl (gelecekte)

### Ongoing Maliyetler

- **Google Play:** Ücretsiz (komisyon yok)
- **Apple App Store:** %15-30 komisyon (ilk $1M'dan sonra %15)

### Ek Servisler (İsteğe Bağlı)

- **Firebase (Push Notifications):** Ücretsiz tier yeterli
- **Firebase Analytics:** Ücretsiz
- **Firebase Crashlytics:** Ücretsiz
- **CI/CD (GitHub Actions):** Ücretsiz

**Toplam Minimum Maliyet:** $25 (sadece Google Play için)

---

## 📋 Sonraki Adımlar

### Başlangıç İçin Gerekenler

1. **Teknoloji Seçimi Onayı:**
   - ✅ React Native (TypeScript)
   - ✅ React Native Paper

2. **Geliştirme Ortamı Kurulumu:**
   - React Native CLI kurulumu
   - Android Studio kurulumu
   - VS Code extension'ları

3. **Proje Başlatma:**
   - React Native proje oluşturma
   - Temel konfigürasyon
   - Phase 1 başlatma

### Zamanı Geldiğinde Yapılacaklar

1. Bu roadmap'i gözden geçir
2. Geliştirme ortamını kur
3. Phase 1'den başla
4. Her phase'i tamamladıkça işaretle
5. Test ve feedback topla
6. Google Play'e yükle

---

## 📝 Notlar

### MaterialPro Uyumluluğu

- Web UI (Mng.Ui) ile görsel tutarlılık
- Aynı renk paleti ve tasarım prensipleri
- Material Design 3 standardı

### Backend Entegrasyonu

- Mevcut API'ler kullanılacak
- MngGateway üzerinden istekler
- JWT token authentication
- Multi-tenant desteği (domain bazlı)

### Performans

- Lazy loading
- Image optimization
- Caching stratejileri
- Network request optimization

### Güvenlik

- API key'lerin güvenli saklanması
- Certificate pinning (HTTPS)
- Token refresh mekanizması
- Root/jailbreak kontrolü (opsiyonel)

---

## 🔗 İlgili Dokümantasyon

- [Mng.Ui Roadmap](../Mng.Ui/RoadMap.md) - Web UI roadmap
- [MngDataGateway API Documentation](../MngDataGateway/api/) - API dokümantasyonu
- [MngKeeper API Documentation](../MngKeeper/api/README.md) - Authentication API

---

**Hazırlayan:** AI Assistant  
**Tarih:** 30 Aralık 2025  
**Versiyon:** 1.0  
**Durum:** Planlama Aşaması

