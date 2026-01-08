# MngDomainUI - Domain Management UI

MonitraNG Domain Management için Nuxt 3 + Nuxt UI tabanlı frontend uygulaması.

## 🚀 Teknoloji Stack

- **Nuxt 3** - Vue 3 framework
- **Nuxt UI** - Tailwind CSS tabanlı UI component library
- **TypeScript** - Type-safe development
- **Pinia** - State management

## 📋 Gereksinimler

- Node.js 18+
- npm/yarn/pnpm
- MngKeeper API (domain management için)

## 🔧 Kurulum

### 1. Bağımlılıkları Yükleyin

```bash
npm install
# veya
yarn install
# veya
pnpm install
```

### 2. Environment Variables

`.env` dosyası oluşturun (`.env.example` dosyasını referans alın):

```env
# MngKeeper API URL
KEEPER_URL=https://localhost:5001

# API Gateway URL (if using gateway)
# GATEWAY_URL=https://localhost:5040
```

### 3. Development Server

```bash
npm run dev
```

Uygulama http://localhost:3000 adresinde çalışacaktır.

## 📁 Proje Yapısı

```
MngDomainUI/
├── components/          # Vue component'leri
│   ├── domain/         # Domain-specific components
│   └── common/         # Shared components
├── composables/         # Composable functions
│   ├── useApi.ts       # API client
│   └── useDomain.ts    # Domain operations
├── pages/              # Nuxt file-based routing
├── stores/             # Pinia stores
│   └── domain.ts       # Domain state management
├── types/              # TypeScript type definitions
│   └── domain.ts       # Domain types
└── utils/              # Utility functions
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

## 📚 API Endpoints

Uygulama MngKeeper API'sine bağlıdır:

- `GET /api/domain` - Tüm domainleri listele
- `GET /api/domain/{id}` - Domain detayı
- `GET /api/domain/name/{name}` - İsim ile domain getir
- `POST /api/domain` - Yeni domain oluştur
- `PUT /api/domain/{id}` - Domain güncelle
- `DELETE /api/domain/{id}` - Domain sil

## 🔗 İlgili Dokümantasyon

- [Development Roadmap](../../docs/MngDomainUI/ROADMAP.md)
- [Current Status](../../docs/MngDomainUI/current_status.md)
- [MngKeeper API Documentation](../../docs/MngKeeper/)
- [Domain Creation Pipeline](../../docs/MngKeeper/guides/)

