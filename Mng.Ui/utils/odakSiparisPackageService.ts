import { ocCreate, ocUpdate } from '@/services/operationCoreService';
import { ODAK_SIPARIS_CONFIG, type OdakPackageRow } from '@/utils/odakSiparisConfig';
import {
  customerIdFromRow,
  fetchCustomerRelationOptions,
  fetchOdakPackageById,
  packageDataId,
} from '@/utils/odakSiparisService';
import { fromDateInputValue, toDateInputValue } from '@/utils/odakSiparisDateUtils';

export type OdakPackageDialogMode = 'create' | 'edit';

export interface OdakPackageFormModel {
  packageNo: string;
  name: string;
  customerId: string | null;
  status: 'open' | 'closed';
  beginDate: string;
  deliveryDate: string;
  deliveryAddress: string;
  notes: string;
  paymentDetail: string;
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
    status: partial?.status ?? 'open',
    beginDate: partial?.beginDate ?? '',
    deliveryDate: partial?.deliveryDate ?? '',
    deliveryAddress: partial?.deliveryAddress ?? '',
    notes: partial?.notes ?? '',
    paymentDetail: partial?.paymentDetail ?? '',
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
    status: row.status === 'closed' ? 'closed' : 'open',
    beginDate: toDateInputValue(row.beginDate),
    deliveryDate: toDateInputValue(row.deliveryDate),
    deliveryAddress: row.deliveryAddress ?? '',
    notes: row.notes ?? '',
    paymentDetail: row.paymentDetail ?? '',
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
    status: form.status,
    beginDate: fromDateInputValue(form.beginDate),
    deliveryDate: fromDateInputValue(form.deliveryDate),
    deliveryAddress: form.deliveryAddress.trim() || null,
    notes: form.notes.trim() || null,
    paymentDetail: form.paymentDetail.trim() || null,
    partCount: form.partCount,
    stockCount: form.stockCount,
    shippedCount: form.shippedCount,
    poVersion: form.poVersion.trim() || null,
  };
}

export async function loadPackageFormContext(): Promise<{ value: string; title: string }[]> {
  return fetchCustomerRelationOptions();
}

export async function loadPackageForEdit(packageId: string): Promise<OdakPackageRow | null> {
  return fetchOdakPackageById(packageId);
}

export async function createOdakPackage(form: OdakPackageFormModel): Promise<string | null> {
  const body = formModelToPackagePayload(form);
  const created = await ocCreate(ODAK_SIPARIS_CONFIG.packagesDataset, body);
  const o = created as Record<string, unknown>;
  return packageDataId(o) || null;
}

export async function updateOdakPackage(packageId: string, form: OdakPackageFormModel): Promise<void> {
  const body = formModelToPackagePayload(form);
  await ocUpdate(ODAK_SIPARIS_CONFIG.packagesDataset, packageId, body);
}
