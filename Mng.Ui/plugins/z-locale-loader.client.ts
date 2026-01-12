/**
 * Runtime Locale Loader Plugin
 * 
 * This plugin loads locale files from MinIO (via backend API) at runtime.
 * Falls back to build-time locale files if MinIO is unavailable.
 * 
 * Plugin name starts with 'z-' to ensure it runs after vuetify.ts (which sets up i18n)
 */

export default defineNuxtPlugin((nuxtApp) => {
  // Only run on client side
  if (process.server) {
    return;
  }

  const CACHE_PREFIX = 'locale_cache_';
  const CACHE_TTL = 60 * 60 * 1000; // 1 hour in milliseconds

  /**
   * Get cached locale data from localStorage
   */
  function getCachedLocale(locale: string): { data: any; timestamp: number } | null {
    if (!process.client) return null;

    try {
      const cacheKey = `${CACHE_PREFIX}${locale}`;
      const cached = localStorage.getItem(cacheKey);
      if (!cached) return null;

      const parsed = JSON.parse(cached);
      const now = Date.now();

      // Check if cache is still valid (within TTL)
      if (now - parsed.timestamp < CACHE_TTL) {
        return parsed;
      }

      // Cache expired, remove it
      localStorage.removeItem(cacheKey);
      return null;
    } catch (error) {
      console.warn('[Locale Loader] Failed to read cache:', error);
      return null;
    }
  }

  /**
   * Save locale data to localStorage cache
   */
  function setCachedLocale(locale: string, data: any): void {
    if (!process.client) return;

    try {
      const cacheKey = `${CACHE_PREFIX}${locale}`;
      const cacheData = {
        data,
        timestamp: Date.now(),
      };
      localStorage.setItem(cacheKey, JSON.stringify(cacheData));
    } catch (error) {
      console.warn('[Locale Loader] Failed to save cache:', error);
    }
  }

  /**
   * Invalidate cache for a locale (force reload from MinIO)
   */
  function invalidateCache(locale?: string): void {
    if (!process.client) return;

    try {
      if (locale) {
        const cacheKey = `${CACHE_PREFIX}${locale}`;
        localStorage.removeItem(cacheKey);
      } else {
        // Invalidate all locale caches
        const keys = Object.keys(localStorage);
        keys.forEach(key => {
          if (key.startsWith(CACHE_PREFIX)) {
            localStorage.removeItem(key);
          }
        });
      }
    } catch (error) {
      console.warn('[Locale Loader] Failed to invalidate cache:', error);
    }
  }

  /**
   * Load locale from MinIO via backend API
   */
  async function loadLocaleFromMinIO(locale: string): Promise<any | null> {
    try {
      // Use fetchFromMngKeeper to call the backend API
      const { fetchFromMngKeeper } = await import('@/services/apiService');
      const localeData = await fetchFromMngKeeper(`/system/locales/${locale}`, 'GET');
      return localeData;
    } catch (error: any) {
      // 404 means file doesn't exist in MinIO, which is OK (fallback to build files)
      if (error.message?.includes('404') || error.statusCode === 404) {
        console.info(`[Locale Loader] Locale file ${locale}.json not found in MinIO, using build files`);
        return null;
      }
      
      // Network errors, authentication errors, etc. - log but don't fail
      console.warn(`[Locale Loader] Failed to load ${locale} from MinIO:`, error.message || error);
      return null;
    }
  }

  /**
   * Load and merge locale files
   */
  async function loadRuntimeLocales(): Promise<void> {
    try {
      // Get i18n instance (vue-i18n legacy mode)
      const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
      if (!i18n) {
        console.warn('[Locale Loader] i18n instance not found, skipping runtime locale loading');
        return;
      }

      // Get current locale
      const currentLocale = i18n.locale || 'en';
      
      // Get all available locales from i18n messages (legacy mode - messages is a plain object)
      const messages = i18n.messages || {};
      const availableLocales = Object.keys(messages);

      // Try to load each locale from MinIO
      for (const locale of availableLocales) {
        // Skip 'ro' (it's actually 'ar' - Arabic)
        if (locale === 'ro') continue;

        // Check cache first
        const cached = getCachedLocale(locale);
        if (cached) {
          // Merge cached data into i18n messages (deep merge to preserve existing keys)
          // In legacy mode, messages is a plain object, we can directly assign
          messages[locale] = { ...messages[locale], ...cached.data };
          continue;
        }

        // Try to load from MinIO
        const minioData = await loadLocaleFromMinIO(locale);
        
        if (minioData) {
          // Cache the data
          setCachedLocale(locale, minioData);
          
          // Merge into i18n messages (deep merge to preserve existing keys)
          // In legacy mode, messages is a plain object, we can directly assign
          messages[locale] = { ...messages[locale], ...minioData };
        }
      }
    } catch (error) {
      console.error('[Locale Loader] Error loading runtime locales:', error);
      // Don't throw - fallback to build files
    }
  }

  // Expose invalidateCache function globally for use in locale editor
  (nuxtApp as any).$invalidateLocaleCache = invalidateCache;

  // Wait for app to be mounted, then load locales
  nuxtApp.hook('app:mounted', async () => {
    // Small delay to ensure auth store is ready and user is authenticated
    await new Promise(resolve => setTimeout(resolve, 1000));
    
    // Check if user is authenticated before loading from MinIO
    try {
      const { useAuthStore } = await import('@/stores/auth');
      const authStore = useAuthStore();
      
      if (!authStore.isAuthenticated) {
        return;
      }
    } catch (error) {
      console.warn('[Locale Loader] Could not check authentication, proceeding anyway:', error);
    }
    
    // Load locales from MinIO
    await loadRuntimeLocales();
  });
});
