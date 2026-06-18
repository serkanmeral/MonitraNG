/**
 * Odak Sipariş hub — DG dataset + AF form kodları (MO bagimsiz).
 */
export const ODAK_SIPARIS_CONFIG = {
  packagesDataset: 'odak_is_paketleri',
  packagesFormCode: 'odak-is-paketleri-form',
  linesDataset: 'odak_siparis_kalemleri',
  linesFormCode: 'odak-siparis-kalemleri-form',
  customersDataset: 'odak_musteriler',
  customersFormCode: 'odak-musteriler-form',
  customerContactsDataset: 'odak_musteri_kisileri',
  ncrDataset: 'odak_ncr',
  capaDataset: 'odak_capa',
  shipmentsDataset: 'odak_sevkiyatlar',
  shipmentLinesDataset: 'odak_sevkiyat_kalemleri',
  hubSettingsDataset: 'odak_siparis_hub_ayarlari',
  notificationPoliciesDataset: 'odak_siparis_notification_policies',

  /** MO koprusu — ileride; hub DG kullanir */
  workspaceId: '9f9cc085-81c7-4a92-9fa2-357ad5c654cd',
  packageWorkItemTypeId: 'cb3d5251-8c75-4e2a-9b4f-8df05f91f9d3',
  packagesBoardId: '3a9b74c3-49a5-4d3b-b7e7-cd0f50d4911a',
} as const;

/** Vuetify: expand ikonu sola — headers dizisinin ilk elemani olmali (OcAdmin deseni). */
export const ODAK_DATA_TABLE_EXPAND_COLUMN = {
  title: '',
  key: 'data-table-expand',
  sortable: false,
  width: 48,
} as const;

/** Vuetify: eylemler sütunu — odak-sub-list-table ile; son sütun olmalı. */
export const ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER = {
  sortable: false,
  align: 'end' as const,
} as const;

/** Odak alt liste tabloları — OdakSiparisSubListScroll içinde kullanılır. */
export const ODAK_SUB_LIST_TABLE_CLASS = 'odak-sub-list-table';

export type OdakPackageStatus = 'open' | 'closed';

export interface OdakPackageRow {
  __dataId?: string;
  dataId?: string;
  packageNo?: string;
  name?: string;
  customerId?: unknown;
  customerContactId?: unknown;
  designContactId?: unknown;
  manufactureContactId?: unknown;
  status?: OdakPackageStatus | string;
  closedAt?: string | null;
  beginDate?: string;
  deliveryDate?: string;
  deliveryAddress?: string;
  notes?: string;
  paymentDetail?: string;
  partCount?: number;
  stockCount?: number;
  shippedCount?: number;
  lineCount?: number;
  legacyResponsibleId?: string;
  legacyDesignResponsibleId?: string;
  legacyManufactureResponsibleId?: string;
  legacyContactId?: string;
  legacyCreatedAt?: string;
  legacyCreatedBy?: string;
  legacyUpdatedAt?: string;
  legacyUpdatedBy?: string;
  __createdAt?: string;
  __updatedAt?: string;
  __createdBy?: string;
  __updatedBy?: string;
  workItemId?: string;
  workItemKey?: string;
  poVersion?: string;
  poDocument?: OdakFileFieldValue | null;
  poDocumentPath?: string | null;
  poStorageKey?: string | null;
}

/** DG file alani — kayit sonrasi metadata veya upload oncesi base64 payload. */
export type OdakFileFieldValue =
  | string
  | {
      path?: string;
      file_name?: string;
      file_ext?: string;
      file_size?: number;
      upload_time?: string;
      content?: string;
      originalFileName?: string;
    };

export const ODAK_PO_FILE_FIELD = {
  fieldType: 'file',
  name: 'poDocument',
  isArray: false,
  fileOptions: {
    maxSize: 25 * 1024 * 1024,
    allowedExtensions: ['.pdf'],
  },
} as const;

export type OdakCustomerSektor = 'havacilik' | 'savunma' | 'diger';

export interface OdakCustomerRow {
  __dataId?: string;
  dataId?: string;
  kod?: string;
  unvan?: string;
  sektor?: OdakCustomerSektor | string;
  ulke?: string;
  aktif?: boolean;
  notlar?: string;
  legacyFirmId?: string;
}

export interface OdakCustomerContactRow {
  __dataId?: string;
  dataId?: string;
  parentCustomerId?: unknown;
  ad?: string;
  email?: string;
  telefon?: string;
  gorevUnvani?: string;
  birincilKisi?: boolean;
  aktif?: boolean;
  legacyContactId?: string;
}

export const ODAK_CUSTOMER_SEKTOR_OPTIONS = [
  { value: 'havacilik', title: 'Havacılık' },
  { value: 'savunma', title: 'Savunma' },
  { value: 'diger', title: 'Diğer' },
] as const;

export const ODAK_LINE_UNIT_OPTIONS = [
  { value: 'adet', title: 'Adet' },
  { value: 'takim', title: 'Takım' },
  { value: 'kg', title: 'kg' },
  { value: 'm', title: 'm' },
  { value: 'm2', title: 'm²' },
  { value: 'set', title: 'Set' },
] as const;

export const ODAK_LINE_CURRENCY_OPTIONS = [
  { value: 'TRY', title: 'TRY' },
  { value: 'USD', title: 'USD' },
  { value: 'EUR', title: 'EUR' },
  { value: 'GBP', title: 'GBP' },
] as const;

