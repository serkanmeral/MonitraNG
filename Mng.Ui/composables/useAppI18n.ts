/**
 * vue-i18n bu projede legacy modda (plugins/vuetify.ts → createI18n, $i18n).
 * Bu modda `useI18n()` setup içinde güvenilir değil (inject / mixin sırası — sayfa takılabilir).
 * Çeviriler için global `$i18n` ile uyumlu `t` sağlar.
 */
export function useAppI18n() {
  const nuxtApp = useNuxtApp();
  const i18n = nuxtApp.vueApp.config.globalProperties.$i18n as
    | { t?: (key: string, ...args: unknown[]) => string; global?: { t?: (key: string, ...args: unknown[]) => string } }
    | undefined;

  function t(key: string, ...args: unknown[]): string {
    if (i18n && typeof i18n.t === 'function') {
      return i18n.t(key, ...args) as string;
    }
    if (i18n?.global && typeof i18n.global.t === 'function') {
      return i18n.global.t(key, ...args) as string;
    }
    return key;
  }

  return { t };
}
