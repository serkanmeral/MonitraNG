/**
 * Runtime Locale Loader Plugin
 * 
 * This plugin loads locale files from MinIO (via backend API) at runtime.
 * 
 * Priority Strategy (Option B):
 * - MinIO is priority source for all authenticated users (required)
 * - Build-time locale files are used only for login page (unauthenticated users)
 * - For authenticated users: MinIO is required, build-time files are fallback only
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
   * Deep merge two objects (target is modified in place)
   * Priority: Source (MinIO) overrides target (build-time)
   * If source has an object and target has a primitive (string, number, etc.), source overrides completely
   */
  function deepMerge(target: any, source: any): any {
    if (!source || typeof source !== 'object') {
      return target;
    }
    
    if (!target || typeof target !== 'object') {
      return source;
    }
    
    const result = { ...target };
    
    for (const key in source) {
      if (source.hasOwnProperty(key)) {
        if (source[key] && typeof source[key] === 'object' && !Array.isArray(source[key])) {
          // Source has an object value
          const targetValue = target[key];
          
          // If target value is a primitive (string, number, boolean, null, undefined) or array,
          // override completely with source object (MinIO priority)
          if (!targetValue || typeof targetValue !== 'object' || Array.isArray(targetValue)) {
            result[key] = source[key];
          } else {
            // Both are objects, recursively merge them
            result[key] = deepMerge(targetValue, source[key]);
          }
        } else {
          // Override with source value (for primitives and arrays)
          result[key] = source[key];
        }
      }
    }
    
    return result;
  }

  /**
   * Load and merge locale files
   * Priority: MinIO > Build-time files
   * If MinIO has data, it overrides build-time files completely
   * If MinIO has no data, build-time files are used as fallback
   * 
   * @param forceReload If true, skip cache and reload from MinIO
   * @param requireMinIO If true, MinIO is required (for authenticated users). Missing MinIO data will log warnings.
   */
  async function loadRuntimeLocales(forceReload: boolean = false, requireMinIO: boolean = false): Promise<void> {
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

      // Try to load each locale from MinIO (priority: MinIO > Build-time)
      for (const locale of availableLocales) {
        // Special handling for 'ro' (Arabic): Load from MinIO as 'ar' but store in 'ro'
        if (locale === 'ro') {
          // Check cache first (unless force reload)
          if (!forceReload) {
            const cached = getCachedLocale('ar'); // Cache key is 'ar' (MinIO file name)
            if (cached) {
              // MinIO data found in cache - override build-time files completely
              // Deep merge: MinIO data (source) overrides build-time data (target)
              const merged = deepMerge(messages[locale] || {}, cached.data);
              // Force Vue reactivity by creating new object reference
              messages[locale] = { ...merged };
              console.log(`[Locale Loader] Loaded ${locale} (ar) from cache (MinIO priority)`);
              continue;
            }
          }

          // Try to load from MinIO as 'ar' (MinIO stores it as ar.json)
          const minioData = await loadLocaleFromMinIO('ar');
          
          if (minioData) {
            // MinIO data found - override build-time files completely
            // Cache the data with 'ar' key (MinIO file name)
            setCachedLocale('ar', minioData);
            
            // Deep merge: MinIO data (source) overrides build-time data (target)
            const merged = deepMerge(messages[locale] || {}, minioData);
            // Force Vue reactivity by creating new object reference
            messages[locale] = { ...merged };
            
            // Debug: Log merged menu items to verify object structure
            if (merged.menu) {
              const menuKeys = Object.keys(merged.menu).filter(k => 
                typeof merged.menu[k] === 'object' && merged.menu[k] !== null && merged.menu[k].title
              );
              if (menuKeys.length > 0) {
                console.log(`[Locale Loader] Merged menu items with object structure (${locale}):`, menuKeys);
                menuKeys.forEach(key => {
                  console.log(`  - ${key}:`, merged.menu[key]);
                });
              }
            }
            
            // Force Vue-i18n to recognize the change by triggering locale update
            // This ensures components using $t() will re-render
            const currentLocale = i18n.locale;
            i18n.locale = currentLocale; // Trigger reactivity
            
            console.log(`[Locale Loader] Loaded ${locale} (ar) from MinIO (MinIO priority)`);
          } else {
            // MinIO data not found
            if (requireMinIO) {
              // For authenticated users, MinIO is required - log warning
              console.warn(`[Locale Loader] WARNING: ${locale} (ar) not found in MinIO but required for authenticated users. Using build-time files as fallback.`);
            } else {
              // For login page, build-time files are acceptable
              console.log(`[Locale Loader] ${locale} (ar) not found in MinIO, using build-time files (login page fallback)`);
            }
          }
          continue;
        }

        // Check cache first (unless force reload)
        if (!forceReload) {
          const cached = getCachedLocale(locale);
          if (cached) {
            // MinIO data found in cache - override build-time files completely
            // Deep merge: MinIO data (source) overrides build-time data (target)
            const merged = deepMerge(messages[locale] || {}, cached.data);
            // Force Vue reactivity by creating new object reference
            messages[locale] = { ...merged };
            
            // Force Vue-i18n to recognize the change by triggering locale update
            // This ensures components using $t() will re-render
            const currentLocale = i18n.locale;
            i18n.locale = currentLocale; // Trigger reactivity
            
            console.log(`[Locale Loader] Loaded ${locale} from cache (MinIO priority)`);
            continue;
          }
        }

        // Try to load from MinIO
        const minioData = await loadLocaleFromMinIO(locale);
        
        if (minioData) {
          // MinIO data found - override build-time files completely
          // Cache the data
          setCachedLocale(locale, minioData);
          
          // Deep merge: MinIO data (source) overrides build-time data (target)
          const merged = deepMerge(messages[locale] || {}, minioData);
          // Force Vue reactivity by creating new object reference
          messages[locale] = { ...merged };
          
          // Debug: Log merged menu items to verify object structure
          if (merged.menu) {
            const menuKeys = Object.keys(merged.menu).filter(k => 
              typeof merged.menu[k] === 'object' && merged.menu[k] !== null && merged.menu[k].title
            );
            if (menuKeys.length > 0) {
              console.log(`[Locale Loader] Merged menu items with object structure (${locale}):`, menuKeys);
              menuKeys.forEach(key => {
                console.log(`  - ${key}:`, merged.menu[key]);
              });
            }
          }
          
          // Force Vue-i18n to recognize the change by triggering locale update
          // This ensures components using $t() will re-render
          const currentLocale = i18n.locale;
          i18n.locale = currentLocale; // Trigger reactivity
          
          console.log(`[Locale Loader] Loaded ${locale} from MinIO (MinIO priority)`);
        } else {
          // MinIO data not found
          if (requireMinIO) {
            // For authenticated users, MinIO is required - log warning
            console.warn(`[Locale Loader] WARNING: ${locale} not found in MinIO but required for authenticated users. Using build-time files as fallback.`);
          } else {
            // For login page, build-time files are acceptable
            console.log(`[Locale Loader] ${locale} not found in MinIO, using build-time files (login page fallback)`);
          }
        }
      }
    } catch (error) {
      console.error('[Locale Loader] Error loading runtime locales:', error);
      // Don't throw - fallback to build files
    }
  }

  // Expose invalidateCache and reloadLocales functions globally
  // Usage in browser console: 
  //   clearLocaleCache() - Clear all locale caches
  //   clearLocaleCache('tr') - Clear specific locale cache
  //   reloadLocales() - Reload locales from MinIO
  //   checkLocaleCache('tr') - Check what's in cache for a locale
  (nuxtApp as any).$invalidateLocaleCache = invalidateCache;
  (nuxtApp as any).$reloadLocales = async (requireMinIO: boolean = false) => {
    // Invalidate cache first
    invalidateCache();
    // Then reload from MinIO (force reload - skip cache)
    // Check if user is authenticated to determine if MinIO is required
    try {
      const { useAuthStore } = await import('@/stores/auth');
      const authStore = useAuthStore();
      const isRequired = requireMinIO || authStore.isAuthenticated;
      await loadRuntimeLocales(true, isRequired);
    } catch (error) {
      // If auth store is not available, use requireMinIO parameter
      await loadRuntimeLocales(true, requireMinIO);
    }
  };
  
  // Expose functions to window for easy browser console access
  if (process.client && typeof window !== 'undefined') {
    (window as any).clearLocaleCache = invalidateCache;
    (window as any).reloadLocales = (nuxtApp as any).$reloadLocales;
    (window as any).checkLocaleCache = (locale: string) => {
      const cached = getCachedLocale(locale);
      if (cached) {
        console.log(`[Locale Cache] ${locale}:`, cached.data);
        return cached.data;
      } else {
        console.log(`[Locale Cache] ${locale}: No cache found`);
        return null;
      }
    };
    (window as any).checkI18nMessages = (locale: string) => {
      const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
      if (i18n && i18n.messages && i18n.messages[locale]) {
        // Convert Proxy to plain object to avoid infinite loop
        const messages = JSON.parse(JSON.stringify(i18n.messages[locale]));
        console.log(`[i18n Messages] ${locale}:`, messages);
        if (messages.menu) {
          console.log(`[i18n Messages] ${locale}.menu:`, messages.menu);
          // Check specific menu items
          if (messages.menu['apps-automated-forms']) {
            console.log(`[i18n Messages] ${locale}.menu.apps-automated-forms:`, messages.menu['apps-automated-forms']);
          }
          if (messages.menu['automated-forms']) {
            console.log(`[i18n Messages] ${locale}.menu.automated-forms:`, messages.menu['automated-forms']);
          }
        }
        return messages;
      } else {
        console.log(`[i18n Messages] ${locale}: Not found`);
        return null;
      }
    };
  }

  /**
   * Load locales based on current authentication status
   */
  async function loadLocalesForCurrentAuthState() {
    try {
      const { useAuthStore } = await import('@/stores/auth');
      const authStore = useAuthStore();
      
      // Load locales based on authentication status
      if (authStore.isAuthenticated) {
        // For authenticated users: MinIO is required (priority source)
        // Build-time files are fallback only
        console.log('[Locale Loader] User authenticated, loading from MinIO...');
        await loadRuntimeLocales(false, true); // requireMinIO = true
      } else {
        // On login page: Build-time locale files are acceptable
        // MinIO is not required for unauthenticated users
        console.log('[Locale Loader] User not authenticated, using build-time locale files (login page)');
        // Don't load from MinIO for login page - build-time files are sufficient
      }
    } catch (error) {
      console.warn('[Locale Loader] Could not check authentication, using build-time locales:', error);
    }
  }

  // Wait for app to be mounted, then load locales
  nuxtApp.hook('app:mounted', async () => {
    // Small delay to ensure auth store is ready
    await new Promise(resolve => setTimeout(resolve, 500));
    
    // Initial load
    await loadLocalesForCurrentAuthState();
    
    // Watch authentication state changes (for login/logout)
    // This ensures locales are reloaded when user logs in
    try {
      const { useAuthStore } = await import('@/stores/auth');
      const { watch } = await import('vue');
      const authStore = useAuthStore();
      
      // Watch isAuthenticated changes
      watch(
        () => authStore.isAuthenticated,
        async (isAuthenticated, wasAuthenticated) => {
          // Only reload if authentication state actually changed
          if (isAuthenticated !== wasAuthenticated) {
            console.log(`[Locale Loader] Authentication state changed: ${wasAuthenticated} -> ${isAuthenticated}`);
            
            if (isAuthenticated) {
              // User just logged in - invalidate cache and force reload from MinIO
              console.log('[Locale Loader] User logged in, invalidating cache and force reloading from MinIO...');
              invalidateCache();
              // Force reload (skip cache) to ensure fresh data from MinIO
              await loadRuntimeLocales(true, true); // forceReload = true, requireMinIO = true
            } else {
              // User logged out - clear cache (next login will reload)
              console.log('[Locale Loader] User logged out, clearing cache...');
              invalidateCache();
            }
          }
        },
        { immediate: false }
      );
    } catch (error) {
      console.warn('[Locale Loader] Could not watch authentication state:', error);
    }
  });
});
