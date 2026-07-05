import { ocCreate, ocDelete, ocListDatasetPage, ocUpdate } from '@/services/operationCoreService';
import { useUserStore } from '@/stores/apps/user';
import {
  mapUserToOcPersonPickerItem,
  type OcPersonPickerItem,
} from '@/utils/ocPersonPicker';
import { personIdFromRow } from '@/utils/odakSiparisPackagePersonnel';
import {
  ODAK_EGITIM_CONFIG,
  type OdakDivisionRow,
  type OdakTrainingParticipationRow,
  type OdakTrainingRow,
  type OdakTrainingStatus,
  type OdakTrainingTab,
} from '@/utils/odakEgitimConfig';

export function trainingDataId(row: Pick<OdakTrainingRow, '__dataId' | 'dataId'> | null | undefined): string {
  if (!row) return '';
  return String(row.__dataId ?? row.dataId ?? '').trim();
}

export function participationDataId(row: Pick<OdakTrainingParticipationRow, '__dataId' | 'dataId'> | null | undefined): string {
  if (!row) return '';
  return String(row.__dataId ?? row.dataId ?? '').trim();
}

export function divisionDataId(row: Pick<OdakDivisionRow, '__dataId' | 'dataId'> | null | undefined): string {
  if (!row) return '';
  return String(row.__dataId ?? row.dataId ?? '').trim();
}

export function relationIdFromRow(raw: unknown): string {
  if (raw == null || raw === '') return '';
  if (typeof raw === 'string') return raw.trim();
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    return String(o.__dataId ?? o.dataId ?? o.id ?? '').trim();
  }
  return String(raw).trim();
}

export function trainingDisplayNo(row: OdakTrainingRow): string {
  return row.egitimNo?.trim() || trainingDataId(row) || '—';
}

export function trainingStatusFromRow(row: OdakTrainingRow): OdakTrainingStatus {
  if (row.durum === 'Iptal') return 'Iptal';
  if (row.gerceklesenTarih) return 'Tamamlandi';
  return 'Planlandi';
}

export function trainingStatusLabel(status: OdakTrainingStatus | string | undefined): string {
  if (status === 'Tamamlandi') return 'Tamamlandı';
  if (status === 'Iptal') return 'İptal';
  return 'Planlandı';
}

