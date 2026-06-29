import { type Ref } from 'vue';
import type { OdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import { ODAK_PACKAGE_LIST_KEY_TO_FIELD } from '@/utils/odakSiparisPackageListSettings';
import { packageRecordForPolicyEval } from '@/utils/odakSiparisFieldPolicies';
import type { OdakPackageRow } from '@/utils/odakSiparisConfig';
import { useOdakFieldAccess } from '@/composables/useOdakFieldAccess';

export function useOdakPackageFieldAccess(fieldPolicies: Ref<OdakFieldPoliciesBlob | null | undefined>) {
  const access = useOdakFieldAccess(fieldPolicies, ODAK_PACKAGE_LIST_KEY_TO_FIELD);

  function canViewField(fieldKey: string, row?: OdakPackageRow | null): boolean {
    const record = row ? packageRecordForPolicyEval(row) : {};
    return access.canViewField(fieldKey, record);
  }

  function canEditField(fieldKey: string, row?: OdakPackageRow | null): boolean {
    const record = row ? packageRecordForPolicyEval(row) : {};
    return access.canEditField(fieldKey, record);
  }

  function canViewListColumn(listKey: string, row?: OdakPackageRow | null): boolean {
    const record = row ? packageRecordForPolicyEval(row) : {};
    return access.canViewListColumn(listKey, record);
  }

  return {
    userGroups: access.userGroups,
    canViewField,
    canEditField,
    canViewListColumn,
    accessForField: access.accessForField,
    accessForListColumn: access.accessForListColumn,
  };
}
