import { computed, type MaybeRefOrGetter, toValue } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

export interface SiemCenterBreadcrumbTail {
  text: string;
  href?: string;
  disabled?: boolean;
}

export interface SiemCenterBreadcrumbItem {
  text: string;
  href?: string;
  disabled?: boolean;
}

export interface UseSiemCenterBreadcrumbsContext {
  tail?: MaybeRefOrGetter<SiemCenterBreadcrumbTail | null | undefined>;
}

function translateOr(key: string, fallback: string, t: (key: string) => string): string {
  const value = t(key);
  return value && value !== key ? value : fallback;
}

export function useSiemCenterBreadcrumbs(ctx: UseSiemCenterBreadcrumbsContext = {}) {
  const { t } = useAppI18n();

  const breadcrumbs = computed((): SiemCenterBreadcrumbItem[] => {
    const items: SiemCenterBreadcrumbItem[] = [
      {
        text: translateOr('welcome.breadcrumbs.home', 'Ana sayfa', t),
        href: '/',
        disabled: false,
      },
      {
        text: t('siemCenter.breadcrumbRoot'),
        href: '/apps/siem-center/events',
        disabled: false,
      },
    ];

    const tail = toValue(ctx.tail);
    if (tail?.text) {
      items.push({
        text: tail.text,
        href: tail.href ?? '#',
        disabled: tail.disabled ?? true,
      });
    }

    if (items.length > 1) {
      items[items.length - 1].disabled = true;
    }

    return items;
  });

  return { breadcrumbs };
}
