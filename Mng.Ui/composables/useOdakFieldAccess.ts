import { computed, type Ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import type { OdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import {
  resolveOdakFieldAccess,
  resolveOdakListColumnAccess,
  type OdakFieldAccess,
} from '@/utils/odakSiparisFieldPolicies';

export function useOdakFieldAccess(
  fieldPolicies: Ref<OdakFieldPoliciesBlob | null | undefined>,
  listKeyToField?: Record<string, string>
) {
  const auth = useAuthStore();
  const userGroups = computed(() => auth.userGroups);

  function accessForField(fieldKey: string, row?: Record<string, unknown> | null): OdakFieldAccess {
    return resolveOdakFieldAccess(fieldKey, userGroups.value, row ?? {}, fieldPolicies.value);
  }

  function accessForListColumn(listKey: string, row?: Record<string, unknown> | null): OdakFieldAccess {
    return resolveOdakListColumnAccess(
      listKey,
      userGroups.value,
      row ?? {},
      fieldPolicies.value,
      listKeyToField
    );
  }

  function canViewField(fieldKey: string, row?: Record<string, unknown> | null): boolean {
    return accessForField(fieldKey, row).visible;
  }

  function canEditField(fieldKey: string, row?: Record<string, unknown> | null): boolean {
    return accessForField(fieldKey, row).editable;
  }

  function canViewListColumn(listKey: string, row?: Record<string, unknown> | null): boolean {
    return accessForListColumn(listKey, row).visible;
  }

  return {
    userGroups,
    accessForField,
    accessForListColumn,
    canViewField,
    canEditField,
    canViewListColumn,
  };
}
