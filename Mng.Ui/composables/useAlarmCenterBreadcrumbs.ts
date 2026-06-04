import { computed, type MaybeRefOrGetter, toValue } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

export interface AcCenterBreadcrumbTail {
  text: string;
  href?: string;
  disabled?: boolean;
}

export interface AcCenterBreadcrumbItem {
  text: string;
  href?: string;
  disabled?: boolean;
}

export interface UseAlarmCenterBreadcrumbsContext {
  tail?: MaybeRefOrGetter<AcCenterBreadcrumbTail | null | undefined>;
}

function translateOr(key: string, fallback: string, t: (key: string) => string): string {
  const value = t(key);
  return value && value !== key ? value : fallback;
}

export function useAlarmCenterBreadcrumbs(ctx: UseAlarmCenterBreadcrumbsContext = {}) {
  const { t } = useAppI18n();

  const breadcrumbs = computed((): AcCenterBreadcrumbItem[] => {
    const items: AcCenterBreadcrumbItem[] = [
      {
        text: translateOr('welcome.breadcrumbs.home', 'Ana sayfa', t),
        href: '/',
        disabled: false,
      },
      {
        text: t('alarmCenter.breadcrumbRoot'),
        href: '/apps/alarm-center/alarms',
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
