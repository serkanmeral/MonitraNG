import { createResolver } from "@nuxt/kit";
import path from "node:path";
import { fileURLToPath } from "node:url";
import vuetify, { transformAssetUrls } from "vite-plugin-vuetify";

const { resolve } = createResolver(import.meta.url);
const __dirname = fileURLToPath(new URL(".", import.meta.url));

/** Dev varsayılan host = Odak PROD; .env → ODAK_HOST veya GATEWAY_URL ile override edilir. Test için .env.odak.test.example. */
const ODAK_HOST = process.env.ODAK_HOST?.trim() || "192.168.20.8";

function resolveGatewayUrl(): string {
  if (process.env.GATEWAY_URL?.trim()) return process.env.GATEWAY_URL.trim();
  return process.env.NODE_ENV === "production" ? "" : `http://${ODAK_HOST}:5040`;
}

/** GATEWAY_URL host'undan türet veya dev'de Odak; açık env her zaman öncelikli. */
function resolveBackendServiceUrl(
  envKeys: string[],
  port: number,
  localDefault = `http://localhost:${port}`
): string {
  for (const key of envKeys) {
    const val = process.env[key]?.trim();
    if (val) return val;
  }
  const gateway = resolveGatewayUrl();
  if (gateway) {
    try {
      const u = new URL(gateway);
      u.port = String(port);
      return u.origin;
    } catch {
      // ignore
    }
  }
  if (process.env.NODE_ENV !== "production") {
    return `http://${ODAK_HOST}:${port}`;
  }
  return localDefault;
}

const gatewayUrl = resolveGatewayUrl();
const schedulerUrl = resolveBackendServiceUrl(["SERVER_SCHEDULER_URL", "SCHEDULER_URL"], 5090);
const adminUrl = resolveBackendServiceUrl(["SERVER_ADMIN_URL", "ADMIN_URL"], 5080);
const logCollectorUrl = resolveBackendServiceUrl(
  ["SERVER_LOGCOLLECTOR_URL", "LOGCOLLECTOR_URL", "MNGLOGCOLLECTOR_URL"],
  5091
);

// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  ssr: false,
  experimental: {
    // No routeRules are defined; disabling the unused app manifest avoids Nuxt's
    // duplicate internal manifest-route-rule registration during dev/HMR.
    appManifest: false,
  },
  devtools:{enabled:true},
  css: ["@/assets/css/task-manager.css", "@/assets/css/operation-core.css"],
  //css: ["@/assets/main.scss"], // vuetify ships precompiled css, no need to import sass
  typescript: {
    shim: false,
  },

  vite: {
    // @ts-ignore
    // curently this will lead to a type error, but hopefully will be fixed soon #justBetaThings
    ssr: {
      noExternal: ["vuetify"], // add the vuetify vite plugin
    },
  },
  runtimeConfig: {
    public: {
      // Gateway URL (if using API Gateway, set this and leave other URLs empty).
      // Production'da verilmezse '' → Hub store relative '/hub' kullanır (same-origin).
      // Dev varsayılan: Odak sunucu gateway (HTTP). Yerel stack için .env → GATEWAY_URL=https://localhost:5040
      gatewayUrl,
      // Individual service URLs (used if gatewayUrl is not set)
      keeperUrl: process.env.KEEPER_URL || 'https://localhost:5001',
      reactorUrl: process.env.SERVER_URL || process.env.DATAGATEWAY_URL || process.env.REACTOR_URL || 'https://localhost:5010',
      // Boş/verilmediğinde store gatewayUrl + '/hub' kullanır (same-origin). Dev için .env'de HUB_URL=http://localhost:5020
      hubUrl: (process.env.HUB_URL && process.env.HUB_URL.trim()) ? process.env.HUB_URL : '',
      llmUrl: process.env.LLM_URL || 'https://localhost:5030',
      adminUrl,
      schedulerUrl,
      // LAN collector URL for agent package download links (e.g. http://192.168.20.20:5091)
      logCollectorUrl,
      enableFallbackMenu: process.env.ENABLE_FALLBACK_MENU === 'true' || false,
      // GeoServer base URL (harita altlığı, çevrimdışı). Örn. http://localhost:8082
      geoServerBaseUrl: (process.env.GEOSERVER_BASE_URL && process.env.GEOSERVER_BASE_URL.trim()) ? process.env.GEOSERVER_BASE_URL.trim().replace(/\/$/, '') : '',
      // WMTS tile matrix set (EPSG:900913 veya EPSG:3857). Grid subset sınırlıysa ofset ile hizalayın.
      geoServerTileMatrixSet: (process.env.GEOSERVER_TILE_MATRIX_SET && process.env.GEOSERVER_TILE_MATRIX_SET.trim()) ? process.env.GEOSERVER_TILE_MATRIX_SET.trim() : 'EPSG:900913',
      geoServerTileColOffset: process.env.GEOSERVER_TILE_COL_OFFSET ? parseInt(process.env.GEOSERVER_TILE_COL_OFFSET, 10) : 0,
      geoServerTileRowOffset: process.env.GEOSERVER_TILE_ROW_OFFSET ? parseInt(process.env.GEOSERVER_TILE_ROW_OFFSET, 10) : 0,
      // App version (from package.json)
      appVersion: process.env.npm_package_version || '6.0.0',
      // Telegram bot username (without @) for deep-link bind
      telegramBotUsername: (process.env.NUXT_PUBLIC_TELEGRAM_BOT_USERNAME && process.env.NUXT_PUBLIC_TELEGRAM_BOT_USERNAME.trim())
        ? process.env.NUXT_PUBLIC_TELEGRAM_BOT_USERNAME.trim().replace(/^@/, '')
        : 'MonitraNGBot',
    },
    // Server-side only (private)
    serverAdminUrl: adminUrl,
    serverSchedulerUrl: schedulerUrl,
    /** MngLogCollector base URL (e.g. http://192.168.20.8:5091) — not via Keeper gateway. */
    serverLogCollectorUrl: logCollectorUrl,
    /** Optional; when collector Ingest:ApiKey is set, BFF forwards this header. */
    logCollectorIngestApiKey:
      process.env.MNGLOGCOLLECTOR_INGEST_API_KEY ||
      process.env.LOGCOLLECTOR_INGEST_API_KEY ||
      "",
  },
  build: { transpile: ["vuetify"] },
  modules: [
    "@pinia/nuxt",
    async (options, nuxt) => {
      nuxt.hooks.hook("vite:extendConfig", (config: any) =>
        // @ts-ignore
        config.plugins.push(
          vuetify({
            styles: { configFile: resolve("/assets/scss/variables.scss") },
          })
        )
      );
    },
  ],

  app: {
    head: {
      title: "Monitra NG",
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
        { name: 'description', content: 'MonitraNG - IoT Monitoring and Management Platform' }
      ],
      link: [
        { rel: 'icon', type: 'image/png', sizes: '32x32', href: '/favicon-32.png' },
        { rel: 'icon', type: 'image/png', sizes: '16x16', href: '/favicon-16.png' },
        { rel: 'icon', type: 'image/x-icon', href: '/favicon.ico' },
        { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' },
        { rel: 'apple-touch-icon', sizes: '180x180', href: '/apple-touch-icon.png' }
      ]
    },
    // Base URL ayarı (port numarasını korumak için)
    // Not: Bu build time'da belirlenir, runtime'da değiştirilemez
    // Port numarası browser'dan alınır, bu yüzden boş bırakıyoruz
    baseURL: process.env.BASE_URL || '/',
  },

  nitro: {
    serveStatic: true,
  },

  sourcemap: { server: false, client: false },

  // hooks: {
  //   "vite:extendConfig": (config: any) => {
  //     config.plugins.push(
  //       vuetify({
  //         styles: { configFile: resolve("/assets/scss/variables.scss") },
  //       })
  //     );
  //   },
  // },
  devServerHandlers: [],

  compatibilityDate: "2024-09-06",

  hooks: {
    'pages:extend'(pages) {
      // Organizasyon sayfaları açıkça eklenir (bazı ortamlarda 404 sorununu önlemek için)
      if (pages.findIndex(p => p.path === '/apps/monitoring/organization') === -1) {
        const orgPagePath = path.join(__dirname, 'pages', 'apps', 'monitoring', 'organization', 'index.vue');
        pages.push({
          name: 'apps-monitoring-organization',
          path: '/apps/monitoring/organization',
          file: orgPagePath.split(path.sep).join('/'),
        });
      }
      if (pages.findIndex(p => p.path === '/apps/organization') === -1) {
        const redirectPath = path.join(__dirname, 'pages', 'apps', 'organization', 'index.vue');
        pages.push({
          name: 'apps-organization',
          path: '/apps/organization',
          file: redirectPath.split(path.sep).join('/'),
        });
      }
      // Sohbet odası: tek dosya pages/apps/chat-room.vue (tireli klasör+index bazı ortamlarda 404)
      if (pages.findIndex((p) => p.path === '/apps/chat-room') === -1) {
        const chatRoomPagePath = path.join(__dirname, 'pages', 'apps', 'chat-room.vue');
        pages.push({
          name: 'apps-chat-room',
          path: '/apps/chat-room',
          file: chatRoomPagePath.split(path.sep).join('/'),
        });
      }
    },
  },
});
