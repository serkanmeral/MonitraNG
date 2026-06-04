// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  devtools: { enabled: true },
  ssr: false,

  devServer: {
    port: 3011
  },

  modules: ['@nuxt/ui'],

  typescript: {
    strict: true
  },

  app: {
    baseURL: '/',
    head: {
      title: 'MngEngine - MonitraNG Edge',
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
        { name: 'description', content: 'MngEngine - MonitraNG Edge Data Collector' }
      ],
      link: [{ rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' }]
    }
  },

  nitro: {
    preset: 'static'
  }
})
