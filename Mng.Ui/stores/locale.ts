import { defineStore } from 'pinia'

export type SupportedLocale = 'tr' | 'en' | 'fr' | 'ar' | 'zh'

const LOCALE_STORAGE_KEY = 'monitrang_locale'
const DEFAULT_LOCALE: SupportedLocale = 'tr'
const FALLBACK_LOCALE: SupportedLocale = 'en'

interface LocaleState {
  locale: SupportedLocale
  availableLocales: SupportedLocale[]
  isLoading: boolean
}

export const useLocaleStore = defineStore('locale', {
  state: (): LocaleState => ({
    locale: DEFAULT_LOCALE,
    availableLocales: ['tr', 'en', 'fr', 'ar', 'zh'],
    isLoading: false
  }),

  getters: {
    currentLocale: (state): SupportedLocale => state.locale,
    isTurkish: (state): boolean => state.locale === 'tr',
    isEnglish: (state): boolean => state.locale === 'en',
    isRTL: (state): boolean => state.locale === 'ar',
    localeName: (state): string => {
      const names: Record<SupportedLocale, string> = {
        tr: 'Türkçe',
        en: 'English',
        fr: 'Français',
        ar: 'العربية',
        zh: '中文'
      }
      return names[state.locale]
    }
  },

  actions: {
    /**
     * Initialize locale from localStorage or browser language
     */
    initializeLocale() {
      if (process.client) {
        // 1. Check localStorage
        const savedLocale = localStorage.getItem(LOCALE_STORAGE_KEY) as SupportedLocale | null
        if (savedLocale && this.availableLocales.includes(savedLocale)) {
          this.setLocale(savedLocale, false) // Don't save to localStorage again
          return
        }

        // 2. Check browser language
        const browserLang = navigator.language.split('-')[0] as SupportedLocale
        if (this.availableLocales.includes(browserLang)) {
          this.setLocale(browserLang, true)
          return
        }

        // 3. Default to Turkish
        this.setLocale(DEFAULT_LOCALE, true)
      }
    },

    /**
     * Set locale and save to localStorage
     * Note: i18n locale will be updated by the plugin
     */
    setLocale(locale: SupportedLocale, saveToStorage: boolean = true) {
      console.log('[Locale Store] setLocale called:', locale, 'saveToStorage:', saveToStorage);
      
      if (!this.availableLocales.includes(locale)) {
        console.warn(`[Locale Store] Locale ${locale} is not supported, falling back to ${DEFAULT_LOCALE}`)
        locale = DEFAULT_LOCALE
      }

      const oldLocale = this.locale;
      this.locale = locale
      console.log('[Locale Store] Locale state updated:', oldLocale, '->', this.locale);

      // Save to localStorage
      if (process.client && saveToStorage) {
        localStorage.setItem(LOCALE_STORAGE_KEY, locale)
        console.log('[Locale Store] Saved to localStorage:', locale);
      }
    },

    /**
     * Toggle between Turkish and English (quick switch)
     */
    toggleLocale() {
      const newLocale: SupportedLocale = this.locale === 'tr' ? 'en' : 'tr'
      this.setLocale(newLocale)
    }
  }
})
