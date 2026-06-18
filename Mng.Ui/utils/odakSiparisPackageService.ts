import { ocCreate, ocUpdate } from '@/services/operationCoreService';
import { useAuthStore } from '@/stores/auth';
import { ODAK_SIPARIS_CONFIG, type OdakPackageRow } from '@/utils/odakSiparisConfig';
import {
  filterPackagePayloadByFieldAccess,
  packageRecordForPolicyEval,
  type OdakFieldPoliciesBlob,
} from '@/utils/odakSiparisFieldPolicies';
import { loadOdakPackageHubRuntimeSettings } from '@/utils/odakSiparisHubSettingsService';
import {
  diffPackageFields,
  dispatchOdakPackageNotification,
} from '@/utils/odakSiparisNotificationDispatch';
import {
  customerIdFromRow,
  fetchCustomerRelationOptions,
  fetchOdakPackageById,
  packageDataId,
} from '@/utils/odakSiparisService';
import { fromDateInputValue, toDateInputValue } from '@/utils/odakSiparisDateUtils';
import {
  customerContactIdFromRow,
  fetchContactSelectOptions,
} from '@/utils/odakSiparisCustomerContactService';

export type OdakPackageDialogMode = 'create' | 'edit';

export interface OdakPackageFormModel {
  packageNo: string;
  name: string;
  customerId: string | null;
  customerContactId: string | null;
  designContactId: string | null;
  manufactureContactId: string | null;
  status: 'open' | 'closed';
  beginDate: string;
  deliveryDate: string;
  deliveryAddress: string;
  notes: string;
  partCount: number | null;
  stockCount: number | null;
  shippedCount: number | null;
  poVersion: string;
}

export function emptyPackageFormModel(partial?: Partial<OdakPackageFormModel>): OdakPackageFormModel {
  return {
    packageNo: partial?.packageNo ?? '',
    name: partial?.name ?? '',
    customerId: partial?.customerId ?? null,
    customerContactId: partial?.customerContactId ?? null,
    designContactId: partial?.designContactId ?? null,
    manufactureContactId: partial?.manufactureContactId ?? null,
    status: partial?.status ?? 'open',
    beginDate: partial?.beginDate ?? '',
    deliveryDate: partial?.deliveryDate ?? '',
    deliveryAddress: partial?.deliveryAddress ?? '',
    notes: partial?.notes ?? '',
    partCount: partial?.partCount ?? null,
    stockCount: partial?.stockCount ?? null,
    shippedCount: partial?.shippedCount ?? null,
    poVersion: partial?.poVersion ?? '',
  };
}

export function packageRowToFormModel(row: OdakPackageRow): OdakPackageFormModel {
  return emptyPackageFormModel({
    packageNo: row.packageNo ?? '',
    name: row.name ?? '',
    customerId: customerIdFromRow(row) || null,
    customerContactId: customerContactIdFromRow(row.customerContactId) || null,
    designContactId: customerContactIdFromRow(row.designContactId) || null,
    manufactureContactId: customerContactIdFromRow(row.manufactureContactId) || null,
    status: row.status === 'closed' ? 'closed' : 'open',
    beginDate: toDateInputValue(row.beginDate),
    deliveryDate: toDateInputValue(row.deliveryDate),
    deliveryAddress: row.deliveryAddress ?? '',
    notes: row.notes ?? '',
    partCount: row.partCount ?? null,
    stockCount: row.stockCount ?? null,
    shippedCount: row.shippedCount ?? null,
    poVersion: row.poVersion ?? '',
  });
}

export function formModelToPackagePayload(form: OdakPackageFormModel): Record<string, unknown> {
  return {
    packageNo: form.packageNo.trim(),
    name: form.name.trim(),
    customerId: form.customerId || null,
    customerContactId: form.customerContactId || null,
    designContactId: form.designContactId || null,
    manufactureContactId: form.manufactureContactId || null,
    status: form.status,
    beginDate: fromDateInputValue(form.beginDate),
    deliveryDate: fromDateInputValue(form.deliveryDate),
    deliveryAddress: form.deliveryAddress.trim() || null,
    notes: form.notes.trim() || null,
    partCount: form.partCount,
    stockCount: form.stockCount,
    shippedCount: form.shippedCount,
    poVersion: form.poVersion.trim() || null,
  };
}

export async function loadPackageFormContext(): Promise<{ value: string; title: string }[]> {
  return fetchCustomerRelationOptions();
}

export async function loadCustomerContactOptions(customerId: string | null): Promise<{ value: string; title: string }[]> {
  if (!customerId) return [];
  return fetchContactSelectOptions(customerId);
}

export async function loadPackageForEdit(packageId: string): Promise<OdakPackageRow | null> {
  return fetchOdakPackageById(packageId);
}

export async function createOdakPackage(form: OdakPackageFormModel): Promise<string | null> {
  const body = formModelToPackagePayload(form);
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.packagesDataset, body);
  const o = created as Record<string, unknown>;
  const id = packageDataId(o) || null;
  if (id) {
    void dispatchOdakPackageNotification('PackageCreated', o as OdakPackageRow, {
      actorPersonId: useAuthStore().userInfo?.mng_person_id ?? null,
    });
  }
  return id;
}

async function applyFieldPolicyToPayload(
  body: Record<string, unknown>,
  existingRow?: OdakPackageRow | null
): Promise<Record<string, unknown>> {
  let blob: OdakFieldPoliciesBlob = { policiesByField: {} };
  try {
    const settings = await loadOdakPackageHubRuntimeSettings();
    blob = settings.fieldPolicies;
  } catch {
    /* defaults open */
  }
  const auth = useAuthStore();
  const record = existingRow ? packageRecordForPolicyEval(existingRow) : packageRecordForPolicyEval(body as OdakPackageRow);
  return filterPackagePayloadByFieldAccess(body, auth.userGroups, record, blob);
}

export async function updateOdakPackage(
  packageId: string,
  form: OdakPackageFormModel,
  existingRow?: OdakPackageRow | null
): Promise<void> {
  const beforePayload = existingRow
    ? formModelToPackagePayload(packageRowToFormModel(existingRow))
    : {};
  let body = formModelToPackagePayload(form);
  body = await applyFieldPolicyToPayload(body, existingRow ?? null);
  await ocUpdate(ODAK_SIPARIS_CONFIG.packagesDataset, packageId, body);
  const changedFields = diffPackageFields(beforePayload, body);
  void dispatchOdakPackageNotification('PackageUpdated', { ...existingRow, ...body, __dataId: packageId } as OdakPackageRow, {
    changedFields,
    actorPersonId: useAuthStore().userInfo?.mng_person_id ?? null,
  });
}
