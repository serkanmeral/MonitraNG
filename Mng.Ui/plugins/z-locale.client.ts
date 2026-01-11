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
  console.log('[z-locale Plugin] Plugin starting...');
  
  // Only run on client side
  if (!process.client) {
    console.log('[z-locale Plugin] Not client side, exiting');
    return;
  }

  console.log('[z-locale Plugin] Client side, continuing...');
  console.log('[z-locale Plugin] nuxtApp.vueApp:', nuxtApp.vueApp);
  console.log('[z-locale Plugin] nuxtApp.vueApp.config:', nuxtApp.vueApp.config);
  console.log('[z-locale Plugin] nuxtApp.vueApp.config.globalProperties:', nuxtApp.vueApp.config.globalProperties);

  const localeStore = useLocaleStore()
  console.log('[z-locale Plugin] Locale store retrieved');

  // Initialize locale on app start (from localStorage or browser language)
  localeStore.initializeLocale()
  console.log('[z-locale Plugin] Locale store initialized, current locale:', localeStore.locale);

  // Sync locale store with vue-i18n locale
  // Get i18n instance from nuxtApp (stored by vuetify.ts plugin)
  const i18n = (nuxtApp as any).$i18n || nuxtApp.vueApp.config.globalProperties.$i18n
  console.log('[z-locale Plugin] i18n instance from nuxtApp.$i18n:', (nuxtApp as any).$i18n);
  console.log('[z-locale Plugin] i18n instance from globalProperties:', nuxtApp.vueApp.config.globalProperties.$i18n);
  console.log('[z-locale Plugin] Final i18n instance:', i18n);
  
  if (i18n) {
    console.log('[z-locale Plugin] i18n instance found, setting up locale sync');
    console.log('[z-locale Plugin] Current i18n locale:', i18n.locale);
    
    // Set initial i18n locale from store
    i18n.locale = localeStore.locale;
    console.log('[z-locale Plugin] Initial i18n locale set to:', i18n.locale);
    
    // Watch locale store changes and update i18n locale
    watch(() => localeStore.locale, (newLocale) => {
      console.log('[z-locale Plugin] Locale store changed to:', newLocale, ', updating i18n locale from', i18n.locale);
      i18n.locale = newLocale;
      console.log('[z-locale Plugin] i18n locale updated to:', i18n.locale);
    }, { immediate: false });
    
    console.log('[z-locale Plugin] Watch set up successfully');
  } else {
    console.warn('[z-locale Plugin] i18n instance NOT FOUND in nuxtApp.$i18n or globalProperties');
    console.warn('[z-locale Plugin] Available globalProperties keys:', Object.keys(nuxtApp.vueApp.config.globalProperties));
  }
  
  console.log('[z-locale Plugin] Plugin setup complete');
})
