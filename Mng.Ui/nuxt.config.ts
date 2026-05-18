import { createResolver } from "@nuxt/kit";
import path from "node:path";
import { fileURLToPath } from "node:url";
import vuetify, { transformAssetUrls } from "vite-plugin-vuetify";

const { resolve } = createResolver(import.meta.url);
const __dirname = fileURLToPath(new URL(".", import.meta.url));

// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  ssr: false,
  devtools:{enabled:true},
  css: ["@/assets/css/task-manager.css"],
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
      // Production'da verilmezse '' → Hub store relative '/hub' kullanır (same-origin). Dev'de localhost:5040.
      gatewayUrl: (process.env.GATEWAY_URL && process.env.GATEWAY_URL.trim())
        ? process.env.GATEWAY_URL
        : (process.env.NODE_ENV === 'production' ? '' : 'https://localhost:5040'),
      // Individual service URLs (used if gatewayUrl is not set)
      keeperUrl: process.env.KEEPER_URL || 'https://localhost:5001',
      reactorUrl: process.env.SERVER_URL || process.env.DATAGATEWAY_URL || process.env.REACTOR_URL || 'https://localhost:5010',
      // Boş/verilmediğinde store gatewayUrl + '/hub' kullanır (same-origin). Dev için .env'de HUB_URL=http://localhost:5020
      hubUrl: (process.env.HUB_URL && process.env.HUB_URL.trim()) ? process.env.HUB_URL : '',
      llmUrl: process.env.LLM_URL || 'https://localhost:5030',
      adminUrl: process.env.ADMIN_URL || 'http://localhost:5080',
      // Fallback menu control (default: false - disabled)
      enableFallbackMenu: process.env.ENABLE_FALLBACK_MENU === 'true' || false,
      // GeoServer base URL (harita altlığı, çevrimdışı). Örn. http://localhost:8082
      geoServerBaseUrl: (process.env.GEOSERVER_BASE_URL && process.env.GEOSERVER_BASE_URL.trim()) ? process.env.GEOSERVER_BASE_URL.trim().replace(/\/$/, '') : '',
      // WMTS tile matrix set (EPSG:900913 veya EPSG:3857). Grid subset sınırlıysa ofset ile hizalayın.
      geoServerTileMatrixSet: (process.env.GEOSERVER_TILE_MATRIX_SET && process.env.GEOSERVER_TILE_MATRIX_SET.trim()) ? process.env.GEOSERVER_TILE_MATRIX_SET.trim() : 'EPSG:900913',
      geoServerTileColOffset: process.env.GEOSERVER_TILE_COL_OFFSET ? parseInt(process.env.GEOSERVER_TILE_COL_OFFSET, 10) : 0,
      geoServerTileRowOffset: process.env.GEOSERVER_TILE_ROW_OFFSET ? parseInt(process.env.GEOSERVER_TILE_ROW_OFFSET, 10) : 0,
      // App version (from package.json)
      appVersion: process.env.npm_package_version || '6.0.0'
    },
    // Server-side only (private)
    serverAdminUrl: process.env.SERVER_ADMIN_URL || process.env.ADMIN_URL || 'http://localhost:5080',
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
        { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' },
        { rel: 'alternate icon', type: 'image/svg+xml', href: '/icon-simple.svg' }
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
