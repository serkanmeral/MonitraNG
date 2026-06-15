/**
 * Odak Sipariş hub — DG dataset + AF form kodları (MO bagimsiz).
 */
export const ODAK_SIPARIS_CONFIG = {
  packagesDataset: 'odak_is_paketleri',
  packagesFormCode: 'odak-is-paketleri-form',
  linesDataset: 'odak_siparis_kalemleri',
  linesFormCode: 'odak-siparis-kalemleri-form',
  customersDataset: 'odak_musteriler',

  /** MO koprusu — ileride; hub DG kullanir */
  workspaceId: '9f9cc085-81c7-4a92-9fa2-357ad5c654cd',
  packageWorkItemTypeId: 'cb3d5251-8c75-4e2a-9b4f-8df05f91f9d3',
  packagesBoardId: '3a9b74c3-49a5-4d3b-b7e7-cd0f50d4911a',
} as const;

export type OdakPackageStatus = 'open' | 'closed';

export interface OdakPackageRow {
  __dataId?: string;
  dataId?: string;
  packageNo?: string;
  name?: string;
  customerId?: unknown;
  status?: OdakPackageStatus | string;
  closedAt?: string | null;
  beginDate?: string;
  deliveryDate?: string;
  deliveryAddress?: string;
  notes?: string;
  paymentDetail?: string;
  partCount?: number;
  lineCount?: number;
  workItemId?: string;
  workItemKey?: string;
}