export interface OdakLineRow {
  __dataId?: string;
  dataId?: string;
  parentPackageId?: unknown;
  lineNo?: number;
  customerProjectNo?: string;
  customerPoNo?: string;
  customerPoItemNo?: number | string;
  /** Müşteri SAS (satın alma) kalem numarası — PO kalem no'dan ayrı. */
  sasItemNo?: string;
  customerJobNo?: string;
  poItemRevNo?: string;
  description?: string;
  productId?: unknown;
  quantity?: number;
  unit?: string;
  shippedQuantity?: number;
  qualityReqs?: string;
  isFai?: boolean;
  isFaiComplete?: boolean;
  /** Kalem termin tarihi (iş paketi termininden bağımsız). */
  deliveryDate?: string;
  shipmentDate?: string;
  shipmentAddress?: string;
  unitCost?: number;
  totalCost?: number;
  currency?: string;
  legacyLineId?: string;
}

export type OdakNcrStatus =
  | 'MRB Bekleniyor'
  | 'Rework/Kontrol Bekleniyor'
  | 'Değerlendirme Bekleniyor'
  | 'DF No Bekleniyor'
  | 'Müşteri Dönüşü Bekleniyor'
  | 'DF Düzenlenecek'
  | 'Kapalı';

export type OdakFaiStatus = 'FAI Bekliyor' | 'FAI Yapıldı' | 'FAI Formu Doldurulmayacak';
export type OdakCapaStatus = 'Acik' | 'Takip' | 'Kapali';

export const ODAK_NCR_STATUS_OPTIONS = [
  { value: 'MRB Bekleniyor', title: 'MRB Bekleniyor' },
  { value: 'Rework/Kontrol Bekleniyor', title: 'Rework/Kontrol Bekleniyor' },
  { value: 'Değerlendirme Bekleniyor', title: 'Değerlendirme Bekleniyor' },
  { value: 'DF No Bekleniyor', title: 'DF No Bekleniyor' },
  { value: 'Müşteri Dönüşü Bekleniyor', title: 'Müşteri Dönüşü Bekleniyor' },
  { value: 'DF Düzenlenecek', title: 'DF Düzenlenecek' },
  { value: 'Kapalı', title: 'Kapalı' },
] as const;

export const ODAK_FAI_STATUS_OPTIONS = [
  { value: 'FAI Bekliyor', title: 'FAI Bekliyor' },
  { value: 'FAI Yapıldı', title: 'FAI Yapıldı' },
  { value: 'FAI Formu Doldurulmayacak', title: 'FAI Formu Doldurulmayacak' },
] as const;

export const ODAK_CAPA_STATUS_OPTIONS = [
  { value: 'Acik', title: 'Açık' },
  { value: 'Takip', title: 'Takip' },
  { value: 'Kapali', title: 'Kapalı' },
] as const;

export interface OdakNcrRow {
  __dataId?: string;
  dataId?: string;
  ncrNo?: string;
  legacyNcNo?: string;
  parentPackageId?: unknown;
  parentLineId?: unknown;
  ncStatus?: OdakNcrStatus | string;
  ncDate?: string;
  controlType?: string;
  descriptor?: string;
  explanation?: string;
  productCode?: string;
  jobNo?: string;
  partCount?: number;
  reworkCount?: number;
  repairCount?: number;
  observeCount?: number;
  scrapCount?: number;
  asisCount?: number;
  returnCount?: number;
  otherCount?: number;
  faiStatus?: OdakFaiStatus | string;
  errorCode?: string;
  ncAction?: string;
  responsible?: string;
  closureDate?: string;
  notes?: string;
  legacyNcrId?: string;
  __createdAt?: string;
}

export type OdakShipmentStatus = 'Planlandi' | 'Tamamlandi' | 'Iptal';
export type OdakQcfStatus = 'Yok' | 'Bekliyor' | 'Tamamlandi';

export const ODAK_SHIPMENT_STATUS_OPTIONS = [
  { value: 'Planlandi', title: 'Planlandı' },
  { value: 'Tamamlandi', title: 'Tamamlandı' },
  { value: 'Iptal', title: 'İptal' },
] as const;

export const ODAK_QCF_STATUS_OPTIONS = [
  { value: 'Yok', title: 'Yok' },
  { value: 'Bekliyor', title: 'Bekliyor' },
  { value: 'Tamamlandi', title: 'Tamamlandı' },
] as const;

export interface OdakShipmentRow {
  __dataId?: string;
  dataId?: string;
  parentPackageId?: unknown;
  waybillNo?: string;
  shipmentDate?: string;
  status?: OdakShipmentStatus | string;
  controlType?: string;
  shipmentAddress?: string;
  notes?: string;
  qcfStatus?: OdakQcfStatus | string;
  qcfReferenceNo?: string;
  qcfNotes?: string;
  legacyShipmentId?: string;
  __createdAt?: string;
}

export interface OdakShipmentLineRow {
  __dataId?: string;
  dataId?: string;
  parentShipmentId?: unknown;
  parentPackageId?: unknown;
  parentLineId?: unknown;
  shippedQuantity?: number;
  lineNo?: number;
  lineDescription?: string;
  legacyShipmentItemId?: string;
}

export interface OdakCapaRow {
  __dataId?: string;
  dataId?: string;
  capaNo?: string;
  legacyCapaNo?: string;
  parentPackageId?: unknown;
  parentNcrId?: unknown;
  cpaDate?: string;
  source?: string;
  requestDivision?: string;
  description?: string;
  nonconformity?: string;
  tecnique?: string;
  errorCode?: string;
  rootCause?: string;
  correctiveAction?: string;
  preventiveAction?: string;
  firstFollowupDate?: string;
  secondFollowupDate?: string;
  closedDate?: string;
  capaStatus?: OdakCapaStatus | string;
  notes?: string;
  legacyCapaId?: string;
  __createdAt?: string;
}
