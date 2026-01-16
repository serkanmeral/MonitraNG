import { createResolver } from "@nuxt/kit";
import vuetify, { transformAssetUrls } from "vite-plugin-vuetify";

const { resolve } = createResolver(import.meta.url);

// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  ssr: false,
  devtools:{enabled:true},
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
      // Gateway URL (if using API Gateway, set this and leave other URLs empty)
      gatewayUrl: process.env.GATEWAY_URL || 'https://localhost:5040',
      // Individual service URLs (used if gatewayUrl is not set)
      keeperUrl: process.env.KEEPER_URL || 'https://localhost:5001',
      reactorUrl: process.env.SERVER_URL || process.env.DATAGATEWAY_URL || process.env.REACTOR_URL || 'https://localhost:5010',
      hubUrl: process.env.HUB_URL || 'http://localhost:5020',
      llmUrl: process.env.LLM_URL || 'https://localhost:5030',
      // Fallback menu control (default: false - disabled)
      enableFallbackMenu: process.env.ENABLE_FALLBACK_MENU === 'true' || false,
      // App version (from package.json)
      appVersion: process.env.npm_package_version || '6.0.0'
    }
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
});
