import { computed, type MaybeRefOrGetter, toValue } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

export interface AcBreadcrumbTail {
  text: string;
  href?: string;
  disabled?: boolean;
}

export interface AcBreadcrumbItem {
  text: string;
  href?: string;
  disabled?: boolean;
}

export interface UseAutomationCenterBreadcrumbsContext {
  tail?: MaybeRefOrGetter<AcBreadcrumbTail | null | undefined>;
}

function translateOr(key: string, fallback: string, t: (key: string) => string): string {
  const value = t(key);
  return value && value !== key ? value : fallback;
}

export function useAutomationCenterBreadcrumbs(ctx: UseAutomationCenterBreadcrumbsContext = {}) {
  const { t } = useAppI18n();

  const breadcrumbs = computed((): AcBreadcrumbItem[] => {
    const items: AcBreadcrumbItem[] = [
      {
        text: translateOr('welcome.breadcrumbs.home', 'Ana sayfa', t),
        href: '/',
        disabled: false,
      },
      {
        text: t('automationCenter.breadcrumbRoot'),
        href: '/apps/automation-center/workflows',
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
