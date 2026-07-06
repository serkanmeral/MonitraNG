import { useNuxtApp } from '#app';
import { useSideMenuStore, type SideMenuItem } from '@/stores/apps/sideMenu';
import { useAppI18n } from '@/composables/useAppI18n';
import { useLocaleStore } from '@/stores/locale';
import { humanizeAppPath } from '@/utils/welcomeRecentPagesStorage';
import { resolveSideMenuItemTitle } from '@/utils/resolveSideMenuItemTitle';

function findMenuItemForPath(menuStore: ReturnType<typeof useSideMenuStore>, path: string): SideMenuItem | null {
  const exact = menuStore.getMenuItemByRoute(path);
  if (exact) return exact;

  let current = path;
  while (current.includes('/')) {
    current = current.slice(0, current.lastIndexOf('/'));
    if (!current) break;
    const found = menuStore.getMenuItemByRoute(current);
    if (found) return found;
  }
  return null;
}

function readI18nMessages(): Record<string, unknown> {
  const nuxtApp = useNuxtApp();
  const i18n = nuxtApp.vueApp.config.globalProperties.$i18n as
    | { global?: { messages?: Record<string, unknown> | { value?: Record<string, unknown> } }; messages?: Record<string, unknown> | { value?: Record<string, unknown> } }
    | undefined;
  const i18nGlobal = i18n?.global || i18n;
  const raw = i18nGlobal?.messages;
  if (!raw) return {};
  if (typeof raw === 'object' && raw !== null && 'value' in raw) {
    return (raw as { value?: Record<string, unknown> }).value || {};
  }
  return raw as Record<string, unknown>;
}

/** Menü + i18n ile okunabilir sayfa başlığı (Devam et listesi için). */
export function resolveWelcomePageTitle(path: string): string {
  if (!path.startsWith('/apps/')) return path;

  try {
    const menuStore = useSideMenuStore();
    const { t } = useAppI18n();
    const localeStore = useLocaleStore();
    const menuItem = findMenuItemForPath(menuStore, path);

    if (menuItem) {
      const resolved = resolveSideMenuItemTitle(menuItem.pageCode, menuItem.title, {
        locale: localeStore.locale,
        messages: readI18nMessages(),
        t,
      });
      if (resolved && !looksLikePageCode(resolved)) {
        return resolved;
      }
    }
  } catch {
    /* pinia / i18n henüz hazır değil */
  }

  return humanizeAppPath(path);
}

function looksLikePageCode(value: string): boolean {
  const v = value.trim();
  if (!v) return true;
  if (v.includes('.menuTitle') || v.includes('.menuHeader') || v.includes('.menuParent')) return true;
  if (v.startsWith('menu.')) return true;
  return /^[a-z][a-zA-Z0-9]*(\.[a-zA-Z][a-zA-Z0-9]*)+$/.test(v);
}

export function useWelcomePageTitle() {
  return { resolveTitleForPath: resolveWelcomePageTitle };
}
