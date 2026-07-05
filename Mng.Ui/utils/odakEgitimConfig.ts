/**
 * Odak Egitim hub — DG dataset kodlari.
 */
export const ODAK_EGITIM_CONFIG = {
  divisionsDataset: 'odak_birimler',
  trainingsDataset: 'odak_egitimler',
  participationsDataset: 'odak_egitim_katilimlari',
  /** Legacy Kalite SQL export — ilk eğitim yılı */
  legacyFirstYear: 2017,
} as const;

export type OdakTrainingStatus = 'Planlandi' | 'Tamamlandi' | 'Iptal';
export type OdakTrainingTab = 'plan' | 'complete' | 'all';

export interface OdakDivisionRow {
  __dataId?: string;
  dataId?: string;
  kod?: string;
  ad?: string;
  aktif?: boolean;
  legacyDivisionId?: string;
}

export interface OdakTrainingRow {
  __dataId?: string;
  dataId?: string;
  egitimNo?: string;
  baslik?: string;
  konu?: string;
  birimId?: unknown;
  egitimVeren?: string;
  planlananTarih?: string | null;
  gerceklesenTarih?: string | null;
  sureDakika?: number | null;
  konum?: string;
  egitimAmaci?: string;
  degerlendirmeYontemi?: string;
  toplamCalisanSayisi?: number;
  durum?: OdakTrainingStatus | string;
  legacyTrainingId?: string;
  __createdAt?: string;
  __updatedAt?: string;
}

export interface OdakTrainingParticipationRow {
  __dataId?: string;
  dataId?: string;
  parentTrainingId?: unknown;
  personelId?: unknown;
  katildi?: boolean;
  etkin?: boolean | null;
  notlar?: string;
  legacyEmployeeTrainingId?: string;
}

export const ODAK_TRAINING_STATUS_OPTIONS = [
  { value: 'Planlandi' as const, title: 'Planlandı' },
  { value: 'Tamamlandi' as const, title: 'Tamamlandı' },
  { value: 'Iptal' as const, title: 'İptal' },
] as const;
