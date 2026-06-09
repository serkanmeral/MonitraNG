import type { WidgetCategory } from '@/stores/apps/widget';
import type { WidgetDomain } from '@/types/apps/widgetManifest';

/** Modül kategorileri — @widget_categories (ürün/domain). Tür (card/chart/table) widget.type alanında. */
export interface WidgetModuleDefinition {
  domain: WidgetDomain | string;
  nameTr: string;
  nameEn: string;
  icon: string;
  color: string;
  order: number;
}

export const WIDGET_MODULES: WidgetModuleDefinition[] = [
  {
    domain: 'alarm',
    nameTr: 'Alarm Merkezi',
    nameEn: 'Alarm Center',
    icon: 'mdi-bell-alert',
    color: 'error',
    order: 10,
  },
  {
    domain: 'siem',
    nameTr: 'SIEM',
    nameEn: 'SIEM',
    icon: 'mdi-shield-check',
    color: 'primary',
    order: 20,
  },
  {
    domain: 'operation-core',
    nameTr: 'Operasyon Merkezi',
    nameEn: 'Operation Core',
    icon: 'mdi-clipboard-list',
    color: 'secondary',
    order: 30,
  },
  {
    domain: 'document-intelligence',
    nameTr: 'Doküman Zekası',
    nameEn: 'Document Intelligence',
    icon: 'mdi-file-document-multiple',
    color: 'info',
    order: 40,
  },
];

const DOMAIN_DESCRIPTION_PREFIX = 'domain:';

/** Eski yanlis kategori adlari (tur ile karistirilmis) veya teknik slug → modul domain */
export const LEGACY_WIDGET_CATEGORY_TO_DOMAIN: Record<string, string> = {
  'alarm-kpi': 'alarm',
  'alarm-charts': 'alarm',
  'siem-kpi': 'siem',
  'siem-charts': 'siem',
  'oc-kpi': 'operation-core',
  'oc-work-queues': 'operation-core',
  'di-lists': 'document-intelligence',
  'di-quick-access': 'document-intelligence',
};

export function normalizeWidgetCategoryKey(name: string): string {
  return name.trim().toLowerCase();
}

export function moduleDomainDescription(domain: string): string {
  return `${DOMAIN_DESCRIPTION_PREFIX}${normalizeWidgetCategoryKey(domain)}`;
}

export function getCategoryDomainKey(category: WidgetCategory | null | undefined): string | null {
  if (!category) return null;
  const desc = (category.description ?? '').trim();
  if (desc.toLowerCase().startsWith(DOMAIN_DESCRIPTION_PREFIX)) {
    return desc.slice(DOMAIN_DESCRIPTION_PREFIX.length).trim().toLowerCase();
  }
  const byName = normalizeWidgetCategoryKey(category.name ?? '');
  const mapped = LEGACY_WIDGET_CATEGORY_TO_DOMAIN[byName];
  if (mapped) return mapped;
  const module = WIDGET_MODULES.find((m) => normalizeWidgetCategoryKey(m.domain) === byName);
  if (module) return normalizeWidgetCategoryKey(module.domain);
  return null;
}

export function resolveDomainCategoryName(slugOrDomain: string): string {
  const key = normalizeWidgetCategoryKey(slugOrDomain);
  const mapped = LEGACY_WIDGET_CATEGORY_TO_DOMAIN[key];
  return mapped ? normalizeWidgetCategoryKey(mapped) : key;
}

export function resolveCategoryIdForTemplate(
  options: { domain?: string; category?: string },
  categories: WidgetCategory[],
): string | undefined {
  const domainKeys = new Set<string>();
  if (options.domain?.trim()) {
    domainKeys.add(normalizeWidgetCategoryKey(options.domain));
  }
  if (options.category?.trim()) {
    domainKeys.add(resolveDomainCategoryName(options.category));
  }

  for (const domainKey of domainKeys) {
    const byDesc = categories.find(
      (c) => getCategoryDomainKey(c) === domainKey,
    );
    if (byDesc) return byDesc.__dataId ?? byDesc.dataId;

    const mod = WIDGET_MODULES.find((m) => normalizeWidgetCategoryKey(m.domain) === domainKey);
    if (mod) {
      const byTitle = categories.find(
        (c) =>
          (c.name ?? '').trim() === mod.nameTr ||
          (c.name ?? '').trim() === mod.nameEn,
      );
      if (byTitle) return byTitle.__dataId ?? byTitle.dataId;
    }
  }

  return undefined;
}

export function getWidgetCategoryDisplayName(
  category: WidgetCategory | null | undefined,
  locale = 'tr',
): string {
  if (!category) return '—';

  const domainKey = getCategoryDomainKey(category);
  if (domainKey) {
    const mod = WIDGET_MODULES.find((m) => normalizeWidgetCategoryKey(m.domain) === domainKey);
    if (mod) return locale.startsWith('en') ? mod.nameEn : mod.nameTr;
  }

  const modByTitle = WIDGET_MODULES.find(
    (m) => m.nameTr === category.name || m.nameEn === category.name,
  );
  if (modByTitle) return locale.startsWith('en') ? modByTitle.nameEn : modByTitle.nameTr;

  return category.name?.trim() || '—';
}

export function isWidgetModuleCategory(category: WidgetCategory): boolean {
  if (getCategoryDomainKey(category)) return true;
  return WIDGET_MODULES.some((m) => m.nameTr === category.name || m.nameEn === category.name);
}

/** Eski tur-kategorileri (card/chart/table/banner) — modul degil */
export function isLegacyTypeCategoryName(name: string): boolean {
  return ['card', 'chart', 'table', 'banner', 'map', 'gauge'].includes(
    normalizeWidgetCategoryKey(name),
  );
}

export function widgetModulesForDesigner(locale = 'tr') {
  return WIDGET_MODULES.map((m) => ({
    domain: m.domain,
    label: locale.startsWith('en') ? m.nameEn : m.nameTr,
    icon: m.icon,
    order: m.order,
  }));
}

/** @widget_categories icinden yalnizca urun modullerini (card/chart/table degil) */
export function filterModuleCategories(categories: WidgetCategory[]): WidgetCategory[] {
  return categories
    .filter((cat) => !isLegacyTypeCategoryName(cat.name) && isWidgetModuleCategory(cat))
    .slice()
    .sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
}

export interface ModuleCategorySelectOption {
  value: string;
  title: string;
  subtitle?: string;
  icon?: string;
  color?: string;
}

export function buildModuleCategorySelectOptions(
  categories: WidgetCategory[],
  locale = 'tr',
): ModuleCategorySelectOption[] {
  return filterModuleCategories(categories).map((cat) => ({
    value: cat.__dataId ?? cat.dataId ?? '',
    title: getWidgetCategoryDisplayName(cat, locale),
    subtitle: cat.description,
    icon: cat.icon,
    color: cat.color,
  }));
}
