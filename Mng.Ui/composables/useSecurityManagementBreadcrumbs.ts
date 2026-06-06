import { computed, type MaybeRefOrGetter, toValue } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

export interface SecurityBreadcrumbTail {
  text: string;
  href?: string;
  disabled?: boolean;
}

export interface SecurityBreadcrumbItem {
  text: string;
  href?: string;
  disabled?: boolean;
}

export interface UseSecurityManagementBreadcrumbsContext {
  /** Alarm hub or SIEM hub and sub-pages */
  area: 'alarm' | 'siem';
  /** SIEM sub-pages (events, reference) show panel as intermediate crumb */
  siemSubPage?: boolean;
  tail?: MaybeRefOrGetter<SecurityBreadcrumbTail | null | undefined>;
}

function translateOr(key: string, fallback: string, t: (key: string) => string): string {
  const value = t(key);
  return value && value !== key ? value : fallback;
}

export function useSecurityManagementBreadcrumbs(ctx: UseSecurityManagementBreadcrumbsContext) {
  const { t } = useAppI18n();

  const breadcrumbs = computed((): SecurityBreadcrumbItem[] => {
    const items: SecurityBreadcrumbItem[] = [
      {
        text: translateOr('welcome.breadcrumbs.home', 'Ana sayfa', t),
        href: '/',
        disabled: false,
      },
      {
        text: t('securityManagement.breadcrumbRoot'),
        href: '/apps/siem-center',
        disabled: false,
      },
    ];

    if (ctx.area === 'alarm') {
      items.push({
        text: t('alarmCenter.menuTitle'),
        href: '/apps/alarm-center/alarms',
        disabled: false,
      });
    } else if (ctx.siemSubPage) {
      items.push({
        text: t('siemCenter.dashboard.menuTitle'),
        href: '/apps/siem-center',
        disabled: false,
      });
    }

    const tail = toValue(ctx.tail);
    if (tail?.text) {
      items.push({
        text: tail.text,
        href: tail.href ?? '#',
        disabled: tail.disabled ?? true,
      });
    } else if (ctx.area === 'siem' && !ctx.siemSubPage) {
      items.push({
        text: t('siemCenter.dashboard.menuTitle'),
        href: '/apps/siem-center',
        disabled: true,
      });
    }

    if (items.length > 0) {
      items[items.length - 1].disabled = true;
    }

    return items;
  });

  return { breadcrumbs };
}
