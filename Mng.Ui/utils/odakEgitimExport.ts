import { exportToCSV } from '@/utils/exportUtils';
import type { OdakTrainingRow, OdakTrainingTab } from '@/utils/odakEgitimConfig';
import {
  fetchDivisionLabelMap,
  fetchOdakTrainingsFiltered,
  fetchParticipationCountByTrainingId,
  formatOdakTrainingDate,
  relationIdFromRow,
  trainingDisplayNo,
  trainingStatusLabel,
  trainingStatusFromRow,
} from '@/utils/odakEgitimService';

export interface OdakTrainingExportQuery {
  tab: OdakTrainingTab;
  year?: number | null;
  search?: string;
}

export interface OdakTrainingExportColumn {
  key: string;
  label: string;
}

/** Legacy F19 export sütunları ile hizalı CSV başlıkları. */
export function odakTrainingExportColumns(labels: Record<string, string>): OdakTrainingExportColumn[] {
  return [
    { key: 'baslik', label: labels.baslik ?? 'Başlık' },
    { key: 'konu', label: labels.konu ?? 'Konu' },
    { key: 'konum', label: labels.konum ?? 'Konum' },
    { key: 'egitimVeren', label: labels.egitimVeren ?? 'Eğitimi Veren' },
    { key: 'planlananTarih', label: labels.planlananTarih ?? 'Planlanan Tarih' },
    { key: 'gerceklesenTarih', label: labels.gerceklesenTarih ?? 'Gerçekleşen Tarih' },
    { key: 'degerlendirmeYontemi', label: labels.degerlendirmeYontemi ?? 'Değerlendirme' },
    { key: 'egitimAmaci', label: labels.egitimAmaci ?? 'Eğitim Amaçları' },
    { key: 'toplamCalisanSayisi', label: labels.toplamCalisanSayisi ?? 'Toplam Çalışan' },
    { key: 'sureDakika', label: labels.sureDakika ?? 'Eğitim Süresi (dk)' },
    { key: 'egitimNo', label: labels.egitimNo ?? 'Eğitim No' },
    { key: 'birim', label: labels.birim ?? 'Birim' },
    { key: 'durum', label: labels.durum ?? 'Durum' },
    { key: 'katilimciSayisi', label: labels.katilimciSayisi ?? 'Katılımcı' },
  ];
}

function cell(value: unknown): string {
  if (value == null || value === '') return '';
  return String(value);
}

export function buildOdakTrainingExportRows(
  items: OdakTrainingRow[],
  divisionLabels: Record<string, string>,
  participationCounts: Record<string, number>
): Record<string, string>[] {
  return items.map((row) => {
    const birimId = relationIdFromRow(row.birimId);
    const id = String(row.__dataId ?? row.dataId ?? '').trim();
    return {
      baslik: cell(row.baslik),
      konu: cell(row.konu),
      konum: cell(row.konum),
      egitimVeren: cell(row.egitimVeren),
      planlananTarih: formatOdakTrainingDate(row.planlananTarih),
      gerceklesenTarih: formatOdakTrainingDate(row.gerceklesenTarih),
      degerlendirmeYontemi: cell(row.degerlendirmeYontemi),
      egitimAmaci: cell(row.egitimAmaci),
      toplamCalisanSayisi: row.toplamCalisanSayisi != null ? String(row.toplamCalisanSayisi) : '',
      sureDakika: row.sureDakika != null ? String(row.sureDakika) : '',
      egitimNo: trainingDisplayNo(row),
      birim: birimId ? divisionLabels[birimId] ?? birimId : '',
      durum: trainingStatusLabel(trainingStatusFromRow(row)),
      katilimciSayisi: String(participationCounts[id] ?? 0),
    };
  });
}

export async function exportOdakTrainingsToCsv(
  query: OdakTrainingExportQuery,
  labels: Record<string, string>
): Promise<void> {
  const [items, partCounts] = await Promise.all([
    fetchOdakTrainingsFiltered({
      tab: query.tab,
      year: query.year ?? null,
      search: query.search,
    }),
    fetchParticipationCountByTrainingId(),
  ]);
  const birimIds = items.map((r) => relationIdFromRow(r.birimId)).filter(Boolean);
  const divisionLabels = await fetchDivisionLabelMap(birimIds);
  const columns = odakTrainingExportColumns(labels);
  const rows = buildOdakTrainingExportRows(items, divisionLabels, partCounts);
  const headers = columns.map((c) => c.label);
  const data = rows.map((row) => {
    const out: Record<string, string> = {};
    for (const col of columns) {
      out[col.label] = row[col.key] ?? '';
    }
    return out;
  });
  const tabLabel = query.tab === 'plan' ? 'planlanan' : query.tab === 'complete' ? 'tamamlanan' : 'tum';
  const yearSuffix = query.year != null ? `_${query.year}` : '';
  exportToCSV(data, `odak_egitimler_${tabLabel}${yearSuffix}`, headers);
}
