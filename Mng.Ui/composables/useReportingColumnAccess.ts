import { computed, type Ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import type { OdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import { resolveOdakFieldAccess } from '@/utils/odakSiparisFieldPolicies';

/** Rapor sütun görünürlüğü — Odak fieldPolicies motoru (grup + koşul). Politika yoksa herkese açık. */
export function useReportingColumnAccess(fieldPolicies: Ref<OdakFieldPoliciesBlob>) {
  const auth = useAuthStore();
  const userGroups = computed(() => auth.userGroups);

  function canViewColumn(fieldName: string, row?: Record<string, unknown> | null): boolean {
    return resolveOdakFieldAccess(
      fieldName,
      userGroups.value,
      row ?? {},
      fieldPolicies.value
    ).visible;
  }

  return { userGroups, canViewColumn };
}
