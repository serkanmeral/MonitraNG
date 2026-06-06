import type { MaybeRefOrGetter } from 'vue';
import {
  useSecurityManagementBreadcrumbs,
  type SecurityBreadcrumbTail,
} from '@/composables/useSecurityManagementBreadcrumbs';

export type SiemCenterBreadcrumbTail = SecurityBreadcrumbTail;

export interface UseSiemCenterBreadcrumbsContext {
  tail?: MaybeRefOrGetter<SiemCenterBreadcrumbTail | null | undefined>;
  /** Events, reference vb. alt sayfalar */
  subPage?: boolean;
}

export function useSiemCenterBreadcrumbs(ctx: UseSiemCenterBreadcrumbsContext = {}) {
  return useSecurityManagementBreadcrumbs({
    area: 'siem',
    siemSubPage: ctx.subPage ?? false,
    tail: ctx.tail,
  });
}
