import { ocCreate, ocUpdate, parseSingleDgRecord, parseSingleDgRecordId } from '@/services/operationCoreService';
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
import { personIdFromRow } from '@/utils/odakSiparisPackagePersonnel';

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
    designContactId: personIdFromRow(row.designContactId) || null,
    manufactureContactId: personIdFromRow(row.manufactureContactId) || null,
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

/** Hub-native packages — DG schema maxLength 32, unique legacy key. */
export function generateNewLegacyPackageId(): string {
  return crypto.randomUUID().replace(/-/g, '');
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
  const body = {
    ...formModelToPackagePayload(form),
    legacyPackageId: generateNewLegacyPackageId(),
    lineCount: 0,
  };
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.packagesDataset, body);
  const record = parseSingleDgRecord(created) ?? (created as Record<string, unknown>);
  const id = parseSingleDgRecordId(created) || packageDataId(record) || null;

  const pkgForNotification = {
    ...body,
    ...record,
    __dataId: id ?? record.__dataId ?? record.dataId,
  } as OdakPackageRow;

  void dispatchOdakPackageNotification('PackageCreated', pkgForNotification, {
    actorPersonId: useAuthStore().userInfo?.mng_person_id ?? null,
  });

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
