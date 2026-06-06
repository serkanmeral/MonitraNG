import type { MaybeRefOrGetter } from 'vue';
import {
  useSecurityManagementBreadcrumbs,
  type SecurityBreadcrumbTail,
} from '@/composables/useSecurityManagementBreadcrumbs';

export type AcCenterBreadcrumbTail = SecurityBreadcrumbTail;

export interface UseAlarmCenterBreadcrumbsContext {
  tail?: MaybeRefOrGetter<AcCenterBreadcrumbTail | null | undefined>;
}

export function useAlarmCenterBreadcrumbs(ctx: UseAlarmCenterBreadcrumbsContext = {}) {
  return useSecurityManagementBreadcrumbs({
    area: 'alarm',
    tail: ctx.tail,
  });
}
