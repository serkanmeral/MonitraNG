// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  devtools: { enabled: true },
  
  devServer: {
    port: 3010
  },
  
  modules: [
    '@nuxt/ui',
    '@pinia/nuxt'
  ],

  typescript: {
    strict: true,
    typeCheck: true
  },

  runtimeConfig: {
    public: {
      // Application Version
      appVersion: process.env.APP_VERSION ?? '1.0.0',
      // MngKeeper API URL (for client-side - browser accessible URLs)
      keeperUrl: process.env.KEEPER_URL ?? 'http://localhost:5001',
      // MngDataGateway API URL (for client-side - browser accessible URLs)
      datagatewayUrl: process.env.DATAGATEWAY_URL ?? 'https://localhost:5010',
      // MngScheduler API URL (for client-side - browser accessible URLs)
      schedulerUrl: process.env.SCHEDULER_URL ?? 'http://localhost:5090',
      // API Gateway URL (if using gateway, set this and leave other URLs empty)
      gatewayUrl: process.env.GATEWAY_URL ?? ''
    },
    // Server-side only (private)
    // For Docker containers, use container hostnames; for local dev, use localhost
    serverKeeperUrl: process.env.SERVER_KEEPER_URL ?? process.env.KEEPER_URL ?? 'http://localhost:5001',
    serverDataGatewayUrl: process.env.SERVER_DATAGATEWAY_URL ?? process.env.DATAGATEWAY_URL ?? 'https://localhost:5010',
    serverSchedulerUrl: process.env.SERVER_SCHEDULER_URL ?? process.env.SCHEDULER_URL ?? 'http://localhost:5090',
    serverHubUrl: process.env.SERVER_HUB_URL ?? process.env.HUB_URL ?? 'http://localhost:5020',
    keycloakBaseUrl: process.env.KEYCLOAK_BASE_URL ?? 'http://localhost:8080',
    keycloakPathPrefix: process.env.KEYCLOAK_PATH_PREFIX ?? '',
    keycloakAdminUser: process.env.KEYCLOAK_ADMIN_USER ?? 'admin',
    keycloakAdminPassword: process.env.KEYCLOAK_ADMIN_PASSWORD ?? 'admin123',
    minioEndpoint: process.env.MINIO_ENDPOINT ?? 'localhost:9090',
    minioAccessKey: process.env.MINIO_ACCESS_KEY ?? 'admin',
    minioSecretKey: process.env.MINIO_SECRET_KEY ?? 'admin123',
    minioUseSSL: process.env.MINIO_USE_SSL === 'true' || false
  },

  app: {
    // Base URL - Nuxt.js handles /domain/ prefix internally
    baseURL: (process.env.BASE_URL || process.env.NUXT_APP_BASE_URL || '/domain/') as string,
    head: {
      title: 'MonitraNG - IoT Monitoring',
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
        { name: 'description', content: 'MonitraNG - IoT Monitoring and Management Platform' }
      ],
      link: [
        { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' },
        { rel: 'alternate icon', type: 'image/svg+xml', href: '/icon-simple.svg' }
      ]
    }
  },

  css: ['~/assets/css/main.css']
})

