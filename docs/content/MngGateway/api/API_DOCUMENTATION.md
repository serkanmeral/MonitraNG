---
title: "MngGateway API Documentation"
category: "api"
tags: ["gateway", "api", "routing", "ocelot"]
service: "MngGateway"
difficulty: "beginner"
estimated_time: "10 dakika"
language: "tr"
priority: 1
---

# MngGateway API Documentation

## Özet
MngGateway, tüm mikroservislerin tek giriş noktasıdır. Ocelot kullanarak routing yapar.

## Base URL

```
https://api.monitra.local
```

## Routing Yapısı

### Backend Servisler

```
/keeper/*     → MngKeeper:5001
/data/*       → MngDataGateway:5010
/hub/*        → MngHub:5020
/reactor/*    → MngReactor:5003
/engine/*     → MngEngine:5004
/notifier/*   → MngNotifier:5070
/scheduler/*  → MngScheduler:5060
/llm/*        → MngLLM:5050
/admin/*      → MngAdmin:5080
/auth/*       → Keycloak:8080
```

## Authentication

Tüm istekler JWT token gerektirir (auth endpoint'leri hariç).

## Rate Limiting

- **Authenticated:** 100 req/min
- **Unauthenticated:** 30 req/min

## CORS Policy

Sadece whitelist'teki origin'lerden istek kabul edilir.

## İlgili Linkler

- [Architecture Guide](../architecture/ARCHITECTURE_GUIDE.md)

---

**Son Güncelleme:** 16 Ocak 2026
