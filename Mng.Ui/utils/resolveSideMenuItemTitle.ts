export interface ResolveSideMenuTitleOptions {
  locale: string;
  messages: Record<string, unknown>;
  t: (key: string) => string;
}

function isMissingTranslation(value: unknown, translationKey: string): boolean {
  if (!value) return true;
  if (value === translationKey) return true;
  return typeof value === 'string' && value.startsWith('menu.');
}

/**
 * Sidebar NavItem ile aynı mantık: menu.{pageCode}, kök pageCode, object.title, fallback title.
 */
export function resolveSideMenuItemTitle(
  pageCode: string | undefined,
  fallbackTitle: string | undefined,
  options: ResolveSideMenuTitleOptions,
): string {
  const trimmedFallback = (fallbackTitle || '').trim();

  if (!pageCode) {
    return trimmedFallback;
  }

  const { locale, messages, t } = options;
  const localeMessages = (messages[locale] ?? messages) as Record<string, unknown>;
  const menu = localeMessages?.menu as Record<string, unknown> | undefined;

  let menuValue: unknown = null;
  if (menu && menu[pageCode] !== undefined) {
    menuValue = menu[pageCode];
  } else {
    const menuKey = `menu.${pageCode}`;
    menuValue = t(menuKey);
    if (isMissingTranslation(menuValue, menuKey)) {
      menuValue = t(pageCode);
    }
  }

  if (!isMissingTranslation(menuValue, `menu.${pageCode}`)) {
    if (typeof menuValue === 'object' && menuValue !== null && 'title' in menuValue) {
      const objectTitle = String((menuValue as { title?: string }).title || '').trim();
      if (objectTitle) return objectTitle;
    }
    if (typeof menuValue === 'string' && menuValue.trim()) {
      return menuValue.trim();
    }
  }

  if (trimmedFallback && trimmedFallback !== pageCode) {
    return trimmedFallback;
  }

  return trimmedFallback;
}