export function formatOdakTrainingDate(value: unknown): string {
  if (value == null || value === '') return '—';
  const d = new Date(String(value));
  if (Number.isNaN(d.getTime())) return String(value);
  return d.toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function yearFromIso(value: unknown): number | null {
  if (value == null || value === '') return null;
  const d = new Date(String(value));
  if (Number.isNaN(d.getTime())) return null;
  return d.getFullYear();
}

function monthFromIso(value: unknown): number | null {
  if (value == null || value === '') return null;
  const d = new Date(String(value));
  if (Number.isNaN(d.getTime())) return null;
  return d.getMonth() + 1;
}

/** Eğitim süresi (saat) — sureDakika dakikadan. */
export function trainingDurationHours(training: Pick<OdakTrainingRow, 'sureDakika'> | null | undefined): number {
  const minutes = training?.sureDakika;
  if (minutes == null || minutes <= 0) return 0;
  return minutes / 60;
}

export function formatOdakTrainingHours(hours: number, fractionDigits = 2): string {
  if (!Number.isFinite(hours) || hours <= 0) return '—';
  return hours.toLocaleString('tr-TR', {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  });
}

export function formatOdakTrainingDuration(training: Pick<OdakTrainingRow, 'sureDakika'> | null | undefined): string {
  return formatOdakTrainingHours(trainingDurationHours(training));
}

/** Kişinin katıldığı tek eğitim için harcanan süre (saat). */
export function personTrainingHoursForParticipation(
  training: Pick<OdakTrainingRow, 'sureDakika'> | null | undefined,
  katildi: boolean | null | undefined
): number {
  if (katildi === false) return 0;
  return trainingDurationHours(training);
}

/** Eğitimde katılanlar için toplam insan-saati. */
export function trainingTotalAttendedPersonHours(
  training: Pick<OdakTrainingRow, 'sureDakika'> | null | undefined,
  attendedCount: number
): number {
  if (attendedCount <= 0) return 0;
  return trainingDurationHours(training) * attendedCount;
}

/**
 * Legacy aylık istatistik katkısı: katılım kaydı × (sureSaat / toplamCalisanSayisi).
 * @see Kalite TrainingsController::statistics
 */
export function legacyMonthlyStatContribution(
  training: Pick<OdakTrainingRow, 'sureDakika' | 'toplamCalisanSayisi'>,
  participationCount: number
): number {
  if (participationCount <= 0) return 0;
  const hours = trainingDurationHours(training);
  if (hours <= 0) return 0;
  const staff = training.toplamCalisanSayisi;
  const divisor = staff != null && staff > 0 ? staff : 1;
  return participationCount * (hours / divisor);
}

export function sumPersonTrainingHours(
  rows: Array<{ training: Pick<OdakTrainingRow, 'sureDakika'> | null; participation: Pick<OdakTrainingParticipationRow, 'katildi'> }>
): number {
  let total = 0;
  for (const row of rows) {
    total += personTrainingHoursForParticipation(row.training, row.participation.katildi);
  }
  return total;
}

function yearFromEgitimNo(egitimNo: unknown): number | null {
  const m = String(egitimNo ?? '').match(/^EGTM(\d{4})\//);
  return m ? parseInt(m[1], 10) : null;
}

function planYearFromRow(row: OdakTrainingRow): number | null {
  return yearFromIso(row.planlananTarih) ?? yearFromEgitimNo(row.egitimNo);
}

function actualYearFromRow(row: OdakTrainingRow): number | null {
  return yearFromIso(row.gerceklesenTarih) ?? yearFromEgitimNo(row.egitimNo);
}

/** Yıl seçici — legacy 2017'den güncel yıla kadar (azalan). */
export function buildOdakEgitimYearOptions(): number[] {
  const current = new Date().getFullYear();
  const first = ODAK_EGITIM_CONFIG.legacyFirstYear;
  const years: number[] = [];
  for (let y = current + 1; y >= first; y--) years.push(y);
  return years;
}

export function trainingMatchesTab(row: OdakTrainingRow, tab: OdakTrainingTab, year: number | null): boolean {
  const status = trainingStatusFromRow(row);
  if (tab === 'plan') {
    if (status !== 'Planlandi') return false;
    if (year == null) return true;
    const y = planYearFromRow(row);
    return y == null || y === year;
  }
  if (tab === 'complete') {
    if (status !== 'Tamamlandi') return false;
    if (year == null) return true;
    const y = actualYearFromRow(row);
    return y == null || y === year;
  }
  if (year == null) return true;
  const planY = planYearFromRow(row);
  const actualY = actualYearFromRow(row);
  return planY === year || actualY === year;
}

export function trainingMatchesSearch(row: OdakTrainingRow, search: string): boolean {
  const q = search.trim().toLowerCase();
  if (!q) return true;
  const hay = [
    row.egitimNo,
    row.baslik,
    row.konu,
    row.egitimVeren,
    row.konum,
    row.egitimAmaci,
    row.degerlendirmeYontemi,
  ]
    .filter(Boolean)
    .join(' ')
    .toLowerCase();
  return hay.includes(q);
}

export interface OdakTrainingFormModel {
  baslik: string;
  konu: string;
  birimId: string | null;
  egitimVeren: string;
  planlananTarih: string;
  gerceklesenTarih: string;
  sureDakika: number | null;
  konum: string;
  egitimAmaci: string;
  degerlendirmeYontemi: string;
  toplamCalisanSayisi: number | null;
  durum: OdakTrainingStatus;
}

export function emptyTrainingFormModel(partial?: Partial<OdakTrainingFormModel>): OdakTrainingFormModel {
  return {
    baslik: partial?.baslik ?? '',
    konu: partial?.konu ?? '',
    birimId: partial?.birimId ?? null,
    egitimVeren: partial?.egitimVeren ?? '',
    planlananTarih: partial?.planlananTarih ?? '',
    gerceklesenTarih: partial?.gerceklesenTarih ?? '',
    sureDakika: partial?.sureDakika ?? null,
    konum: partial?.konum ?? '',
    egitimAmaci: partial?.egitimAmaci ?? '',
    degerlendirmeYontemi: partial?.degerlendirmeYontemi ?? '',
    toplamCalisanSayisi: partial?.toplamCalisanSayisi ?? null,
    durum: partial?.durum ?? 'Planlandi',
  };
}

function isoFromLocalInput(value: string): string | null {
  const v = value.trim();
  if (!v) return null;
  const d = new Date(v);
  if (Number.isNaN(d.getTime())) return null;
  return d.toISOString();
}

function localInputFromIso(value: unknown): string {
  if (value == null || value === '') return '';
  const d = new Date(String(value));
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function trainingRowToFormModel(row: OdakTrainingRow): OdakTrainingFormModel {
  return emptyTrainingFormModel({
    baslik: row.baslik ?? '',
    konu: row.konu ?? '',
    birimId: relationIdFromRow(row.birimId) || null,
    egitimVeren: row.egitimVeren ?? '',
    planlananTarih: localInputFromIso(row.planlananTarih),
    gerceklesenTarih: localInputFromIso(row.gerceklesenTarih),
    sureDakika: row.sureDakika ?? null,
    konum: row.konum ?? '',
    egitimAmaci: row.egitimAmaci ?? '',
    degerlendirmeYontemi: row.degerlendirmeYontemi ?? '',
    toplamCalisanSayisi: row.toplamCalisanSayisi ?? null,
    durum: trainingStatusFromRow(row),
  });
}

export function resolveTrainingDurum(form: OdakTrainingFormModel): OdakTrainingStatus {
  if (form.durum === 'Iptal') return 'Iptal';
  if (form.gerceklesenTarih.trim()) return 'Tamamlandi';
  return 'Planlandi';
}

export function formModelToTrainingPayload(form: OdakTrainingFormModel): Record<string, unknown> {
  const durum = resolveTrainingDurum(form);
  return {
    baslik: form.baslik.trim(),
    konu: form.konu.trim() || null,
    birimId: form.birimId || null,
    egitimVeren: form.egitimVeren.trim() || null,
    planlananTarih: isoFromLocalInput(form.planlananTarih),
    gerceklesenTarih: isoFromLocalInput(form.gerceklesenTarih),
    sureDakika: form.sureDakika ?? null,
    konum: form.konum.trim() || null,
    egitimAmaci: form.egitimAmaci.trim() || null,
    degerlendirmeYontemi: form.degerlendirmeYontemi.trim() || null,
    toplamCalisanSayisi: form.toplamCalisanSayisi ?? 0,
    durum,
  };
}

export async function generateEgitimNo(year = new Date().getFullYear()): Promise<string> {
  const prefix = `EGTM${year}/`;
  const resp = await ocListDatasetPage(ODAK_EGITIM_CONFIG.trainingsDataset, {
    limit: 500,
    sort: '-egitimNo',
    search: prefix,
  });
  const items = (resp.items ?? []) as OdakTrainingRow[];
  let max = 0;
  for (const row of items) {
    const no = row.egitimNo ?? '';
    const m = no.match(new RegExp(`^EGTM${year}/(\\d+)$`));
    if (m) max = Math.max(max, parseInt(m[1], 10));
  }
  return `${prefix}${String(max + 1).padStart(2, '0')}`;
}

export interface OdakTrainingListQuery {
  tab?: OdakTrainingTab;
  /** null = tüm yıllar */
  year?: number | null;
  search?: string;
  page?: number;
  limit?: number;
}

async function fetchOdakTrainingsFiltered(query: Pick<OdakTrainingListQuery, 'tab' | 'year' | 'search'> = {}): Promise<OdakTrainingRow[]> {
  const tab = query.tab ?? 'plan';
  const year = query.year === undefined ? null : query.year;
  const search = query.search?.trim() ?? '';

  let filter: string | undefined;
  if (tab === 'plan') filter = 'durum:eq:Planlandi';
  else if (tab === 'complete') filter = 'durum:eq:Tamamlandi';

  const resp = await ocListDatasetPage(ODAK_EGITIM_CONFIG.trainingsDataset, {
    skip: 0,
    limit: 1000,
    sort: tab === 'complete' ? '-gerceklesenTarih' : '-planlananTarih',
    filter,
    search: search || undefined,
  });

  let items = ((resp.items ?? []) as OdakTrainingRow[]).filter((row) =>
    trainingMatchesTab(row, tab, year)
  );
  if (search) {
    items = items.filter((row) => trainingMatchesSearch(row, search));
  }
  return items;
}

export async function fetchOdakTrainingsPage(query: OdakTrainingListQuery = {}): Promise<{
  items: OdakTrainingRow[];
  total: number;
}> {
  const page = query.page ?? 1;
  const limit = query.limit ?? 20;
  const items = await fetchOdakTrainingsFiltered(query);
  const total = items.length;
  const skip = (page - 1) * limit;
  return { items: items.slice(skip, skip + limit), total };
}

export async function fetchOdakTrainingById(id: string): Promise<OdakTrainingRow | null> {
  const resp = await ocListDatasetPage(ODAK_EGITIM_CONFIG.trainingsDataset, {
    limit: 1,
    filter: `__dataId:eq:${id}`,
  });
  const row = (resp.items?.[0] ?? null) as OdakTrainingRow | null;
  return row;
}

export async function createOdakTraining(form: OdakTrainingFormModel): Promise<string> {
  const egitimNo = await generateEgitimNo();
  const payload = { ...formModelToTrainingPayload(form), egitimNo };
  const created = await ocCreate(ODAK_EGITIM_CONFIG.trainingsDataset, payload);
  const id = String((created as Record<string, unknown>)?.__dataId ?? (created as Record<string, unknown>)?.dataId ?? '').trim();
  return id;
}

export async function updateOdakTraining(id: string, form: OdakTrainingFormModel): Promise<void> {
  await ocUpdate(ODAK_EGITIM_CONFIG.trainingsDataset, id, formModelToTrainingPayload(form));
}

export async function deleteOdakTraining(id: string): Promise<void> {
  await ocDelete(ODAK_EGITIM_CONFIG.trainingsDataset, id);
}

export async function fetchOdakDivisions(activeOnly = true): Promise<OdakDivisionRow[]> {
  const filter = activeOnly ? 'aktif:eq:true' : undefined;
  const resp = await ocListDatasetPage(ODAK_EGITIM_CONFIG.divisionsDataset, {
    limit: 200,
    sort: 'ad',
    filter,
  });
  return (resp.items ?? []) as OdakDivisionRow[];
}

export type OdakDivisionAktifTab = 'active' | 'inactive' | 'all';

export interface OdakDivisionFormModel {
  kod: string;
  ad: string;
  aktif: boolean;
}

export interface OdakDivisionListQuery {
  aktifTab?: OdakDivisionAktifTab;
  search?: string;
}

function divisionMatchesSearch(row: OdakDivisionRow, search: string): boolean {
  const q = search.trim().toLowerCase();
  if (!q) return true;
  const hay = [row.kod, row.ad, row.legacyDivisionId].filter(Boolean).join(' ').toLowerCase();
  return hay.includes(q);
}

export async function fetchOdakDivisionsPage(query: OdakDivisionListQuery = {}): Promise<{
  items: OdakDivisionRow[];
  total: number;
}> {
  const tab = query.aktifTab ?? 'active';
  const search = query.search?.trim() ?? '';
  let items = await fetchOdakDivisions(false);
  if (tab === 'active') items = items.filter((r) => r.aktif !== false);
  else if (tab === 'inactive') items = items.filter((r) => r.aktif === false);
  if (search) items = items.filter((r) => divisionMatchesSearch(r, search));
  items.sort((a, b) => (a.ad ?? '').localeCompare(b.ad ?? '', 'tr'));
  return { items, total: items.length };
}

export function emptyDivisionFormModel(partial?: Partial<OdakDivisionFormModel>): OdakDivisionFormModel {
  return {
    kod: partial?.kod ?? '',
    ad: partial?.ad ?? '',
    aktif: partial?.aktif ?? true,
  };
}

export function divisionRowToFormModel(row: OdakDivisionRow): OdakDivisionFormModel {
  return emptyDivisionFormModel({
    kod: row.kod ?? '',
    ad: row.ad ?? '',
    aktif: row.aktif !== false,
  });
}

export function formModelToDivisionPayload(form: OdakDivisionFormModel): Record<string, unknown> {
  return {
    kod: form.kod.trim(),
    ad: form.ad.trim(),
    aktif: form.aktif,
  };
}

export async function suggestNextDivisionKod(): Promise<string> {
  const rows = await fetchOdakDivisions(false);
  let max = 0;
  for (const row of rows) {
    const kod = row.kod ?? '';
    const m = kod.match(/^BRM-(\d+)$/i);
    if (m) max = Math.max(max, parseInt(m[1], 10));
  }
  return `BRM-${String(max + 1).padStart(3, '0')}`;
}

export async function createOdakDivision(form: OdakDivisionFormModel): Promise<string> {
  const created = await ocCreate(ODAK_EGITIM_CONFIG.divisionsDataset, formModelToDivisionPayload(form));
  const rec = created as Record<string, unknown>;
  return String(rec?.__dataId ?? rec?.dataId ?? '').trim();
}

export async function updateOdakDivision(id: string, form: OdakDivisionFormModel): Promise<void> {
  await ocUpdate(ODAK_EGITIM_CONFIG.divisionsDataset, id, formModelToDivisionPayload(form));
}

export async function deleteOdakDivision(id: string): Promise<void> {
  await ocDelete(ODAK_EGITIM_CONFIG.divisionsDataset, id);
}

export interface OdakEgitimStatsSummary {
  divisionCount: number;
  activeDivisionCount: number;
  trainingCount: number;
  plannedCount: number;
  completedCount: number;
  cancelledCount: number;
  participationCount: number;
  distinctParticipantCount: number;
  byDivision: Array<{ divisionId: string; divisionLabel: string; trainingCount: number }>;
  byYear: Array<{ year: number; planned: number; completed: number }>;
}

export interface OdakEgitimMonthlyHoursRow {
  month: number;
  monthlyHours: number;
  cumulativeHours: number;
}

export interface OdakEgitimMonthlyHoursStats {
  year: number;
  rows: OdakEgitimMonthlyHoursRow[];
  yearTotalHours: number;
}

/** İstatistik yıl seçici — legacyFirstYear (2017) … güncel yıl + 2. */
export function buildOdakEgitimStatsYearOptions(): number[] {
  const current = new Date().getFullYear();
  const first = ODAK_EGITIM_CONFIG.legacyFirstYear;
  const years: number[] = [];
  for (let y = current + 2; y >= first; y--) years.push(y);
  return years;
}

export async function fetchOdakEgitimMonthlyHoursStats(year: number): Promise<OdakEgitimMonthlyHoursStats> {
  const [trainings, partCounts] = await Promise.all([
    fetchOdakTrainingsFiltered({ tab: 'complete', year: null }),
    fetchParticipationCountByTrainingId(),
  ]);

  const monthly = Array.from({ length: 12 }, () => 0);
  for (const tr of trainings) {
    if (trainingStatusFromRow(tr) !== 'Tamamlandi') continue;
    if (yearFromIso(tr.gerceklesenTarih) !== year) continue;
    const month = monthFromIso(tr.gerceklesenTarih);
    if (month == null || month < 1 || month > 12) continue;
    const tid = trainingDataId(tr);
    monthly[month - 1] += legacyMonthlyStatContribution(tr, partCounts[tid] ?? 0);
  }

  let cumulative = 0;
  const rows: OdakEgitimMonthlyHoursRow[] = monthly.map((monthlyHours, index) => {
    cumulative += monthlyHours;
    return { month: index + 1, monthlyHours, cumulativeHours: cumulative };
  });

  return { year, rows, yearTotalHours: cumulative };
}

export async function fetchOdakEgitimStats(): Promise<OdakEgitimStatsSummary> {
  const [divisions, trainingResp] = await Promise.all([
    fetchOdakDivisions(false),
    ocListDatasetPage(ODAK_EGITIM_CONFIG.trainingsDataset, { limit: 1000, sort: '-egitimNo' }),
  ]);
  const trainings = (trainingResp.items ?? []) as OdakTrainingRow[];
  const divisionLabels = await fetchDivisionLabelMap(divisions.map((d) => divisionDataId(d)).filter(Boolean));

  let participationCount = 0;
  const participantIds = new Set<string>();
  let skip = 0;
  const limit = 500;
  while (true) {
    const resp = await ocListDatasetPage(ODAK_EGITIM_CONFIG.participationsDataset, { skip, limit });
    const batch = (resp.items ?? []) as OdakTrainingParticipationRow[];
    participationCount += batch.length;
    for (const p of batch) {
      const pid = participationPersonId(p);
      if (pid) participantIds.add(pid);
    }
    if (batch.length < limit) break;
    skip += limit;
  }

  const byDivisionMap: Record<string, number> = {};
  for (const tr of trainings) {
    const divId = relationIdFromRow(tr.birimId);
    const key = divId || '__none__';
    byDivisionMap[key] = (byDivisionMap[key] ?? 0) + 1;
  }
  const byDivision = Object.entries(byDivisionMap)
    .map(([divisionId, trainingCount]) => ({
      divisionId,
      divisionLabel:
        divisionId === '__none__'
          ? '—'
          : divisionLabels[divisionId] ?? divisionId,
      trainingCount,
    }))
    .sort((a, b) => b.trainingCount - a.trainingCount);

  const byYearMap: Record<number, { planned: number; completed: number }> = {};
  for (const tr of trainings) {
    const status = trainingStatusFromRow(tr);
    const year =
      yearFromIso(tr.gerceklesenTarih) ??
      yearFromIso(tr.planlananTarih) ??
      new Date().getFullYear();
    if (!byYearMap[year]) byYearMap[year] = { planned: 0, completed: 0 };
    if (status === 'Tamamlandi') byYearMap[year].completed++;
    else if (status === 'Planlandi') byYearMap[year].planned++;
  }
  const byYear = Object.entries(byYearMap)
    .map(([year, counts]) => ({ year: parseInt(year, 10), ...counts }))
    .sort((a, b) => b.year - a.year);

  let plannedCount = 0;
  let completedCount = 0;
  let cancelledCount = 0;
  for (const tr of trainings) {
    const s = trainingStatusFromRow(tr);
    if (s === 'Tamamlandi') completedCount++;
    else if (s === 'Iptal') cancelledCount++;
    else plannedCount++;
  }

  return {
    divisionCount: divisions.length,
    activeDivisionCount: divisions.filter((d) => d.aktif !== false).length,
    trainingCount: trainings.length,
    plannedCount,
    completedCount,
    cancelledCount,
    participationCount,
    distinctParticipantCount: participantIds.size,
    byDivision,
    byYear,
  };
}

export type OdakDivisionDialogMode = 'create' | 'edit';

export async function fetchDivisionLabelMap(ids: string[]): Promise<Record<string, string>> {
  const unique = [...new Set(ids.filter(Boolean))];
  if (!unique.length) return {};
  const rows = await fetchOdakDivisions(false);
  const map: Record<string, string> = {};
  for (const row of rows) {
    const id = divisionDataId(row);
    if (id) map[id] = row.ad?.trim() || row.kod?.trim() || id;
  }
  return map;
}

export async function fetchParticipationsForTraining(trainingId: string): Promise<OdakTrainingParticipationRow[]> {
  const all: OdakTrainingParticipationRow[] = [];
  let skip = 0;
  const limit = 500;
  while (true) {
    const resp = await ocListDatasetPage(ODAK_EGITIM_CONFIG.participationsDataset, {
      skip,
      limit,
      filter: `parentTrainingId:eq:${trainingId}`,
      sort: 'personelId',
    });
    const batch = (resp.items ?? []) as OdakTrainingParticipationRow[];
    all.push(...batch);
    if (batch.length < limit) break;
    skip += limit;
  }
  return all;
}

/** Tüm katılımları sayfa sayfa yükler (liste katılımcı sayısı için). */
export async function fetchParticipationCountByTrainingId(): Promise<Record<string, number>> {
  const map: Record<string, number> = {};
  let skip = 0;
  const limit = 500;
  while (true) {
    const resp = await ocListDatasetPage(ODAK_EGITIM_CONFIG.participationsDataset, {
      skip,
      limit,
      sort: 'parentTrainingId',
    });
    const batch = (resp.items ?? []) as OdakTrainingParticipationRow[];
    for (const row of batch) {
      const trainingId = relationIdFromRow(row.parentTrainingId);
      if (!trainingId) continue;
      map[trainingId] = (map[trainingId] ?? 0) + 1;
    }
    if (batch.length < limit) break;
    skip += limit;
  }
  return map;
}

export async function fetchParticipationsForPerson(personelId: string): Promise<OdakTrainingParticipationRow[]> {
  const id = personelId.trim();
  if (!id) return [];
  const all: OdakTrainingParticipationRow[] = [];
  let skip = 0;
  const limit = 500;
  while (true) {
    const resp = await ocListDatasetPage(ODAK_EGITIM_CONFIG.participationsDataset, {
      skip,
      limit,
      filter: `personelId:eq:${id}`,
      sort: '-__createdAt',
    });
    const batch = (resp.items ?? []) as OdakTrainingParticipationRow[];
    all.push(...batch);
    if (batch.length < limit) break;
    skip += limit;
  }
  return all;
}

export async function fetchTrainingsByIds(ids: string[]): Promise<Record<string, OdakTrainingRow>> {
  const wanted = new Set(ids.filter(Boolean));
  if (!wanted.size) return {};
  const map: Record<string, OdakTrainingRow> = {};
  let skip = 0;
  const limit = 500;
  while (true) {
    const resp = await ocListDatasetPage(ODAK_EGITIM_CONFIG.trainingsDataset, {
      skip,
      limit,
      sort: '-egitimNo',
    });
    const batch = (resp.items ?? []) as OdakTrainingRow[];
    for (const row of batch) {
      const id = trainingDataId(row);
      if (id && wanted.has(id)) map[id] = row;
    }
    if (wanted.size === Object.keys(map).length) break;
    if (batch.length < limit) break;
    skip += limit;
  }
  return map;
}

export interface PersonTrainingHistoryRow {
  participation: OdakTrainingParticipationRow;
  training: OdakTrainingRow | null;
  trainingId: string;
}

export async function fetchPersonTrainingHistory(personelId: string): Promise<PersonTrainingHistoryRow[]> {
  const participations = await fetchParticipationsForPerson(personelId);
  const trainingIds = participations
    .map((p) => relationIdFromRow(p.parentTrainingId))
    .filter(Boolean);
  const trainingMap = await fetchTrainingsByIds(trainingIds);
  return participations.map((participation) => {
    const trainingId = relationIdFromRow(participation.parentTrainingId);
    return {
      participation,
      training: trainingMap[trainingId] ?? null,
      trainingId,
    };
  });
}

export async function addParticipation(
  trainingId: string,
  personelId: string,
  katildi = true
): Promise<void> {
  await ocCreate(ODAK_EGITIM_CONFIG.participationsDataset, {
    parentTrainingId: trainingId,
    personelId,
    katildi,
    etkin: null,
  });
}

export async function updateParticipation(
  id: string,
  patch: Partial<Pick<OdakTrainingParticipationRow, 'katildi' | 'etkin' | 'notlar'>>
): Promise<void> {
  await ocUpdate(ODAK_EGITIM_CONFIG.participationsDataset, id, patch);
}

export async function removeParticipation(id: string): Promise<void> {
  await ocDelete(ODAK_EGITIM_CONFIG.participationsDataset, id);
}

export async function fetchEgitimPersonPickerItems(search = ''): Promise<OcPersonPickerItem[]> {
  const userStore = useUserStore();
  await userStore.fetchUsers({ search: search.trim() || undefined, limit: 50 });
  return userStore.users
    .map((u) => mapUserToOcPersonPickerItem(u))
    .filter((x): x is OcPersonPickerItem => x != null);
}

export function participationPersonId(row: OdakTrainingParticipationRow): string {
  return personIdFromRow(row.personelId);
}

export type OdakTrainingDialogMode = 'create' | 'edit';
