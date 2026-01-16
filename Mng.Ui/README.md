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

#### Gateway Kullanımı (Önerilen - Production için)

```env
# API Gateway URL (Tüm servisler için merkezi erişim)
GATEWAY_URL=https://localhost:5040

# Not: Gateway kullanıldığında diğer URL'ler kullanılmaz
```

#### Direkt Servis Erişimi (Development için)

```env
# MngKeeper API URL
KEEPER_URL=https://localhost:5001

# MngDataGateway API URL
DATAGATEWAY_URL=https://localhost:5010
# veya eski isim (geriye dönük uyumluluk)
SERVER_URL=https://localhost:5010

# MngHub API URL (SignalR için)
HUB_URL=http://localhost:5020
```

**Önemli Notlar:**
- `GATEWAY_URL` tanımlı ise, diğer URL'ler (`KEEPER_URL`, `DATAGATEWAY_URL`, `HUB_URL`) göz ardı edilir
- Gateway kullanımı production için önerilir (merkezi yönetim, SSL termination, rate limiting)
- Development için direkt servis erişimi de mümkündür
- `.env` dosyası `.gitignore` içinde olduğu için commit edilmez

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

### API Gateway Kullanımı (Önerilen)

Tüm backend servislere API Gateway üzerinden erişilir:

- **Gateway URL:** `GATEWAY_URL` environment variable'dan alınır (örn: `https://localhost:5040`)
- **Keeper Endpoints:** `{GATEWAY_URL}/keeper/api/*`
- **DataGateway Endpoints:** `{GATEWAY_URL}/data/api/*`
- **Hub Endpoints:** `{GATEWAY_URL}/hub/ws/*` (SignalR WebSocket)

### Direkt Servis Erişimi (Development)

Gateway kullanılmadığında, her servis için ayrı URL tanımlanabilir:

#### MngKeeper API
- **Base URL:** `KEEPER_URL` environment variable'dan alınır (varsayılan: `https://localhost:5001`)
- **Endpoints:**
  - `POST /api/auth/token` - Login
  - `POST /api/auth/refresh` - Token yenileme
  - `POST /api/auth/revoke` - Logout
  - `GET /api/domain` - Domain listesi
  - `GET /api/user` - User listesi
  - `GET /api/group` - Group listesi

#### MngDataGateway API
- **Base URL:** `DATAGATEWAY_URL` veya `SERVER_URL` environment variable'dan alınır (varsayılan: `https://localhost:5010`)
- **Endpoints:**
  - `GET /api/datasets` - Dataset listesi
  - `POST /api/datasets` - Dataset oluşturma
  - `GET /api/data/{datasetName}` - Data listesi

#### MngHub API (SignalR)
- **Base URL:** `HUB_URL` environment variable'dan alınır (varsayılan: `http://localhost:5020`)
- **Endpoints:**
  - `GET /ws` - SignalR WebSocket connection
  - `GET /health` - Health check

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

Copyright © 2025 MonitraNG

## 👤 Author

Serkan MERAL - serkan.meral@isimplatform.io
