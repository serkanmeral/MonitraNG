import { useLocaleStore } from '@/stores/locale'
import { watch } from 'vue'

/**
 * Locale Plugin (z-locale.client.ts - runs after vuetify.ts)
 * 
 * This plugin:
 * 1. Initializes the locale store from localStorage or browser language
 * 2. Watches locale store changes and syncs with vue-i18n locale
 * 
 * IMPORTANT: Plugin name starts with 'z' to ensure it runs AFTER vuetify.ts
 * Nuxt plugins run in alphabetical order, so z-locale.client.ts runs after vuetify.ts.
 */
export default defineNuxtPlugin((nuxtApp) => {
  // Only run on client side
  if (!process.client) {
    return;
  }

  const localeStore = useLocaleStore()

  // Initialize locale on app start (from localStorage or browser language)
  localeStore.initializeLocale()

  // Sync locale store with vue-i18n locale
  // Get i18n instance from nuxtApp (stored by vuetify.ts plugin)
  const i18n = (nuxtApp as any).$i18n || nuxtApp.vueApp.config.globalProperties.$i18n
  
  if (i18n) {
    // Set initial i18n locale from store
    // IMPORTANT: In messages.ts, Arabic is stored as 'ro', not 'ar'
    const initialI18nLocale = localeStore.locale === 'ar' ? 'ro' : localeStore.locale;
    i18n.locale = initialI18nLocale;
    
    // Watch locale store changes and update i18n locale
    watch(() => localeStore.locale, (newLocale) => {
      // Map 'ar' to 'ro' for i18n (because messages.ts uses 'ro' for Arabic)
      const i18nLocale = newLocale === 'ar' ? 'ro' : newLocale;
      i18n.locale = i18nLocale;
    }, { immediate: false });
  } else {
    console.warn('[z-locale Plugin] i18n instance NOT FOUND in nuxtApp.$i18n or globalProperties');
    console.warn('[z-locale Plugin] Available globalProperties keys:', Object.keys(nuxtApp.vueApp.config.globalProperties));
  }
})
