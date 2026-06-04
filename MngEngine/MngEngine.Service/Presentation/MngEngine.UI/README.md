# MngEngine UI

MngEngine için Nuxt 3 tabanlı minimal frontend. Config string girişi, sync durumu ve toplama durumunu gösterir.

## Geliştirme

```bash
npm install
npm run dev
```

Geliştirme sunucusu http://localhost:3011 adresinde çalışır. API çağrıları için MngEngine.Api'nin çalışıyor olması gerekir.

## Build

```bash
npm run generate
```

Çıktı `.output/public` dizinine yazılır.

## Backend ile Entegrasyon

Frontend'i MngEngine.Api ile serve etmek için:

```powershell
cd MngEngine.Service/scripts
./build-frontend.ps1
```

Bu script Nuxt'u generate eder ve çıktıyı `MngEngine.Api/wwwroot` dizinine kopyalar. API çalıştırıldığında http://localhost:11100 adresinden hem API hem UI erişilebilir.
