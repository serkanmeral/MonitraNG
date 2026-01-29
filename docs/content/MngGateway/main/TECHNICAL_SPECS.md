# MngGateway Technical Specs (API Referansı)

Test ekibinin kullandığı birincil API referansı. Gateway routing, kimlik doğrulama ve limitler DOCUMENTATION_STANDARDS §3.6 ile uyumlu biçimde bu dokümanda tutulur.

**Temel bilgiler:**
- **Base URL:** `https://api.monitra.local` (production) veya `https://localhost:5040` (development). Tüm mikroservisler bu tek giriş noktası üzerinden erişilir.
- **Kimlik doğrulama:** Auth gerektiren route’lar JWT (Bearer token) bekler. Token MngKeeper `POST /keeper/api/auth/token` ile alınır. `/keeper/api/auth/*`, `/auth/*` (Keycloak) ve diğer AllowAnonymous route’lar token istemez.
- **SSL/TLS:** Terminasyon Gateway’de yapılır; backend servisler internal HTTP kullanır.

---

## 1. Routing tablosu

Aşağıdaki path önekleri gateway üzerinden ilgili backend servise yönlendirilir.

| Path öneki | Backend servis | Internal adres (ör.) |
|------------|----------------|----------------------|
| `/keeper/` | MngKeeper | `http://mngkeeper:5001` |
| `/data/` | MngDataGateway | `http://mngdatagateway:5010` |
| `/hub/` | MngHub (REST + WebSocket) | `http://mnghub:5020` |
| `/reactor/` | MngReactor | `http://mngreactor:5003` |
| `/engine/` | MngEngine | `http://mngengine:5004` |
| `/notifier/` | MngNotifier | `http://mngnotifier:5070` |
| `/scheduler/` | MngScheduler | `http://mngscheduler:5060` |
| `/llm/` | MngLLM | `http://mngllm:5030` |
| `/admin/` | MngAdmin | `http://mngadmin:5080` |
| `/auth/` | Keycloak | `http://keycloak:8080` |

Örnek erişim:
- Keeper token: `POST https://api.monitra.local/keeper/api/auth/token`
- Data list: `GET https://api.monitra.local/data/api/v1/datasets`
- Hub WebSocket: `wss://api.monitra.local/hub/ws/` veya `wss://api.monitra.local/hub/ws/v1`

---

## 2. Kimlik doğrulama

- **JWT:** Çoğu backend route’u `Authorization: Bearer <access_token>` zorunlu tutar.
- **Keycloak:** `/auth/*` doğrudan Keycloak’a gider; login UI ve OAuth/OIDC akışları burada kullanılır.
- **AllowAnonymous:** Backend’in kendi auth ayarına göre health, version, auth/token, notifier/mail vb. token istemeyebilir.

---

## 3. Rate limiting

| Tip | Limit |
|-----|--------|
| Kimlik doğrulanmış istekler | 100 istek/dakika |
| Kimlik doğrulanmamış istekler | 30 istek/dakika |

Limit aşımında gateway 429 (Too Many Requests) dönebilir.

---

## 4. CORS

Sadece yapılandırılmış (whitelist) origin’lerden gelen istekler kabul edilir. CORS politikası gateway üzerinde merkezi yönetilir; backend servisler CORS başlığı eklemez.

---

## 5. Health

Gateway’in kendi sağlık endpoint’i (varsa) uygulama/yapılandırmada tanımlıdır. Backend sağlık kontrolü için ilgili servisin path’i kullanılır (örn. `GET /data/api/v1/health`, `GET /keeper/health` — path backend’e göre değişir).

---

İlgili doküman: [Architecture Guide](../support/architecture/ARCHITECTURE_GUIDE.md), [Gateway Integration](../support/guides/GATEWAY_INTEGRATION_COMPLETE.md).
