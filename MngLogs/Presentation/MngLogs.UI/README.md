# MngLogs UI

Nuxt 3 + @nuxt/ui — saha ajanı yerel konsolu (MngEngine Edge UI kalıbı).

## Dev

```powershell
# Agent API ayakta olmalı (127.0.0.1:5092)
cd MngLogs/Presentation/MngLogs.UI
npm install
npm run dev
```

Dev server: `http://localhost:3092` (API için proxy yok; production’da aynı host’ta `/api`).

## Production embed

```powershell
cd MngLogs
.\scripts\build-frontend.ps1
```

Çıktı: `Presentation/MngLogs.Agent/wwwroot` — Agent static + SPA fallback ile sunar.
