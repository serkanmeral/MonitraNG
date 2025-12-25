# Mng.UI - MonitraNG Frontend Application

MaterialPro admin template tabanlı, modern ve kullanıcı dostu frontend uygulaması.

## 🚀 Teknoloji Stack

- **Nuxt 3** - Vue 3 framework
- **Vuetify 3.7.1** - Material Design component library
- **TypeScript** - Type-safe development
- **Pinia** - State management
- **ApexCharts** - Data visualization
- **VeeValidate** - Form validation

## 📋 Gereksinimler

- Node.js 18+ 
- npm/yarn/pnpm
- MngKeeper API (authentication için)
- MngDataGateway API (data management için)

## 🔧 Kurulum

### 1. Bağımlılıkları Yükleyin

```bash
npm install
# veya
yarn install
# veya
pnpm install
```

### 2. Environment Variables (.env)

Proje root dizininde `.env` dosyası oluşturun:

```env
# MngKeeper API URL (Zorunlu)
KEEPER_URL=https://localhost:5001

# MngReactor/MngDataGateway API URL (Opsiyonel)
SERVER_URL=https://localhost:5011
```

**Not:** `.env` dosyası `.gitignore` içinde olduğu için commit edilmez. `.env.example` dosyasını referans olarak kullanabilirsiniz.

### 3. Development Server

Development server'ı başlatın:

```bash
npm run dev
```

Uygulama şu adreste çalışacak: http://localhost:3000

## 🔐 Authentication

Uygulama MngKeeper API'sine bağlıdır. Login sayfası:

- **Route:** `/auth/login`
- **Endpoint:** `POST /api/auth/token` (MngKeeper)
- **Domain Seçimi:** Birden fazla domain varsa otomatik olarak domain seçimi gösterilir
- **Format:** `domain@username` formatı da desteklenir

## 📁 Proje Yapısı

```
Mng.Ui/
├── components/          # Vue component'leri
│   ├── apps/           # Uygulama-specific component'ler
│   ├── auth/           # Authentication component'leri
│   ├── dashboards/     # Dashboard widget'ları
│   ├── forms/          # Form component'leri
│   ├── lc/             # Layout component'leri
│   └── shared/         # Paylaşılan component'ler
├── pages/              # Nuxt file-based routing
├── stores/             # Pinia store'ları
│   └── auth.ts         # Authentication store
├── services/           # API servis katmanı
│   └── apiService.ts   # API helper functions
├── plugins/            # Nuxt plugins
│   └── auth.client.ts  # Auth initialization
├── middleware/         # Route middleware
│   └── auth.global.js  # Global auth guard
└── docs/               # Dokümantasyon
    └── RoadMap.md      # Geliştirme planı
```

## 🏗️ Build

Production build:

```bash
npm run build
```

Production preview:

```bash
npm run preview
```

## 📚 Dokümantasyon

Detaylı geliştirme planı için: [docs/RoadMap.md](docs/RoadMap.md)

## 🔗 Backend API'ler

### MngKeeper API
- **Base URL:** `KEEPER_URL` environment variable'dan alınır
- **Endpoints:**
  - `POST /api/auth/token` - Login
  - `POST /api/auth/refresh` - Token yenileme
  - `POST /api/auth/revoke` - Logout
  - `GET /api/domain` - Domain listesi
  - `GET /api/user` - User listesi
  - `GET /api/group` - Group listesi

### MngDataGateway API
- **Base URL:** `SERVER_URL` environment variable'dan alınır
- **Endpoints:**
  - `GET /api/datasets` - Dataset listesi
  - `POST /api/datasets` - Dataset oluşturma
  - `GET /api/data/{datasetName}` - Data listesi

## 🐛 Troubleshooting

### CORS Hatası
Backend API'lerde CORS ayarlarını kontrol edin. Frontend `http://localhost:3000` adresinden çalışıyor.

### SSL Certificate Hatası
Development ortamında self-signed certificate kullanıyorsanız, browser'da certificate'i kabul etmeniz gerekebilir.

### Environment Variables Yüklenmiyor
- `.env` dosyasının proje root dizininde olduğundan emin olun
- Nuxt server'ı yeniden başlatın (`npm run dev`)
- `nuxt.config.ts` içinde `runtimeConfig` yapılandırmasını kontrol edin

## 📄 License

Copyright © 2025 iSIM Platform

## 👤 Author

Serkan MERAL - serkan.meral@isimplatform.io
