import { computed, ref, onMounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useSideMenuStore, type SideMenuItem } from '@/stores/apps/sideMenu';
import {
  flattenMenuItems,
  hasMenuAccessToPath,
  hasMenuAccessToPrefix,
} from '@/utils/welcomeMenuUtils';
import {
  welcomeModuleRegistry,
  WELCOME_MODULE_GROUP_KEYS,
  WELCOME_MODULE_GROUP_ORDER,
  type WelcomeModuleDefinition,
  type WelcomeModuleGroupId,
  type WelcomeModuleLink,
} from '@/utils/welcomeModuleRegistry';

export interface WelcomeResolvedModule {
  id: string;
  titleKey: string;
  descriptionKey: string;
  icon: string;
  color: string;
  links: WelcomeModuleLink[];
  isFallback?: boolean;
  fallbackTitle?: string;
  fallbackPageCode?: string;
}

export interface WelcomeModuleGroup {
  groupId: WelcomeModuleGroupId;
  groupKey: string;
  order: number;
  modules: WelcomeResolvedModule[];
}

function extractAppPrefix(path: string): string | null {
  const match = path.match(/^(\/apps\/[^/]+)/);
  return match ? match[1] : null;
}

function pickBestMenuPath(paths: string[]): string {
  const preferred = paths.find((p) => p.includes('/workspace'));
  if (preferred) return preferred;
  const indexPath = paths.find((p) => /\/apps\/[^/]+$/.test(p));
  if (indexPath) return indexPath;
  return [...paths].sort((a, b) => a.length - b.length)[0] || paths[0];
}

function buildFallbackModules(flatItems: SideMenuItem[]): WelcomeResolvedModule[] {
  const registryPrefixes = new Set(welcomeModuleRegistry.map((m) => m.routePrefix));
  const byPrefix = new Map<string, SideMenuItem[]>();

  for (const item of flatItems) {
    const prefix = extractAppPrefix(item.to || '');
    if (!prefix || registryPrefixes.has(prefix)) continue;
    const list = byPrefix.get(prefix) ?? [];
    list.push(item);
    byPrefix.set(prefix, list);
  }

  const fallbacks: WelcomeResolvedModule[] = [];

  for (const [prefix, items] of byPrefix.entries()) {
    const paths = items.map((i) => i.to!).filter(Boolean);
    const primary = items.find((i) => i.to === pickBestMenuPath(paths)) ?? items[0];
    const slug = prefix.replace('/apps/', '');

    fallbacks.push({
      id: `fallback-${slug}`,
      titleKey: 'welcome.modules.fallback.title',
      descriptionKey: 'welcome.modules.fallback.description',
      icon: primary.iconType === 'mdi' ? primary.icon || 'mdi-application-outline' : 'mdi-application-outline',
      color: 'secondary',
      isFallback: true,
      fallbackTitle: primary.title,
      fallbackPageCode: primary.pageCode,
      links: [{ labelKey: 'welcome.modules.fallback.linkOpen', to: pickBestMenuPath(paths) }],
    });
  }

  return fallbacks.sort((a, b) => {
    const aTitle = a.fallbackTitle || a.id;
    const bTitle = b.fallbackTitle || b.id;
    return aTitle.localeCompare(bTitle, 'tr');
  });
}

function resolveRegistryModule(
  def: WelcomeModuleDefinition,
  flatItems: SideMenuItem[],
  isManager: boolean,
): WelcomeResolvedModule | null {
  if (!hasMenuAccessToPrefix(flatItems, def.routePrefix)) {
    return null;
  }

  const visibleLinks = def.links.filter((link) => {
    if (link.requireManager && !isManager) return false;
    return hasMenuAccessToPath(flatItems, link.to);
  });

  if (!visibleLinks.length) {
    const fallbackTo = flatItems.find((i) => i.to?.startsWith(def.routePrefix))?.to;
    if (!fallbackTo) return null;
    visibleLinks.push({ labelKey: 'welcome.modules.fallback.linkOpen', to: fallbackTo });
  }

  return {
    id: def.id,
    titleKey: def.titleKey,
    descriptionKey: def.descriptionKey,
    icon: def.icon,
    color: def.color,
    links: visibleLinks,
  };
}

export function useWelcomePage() {
  const authStore = useAuthStore();
  const menuStore = useSideMenuStore();
  const loading = ref(true);

  onMounted(async () => {
    loading.value = true;
    try {
      await menuStore.loadMenuItems(false);
    } finally {
      loading.value = false;
    }
  });

  const flatVisibleItems = computed(() => flattenMenuItems(menuStore.visibleMenuItems));

  const resolvedModules = computed((): WelcomeResolvedModule[] => {
    const flat = flatVisibleItems.value;
    const isManager = authStore.isManager || authStore.isAdmin;

    const fromRegistry = welcomeModuleRegistry
      .map((def) => resolveRegistryModule(def, flat, isManager))
      .filter((m): m is WelcomeResolvedModule => m !== null);

    const fallbacks = buildFallbackModules(flat);
    return [...fromRegistry, ...fallbacks];
  });

  const moduleGroups = computed((): WelcomeModuleGroup[] => {
    const flat = flatVisibleItems.value;
    const isManager = authStore.isManager || authStore.isAdmin;
    const groups = new Map<WelcomeModuleGroupId, WelcomeResolvedModule[]>();

    for (const def of welcomeModuleRegistry) {
      const mod = resolveRegistryModule(def, flat, isManager);
      if (!mod) continue;
      const list = groups.get(def.group) ?? [];
      list.push(mod);
      groups.set(def.group, list);
    }

    const fallbacks = buildFallbackModules(flat);
    if (fallbacks.length) {
      const list = groups.get('domainApps') ?? [];
      groups.set('domainApps', [...list, ...fallbacks]);
    }

    return (Object.keys(WELCOME_MODULE_GROUP_ORDER) as WelcomeModuleGroupId[])
      .map((groupId) => {
        const modules = (groups.get(groupId) ?? []).sort((a, b) => {
          const defA = welcomeModuleRegistry.find((d) => d.id === a.id);
          const defB = welcomeModuleRegistry.find((d) => d.id === b.id);
          return (defA?.order ?? 999) - (defB?.order ?? 999);
        });
        if (!modules.length) return null;
        return {
          groupId,
          groupKey: WELCOME_MODULE_GROUP_KEYS[groupId],
          order: WELCOME_MODULE_GROUP_ORDER[groupId],
          modules,
        };
      })
      .filter((g): g is WelcomeModuleGroup => g !== null)
      .sort((a, b) => a.order - b.order);
  });

  const hasModules = computed(() => resolvedModules.value.length > 0);

  return {
    loading,
    moduleGroups,
    resolvedModules,
    hasModules,
  };
}
