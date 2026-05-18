<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useMonitoringStore } from '@/stores/apps/monitoring';
import { useAuthStore } from '@/stores/auth';
import { fetchFromDataGateway } from '@/services/apiService';
import type {
  MonCollectionPeriod,
  MonSchedule,
  MonEngine,
  MonAgent,
} from '@/types/apps/monitoring';
import { PlusIcon, RefreshIcon, EditIcon, TrashIcon, CalendarEventIcon, KeyIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}
function mtParam(key: string, params: Record<string, string>, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key, params) || fallback;
    if (i18n?.t) return i18n.t(key, params) || fallback;
  } catch (_) {}
  return fallback;
}

const authStore = useAuthStore();
const store = useMonitoringStore();
const canEdit = computed(() => authStore.isManager);

const page = computed(() => ({ title: mt('monitoring.pageTitle', 'Monitoring tanımları') }));
const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('monitoring.pageTitle', 'Monitoring tanımları'), disabled: true, href: '#' },
]);

const route = useRoute();
const activeTab = ref<'periods' | 'schedules' | 'engines' | 'agents'>('periods');

function applyTabFromQuery() {
  const tab = route.query.tab as string | undefined;
  if (tab === 'periods' || tab === 'schedules' || tab === 'engines' || tab === 'agents') activeTab.value = tab;
}

// --- Periyotlar ---
const periodSearch = ref('');
const periodFormOpen = ref(false);
const periodFormModel = ref<MonCollectionPeriod | null>(null);
const periodDeleteTarget = ref<MonCollectionPeriod | null>(null);
const periodDeleteDialogOpen = ref(false);
const periodForm = ref<Partial<MonCollectionPeriod>>({ name: '', description: null, expression: '' });
const periodHeaders = computed(() => [
  { title: mt('monitoring.collectionPeriods.tableName', 'Ad'), key: 'name', sortable: true },
  { title: mt('monitoring.collectionPeriods.tableCron', 'Cron ifadesi'), key: 'expression', sortable: false },
  { title: mt('monitoring.collectionPeriods.tableDescription', 'Açıklama'), key: 'description', sortable: false },
  { title: mt('monitoring.collectionPeriods.tableActions', 'İşlemler'), key: 'actions', sortable: false, align: 'end' as const },
]);
const filteredPeriods = computed(() => {
  const q = periodSearch.value.toLowerCase().trim();
  if (!q) return store.collectionPeriods;
  return store.collectionPeriods.filter(
    (p) =>
      (p.name ?? '').toLowerCase().includes(q) ||
      (p.expression ?? '').toLowerCase().includes(q) ||
      (p.description ?? '').toLowerCase().includes(q)
  );
});

// --- Cron builder (6 alanlı Quartz; periyot ve engine formlarında kullanılır) ---
const cronBuilderTarget = ref<'period' | 'engine'>('period');
const cronBuilderOpen = ref(false);
const cronBuilderTab = ref<'simple' | 'advanced'>('simple');
const cronBuilderSelectedPreset = ref<string | null>(null);
// Tab 1 - Basit: tek satır [N] [birim]
const cronBuilderEveryN = ref(5);
const cronBuilderUnit = ref<'second' | 'minute' | 'hour'>('minute');
// Tab 1 - Gün/hafta bloku
const cronBuilderCustomMode = ref<'every' | 'daily' | 'weekly'>('every');
const cronBuilderDailyHour = ref(0);
const cronBuilderDailyMinute = ref(0);
const cronBuilderWeeklyDay = ref(0); // 0 Pazar, 1 Pzt, ...
// Tab 2 - Karma (ileride genişletilecek)
const cronBuilderRaw = ref('0 */5 * * * *');

const cronPresets = [
  { value: '*/10 * * * * *', labelKey: 'monitoring.cronBuilder.every10Seconds' },
  { value: '*/15 * * * * *', labelKey: 'monitoring.cronBuilder.every15Seconds' },
  { value: '*/30 * * * * *', labelKey: 'monitoring.cronBuilder.every30Seconds' },
  { value: '0 * * * * *', labelKey: 'monitoring.cronBuilder.everyMinute' },
  { value: '0 */5 * * * *', labelKey: 'monitoring.cronBuilder.every5Minutes' },
  { value: '0 */15 * * * *', labelKey: 'monitoring.cronBuilder.every15Minutes' },
  { value: '0 */30 * * * *', labelKey: 'monitoring.cronBuilder.every30Minutes' },
  { value: '0 0 * * * *', labelKey: 'monitoring.cronBuilder.hourly' },
  { value: '0 0 0 * * *', labelKey: 'monitoring.cronBuilder.dailyAtMidnight' },
  { value: '0 0 0 * * 0', labelKey: 'monitoring.cronBuilder.weeklySunday' },
];

function build6FieldFromSimple(): string {
  const n = Math.max(1, Math.min(cronBuilderUnit.value === 'hour' ? 23 : 59, cronBuilderEveryN.value));
  if (cronBuilderCustomMode.value !== 'every') {
    const m = Math.max(0, Math.min(59, cronBuilderDailyMinute.value));
    const h = Math.max(0, Math.min(23, cronBuilderDailyHour.value));
    if (cronBuilderCustomMode.value === 'daily') return `0 ${m} ${h} * * *`;
    const d = Math.max(0, Math.min(6, cronBuilderWeeklyDay.value));
    return `0 ${m} ${h} * * ${d}`;
  }
  if (cronBuilderUnit.value === 'second') return `*/${n} * * * * *`;
  if (cronBuilderUnit.value === 'minute') return `0 */${n} * * * *`;
  return `0 0 */${n} * * *`;
}

const cronBuilderGenerated = computed(() => {
  if (cronBuilderTab.value === 'advanced') return cronBuilderRaw.value.trim() || '0 */5 * * * *';
  return cronBuilderSelectedPreset.value ?? build6FieldFromSimple();
});

const cronBuilderDisplayValue = computed(() => cronBuilderGenerated.value);

function parseCronIntoBuilder(current: string) {
  let raw = current.trim();
  if (!raw) raw = '0 */5 * * * *';
  const parts = raw.split(/\s+/);
  if (parts.length === 5) raw = '0 ' + raw;
  const normalized = raw.split(/\s+/);
  if (normalized.length !== 6) {
    cronBuilderTab.value = 'simple';
    cronBuilderSelectedPreset.value = null;
    cronBuilderEveryN.value = 5;
    cronBuilderUnit.value = 'minute';
    cronBuilderCustomMode.value = 'every';
    cronBuilderRaw.value = '0 */5 * * * *';
    return;
  }
  const preset = cronPresets.find((p) => p.value === raw);
  if (preset) {
    cronBuilderTab.value = 'simple';
    cronBuilderSelectedPreset.value = preset.value;
    cronBuilderCustomMode.value = 'every';
    return;
  }
  const [sec, min, hour] = normalized.map((x) => x.replace(/^\*\/?/, ''));
  const everySec = raw.match(/^\*\/(\d+) \* \* \* \* \*$/);
  const everyMin = raw.match(/^0 \*\/(\d+) \* \* \* \*$/);
  const everyHour = raw.match(/^0 0 \*\/(\d+) \* \* \*$/);
  if (everySec) {
    cronBuilderTab.value = 'simple';
    cronBuilderSelectedPreset.value = null;
    cronBuilderCustomMode.value = 'every';
    cronBuilderUnit.value = 'second';
    cronBuilderEveryN.value = Math.max(1, Math.min(59, parseInt(everySec[1], 10) || 5));
    return;
  }
  if (everyMin) {
    cronBuilderTab.value = 'simple';
    cronBuilderSelectedPreset.value = null;
    cronBuilderCustomMode.value = 'every';
    cronBuilderUnit.value = 'minute';
    cronBuilderEveryN.value = Math.max(1, Math.min(59, parseInt(everyMin[1], 10) || 5));
    return;
  }
  if (everyHour) {
    cronBuilderTab.value = 'simple';
    cronBuilderSelectedPreset.value = null;
    cronBuilderCustomMode.value = 'every';
    cronBuilderUnit.value = 'hour';
    cronBuilderEveryN.value = Math.max(1, Math.min(23, parseInt(everyHour[1], 10) || 1));
    return;
  }
  const daily6 = raw.match(/^0 (\d+) (\d+) \* \* \*$/);
  const weekly6 = raw.match(/^0 (\d+) (\d+) \* \* (\d+)$/);
  if (daily6) {
    cronBuilderTab.value = 'simple';
    cronBuilderSelectedPreset.value = null;
    cronBuilderCustomMode.value = 'daily';
    cronBuilderDailyMinute.value = parseInt(daily6[1], 10) || 0;
    cronBuilderDailyHour.value = parseInt(daily6[2], 10) || 0;
    return;
  }
  if (weekly6) {
    cronBuilderTab.value = 'simple';
    cronBuilderSelectedPreset.value = null;
    cronBuilderCustomMode.value = 'weekly';
    cronBuilderDailyMinute.value = parseInt(weekly6[1], 10) || 0;
    cronBuilderDailyHour.value = parseInt(weekly6[2], 10) || 0;
    cronBuilderWeeklyDay.value = Math.max(0, Math.min(6, parseInt(weekly6[4], 10) || 0));
    return;
  }
  cronBuilderTab.value = 'advanced';
  cronBuilderRaw.value = raw;
}

function openCronBuilder(source: 'period' | 'engine' = 'period') {
  cronBuilderTarget.value = source;
  const current = (source === 'period' ? (periodForm.value.expression ?? '') : (engineForm.value.sendSchedule ?? '')).trim() || '0 */5 * * * *';
  parseCronIntoBuilder(current);
  cronBuilderOpen.value = true;
}

function selectCronPreset(value: string) {
  cronBuilderSelectedPreset.value = value;
  cronBuilderCustomMode.value = 'every';
}

function applyCronBuilder() {
  const value = cronBuilderDisplayValue.value;
  if (cronBuilderTarget.value === 'period') periodForm.value.expression = value;
  else engineForm.value.sendSchedule = value;
  cronBuilderOpen.value = false;
}

const cronUnitOptions = computed(() => [
  { value: 'second' as const, title: mt('monitoring.cronBuilder.unitSecond', 'Saniye') },
  { value: 'minute' as const, title: mt('monitoring.cronBuilder.unitMinute', 'Dakika') },
  { value: 'hour' as const, title: mt('monitoring.cronBuilder.unitHour', 'Saat') },
]);
const weekdayCronOptions = computed(() => [
  { value: 0, title: mt('monitoring.cronBuilder.weekday0', 'Pazar') },
  { value: 1, title: mt('monitoring.cronBuilder.weekday1', 'Pazartesi') },
  { value: 2, title: mt('monitoring.cronBuilder.weekday2', 'Salı') },
  { value: 3, title: mt('monitoring.cronBuilder.weekday3', 'Çarşamba') },
  { value: 4, title: mt('monitoring.cronBuilder.weekday4', 'Perşembe') },
  { value: 5, title: mt('monitoring.cronBuilder.weekday5', 'Cuma') },
  { value: 6, title: mt('monitoring.cronBuilder.weekday6', 'Cumartesi') },
]);

// --- Schedules ---
const scheduleSearch = ref('');
const scheduleFormOpen = ref(false);
const scheduleFormModel = ref<MonSchedule | null>(null);
const scheduleDeleteTarget = ref<MonSchedule | null>(null);
const scheduleDeleteDialogOpen = ref(false);
const scheduleForm = ref<Partial<MonSchedule>>({ name: '', description: null, type: 'always', config: null });
const scheduleTypeItems = computed(() => [
  { title: mt('monitoring.schedules.typeAlways', 'Sürekli (7/24)'), value: 'always' },
  { title: mt('monitoring.schedules.typeScheduled', 'Zamanlanmış'), value: 'scheduled' },
]);
const weekdayLabels: Record<number, string> = {
  0: 'Paz', 1: 'Pzt', 2: 'Sal', 3: 'Çar', 4: 'Per', 5: 'Cum', 6: 'Cmt',
};
const scheduleHeaders = computed(() => [
  { title: mt('monitoring.schedules.tableName', 'Ad'), key: 'name', sortable: true },
  { title: mt('monitoring.schedules.tableType', 'Tip'), key: 'type', sortable: false },
  { title: mt('monitoring.schedules.tableDescription', 'Açıklama'), key: 'description', sortable: false },
  { title: mt('monitoring.schedules.tableActions', 'İşlemler'), key: 'actions', sortable: false, align: 'end' as const },
]);
const filteredSchedules = computed(() => {
  const q = scheduleSearch.value.toLowerCase().trim();
  if (!q) return store.schedules;
  return store.schedules.filter(
    (s) =>
      (s.name ?? '').toLowerCase().includes(q) ||
      (s.type ?? '').toLowerCase().includes(q) ||
      (s.description ?? '').toLowerCase().includes(q)
  );
});

// --- Engines ---
const engineSearch = ref('');
const engineFormOpen = ref(false);
const engineFormModel = ref<MonEngine | null>(null);
const engineDeleteTarget = ref<MonEngine | null>(null);
const engineDeleteDialogOpen = ref(false);
const engineFormRef = ref<{ validate: () => Promise<{ valid: boolean }>; resetValidation?: () => void } | null>(null);
const engineForm = ref<Partial<MonEngine>>({
  name: '', description: null, status: 'active', username: '', password: '',
  sendSchedule: '0 */5 * * *', configSyncPeriodMinutes: 10,
});
const engineRequiredRule = (v: string | number | null | undefined) => !!String(v ?? '').trim() || mt('monitoring.engines.validationRequired', 'Bu alan zorunludur');
const statusItems = computed(() => [
  { title: mt('monitoring.engines.statusActive', 'Aktif'), value: 'active' },
  { title: mt('monitoring.engines.statusInactive', 'Pasif'), value: 'inactive' },
  { title: mt('monitoring.engines.statusMaintenance', 'Bakımda'), value: 'maintenance' },
]);
const engineHeaders = computed(() => [
  { title: mt('monitoring.engines.tableName', 'Ad'), key: 'name', sortable: true },
  { title: mt('monitoring.engines.tableStatus', 'Durum'), key: 'status', sortable: false },
  { title: mt('monitoring.engines.tableHealth', 'Sağlık'), key: 'health', sortable: false },
  { title: mt('monitoring.engines.tableHostAddress', 'IP'), key: 'hostAddress', sortable: false },
  { title: mt('monitoring.engines.tableLastSeenAt', 'Son görülme'), key: 'lastSeenAt', sortable: false },
  { title: mt('monitoring.engines.tableErrors', 'Hatalar'), key: 'errors', sortable: false },
  { title: mt('monitoring.engines.tableActions', 'İşlemler'), key: 'actions', sortable: false, align: 'end' as const },
]);
const filteredEngines = computed(() => {
  const q = engineSearch.value.toLowerCase().trim();
  if (!q) return store.engines;
  return store.engines.filter(
    (e) => (e.name ?? '').toLowerCase().includes(q) || (e.status ?? '').toLowerCase().includes(q)
  );
});

// --- Agents ---
const agentSearch = ref('');
const agentFormOpen = ref(false);
const agentFormModel = ref<MonAgent | null>(null);
const agentDeleteTarget = ref<MonAgent | null>(null);
const agentDeleteDialogOpen = ref(false);
const assetOptions = ref<Array<{ title: string; value: string }>>([]);
const agentForm = ref<Partial<MonAgent>>({
  name: '', description: null, status: 'active', engineId: '',
  defaultPeriodId: null, defaultScheduleId: null, asset_configs: [],
});
const engineNameById = computed(() => {
  const m = new Map<string, string>();
  store.engines.forEach((e) => m.set(e.__dataId, e.name));
  return m;
});
const agentHeaders = computed(() => [
  { title: mt('monitoring.agents.tableName', 'Ad'), key: 'name', sortable: true },
  { title: mt('monitoring.agents.tableEngine', 'Engine'), key: 'engineName', sortable: false },
  { title: mt('monitoring.agents.tableStatus', 'Durum'), key: 'status', sortable: false },
  { title: mt('monitoring.agents.tableAssetCount', 'Asset sayısı'), key: 'assetCount', sortable: false },
  { title: mt('monitoring.agents.tableActions', 'İşlemler'), key: 'actions', sortable: false, align: 'end' as const },
]);
const filteredAgents = computed(() => {
  const q = agentSearch.value.toLowerCase().trim();
  if (!q) return store.agents;
  return store.agents.filter(
    (a) =>
      (a.name ?? '').toLowerCase().includes(q) ||
      (engineNameById.value.get(a.engineId) ?? '').toLowerCase().includes(q) ||
      (a.status ?? '').toLowerCase().includes(q)
  );
});
const agentsWithMeta = computed(() =>
  filteredAgents.value.map((a) => ({
    ...a,
    engineName: engineNameById.value.get(a.engineId) ?? a.engineId,
    assetCount: (a.asset_configs ?? []).filter((c) => c.active).length,
  }))
);

// Helpers
function truncate(s: string | null | undefined, max = 40) {
  if (!s) return '—';
  return s.length <= max ? s : s.slice(0, max) + '…';
}
const scheduleTypeLabel = (type: string) => scheduleTypeItems.value.find((t) => t.value === type)?.title ?? type;
const statusLabel = (v: string) => statusItems.value.find((s) => s.value === v)?.title ?? v;
function formatLastSeen(v: string | null | undefined) {
  if (!v) return '—';
  try {
    const d = new Date(v);
    return Number.isNaN(d.getTime()) ? v : d.toLocaleString('tr-TR');
  } catch {
    return v;
  }
}
function getScheduleConfig() {
  if (!scheduleForm.value.config || typeof scheduleForm.value.config !== 'object') {
    scheduleForm.value.config = { weekdays: [1, 2, 3, 4, 5], startTime: '08:00', endTime: '18:00' };
  }
  return scheduleForm.value.config as { weekdays?: number[]; startTime?: string; endTime?: string };
}
function toggleScheduleWeekday(w: number) {
  const c = getScheduleConfig();
  const arr = c.weekdays ?? [];
  const idx = arr.indexOf(w);
  const newWeekdays = idx >= 0 ? arr.filter((_, i) => i !== idx) : [...arr, w].sort((a, b) => a - b);
  scheduleForm.value.config = { ...c, weekdays: newWeekdays };
}

onMounted(async () => {
  applyTabFromQuery();
  await store.loadAll();
  try {
    const res = await fetchFromDataGateway('/api/v1/data/mon_assets?limit=2000');
    const raw = Array.isArray(res) ? res : (res?.items ?? res?.data ?? []);
    const id = (r: any) => r?.__dataId ?? r?.dataId ?? '';
    const name = (r: any) => r?.name ?? r?.Name ?? id(r);
    assetOptions.value = raw.map((r: any) => ({ title: name(r), value: id(r) })).filter((o: any) => o.value);
  } catch {
    assetOptions.value = [];
  }
});

async function refresh() {
  await store.loadAll();
  try {
    const res = await fetchFromDataGateway('/api/v1/data/mon_assets?limit=2000');
    const raw = Array.isArray(res) ? res : (res?.items ?? res?.data ?? []);
    const id = (r: any) => r?.__dataId ?? r?.dataId ?? '';
    const name = (r: any) => r?.name ?? r?.Name ?? id(r);
    assetOptions.value = raw.map((r: any) => ({ title: name(r), value: id(r) })).filter((o: any) => o.value);
  } catch {
    assetOptions.value = [];
  }
}

// --- Period actions ---
function periodOpenNew() {
  periodFormModel.value = null;
  periodForm.value = { name: '', description: null, expression: '*/5 * * * *' };
  periodFormOpen.value = true;
}
function periodOpenEdit(item: MonCollectionPeriod) {
  periodFormModel.value = item;
  periodForm.value = { name: item.name, description: item.description ?? null, expression: item.expression };
  periodFormOpen.value = true;
}
function periodCloseForm() {
  periodFormOpen.value = false;
  periodFormModel.value = null;
}
async function periodSave() {
  const name = (periodForm.value.name ?? '').trim();
  const expression = (periodForm.value.expression ?? '').trim();
  if (!name || !expression) return;
  const id = (periodFormModel.value as any)?.__dataId;
  if (id) await store.updatePeriod(id, { ...periodForm.value, name, expression });
  else await store.createPeriod({ ...periodForm.value, name, expression });
  periodCloseForm();
}
function periodOpenDelete(item: MonCollectionPeriod) {
  periodDeleteTarget.value = item;
  periodDeleteDialogOpen.value = true;
}
async function periodConfirmDelete() {
  if (!periodDeleteTarget.value) return;
  await store.deletePeriod(periodDeleteTarget.value.__dataId);
  periodDeleteTarget.value = null;
  periodDeleteDialogOpen.value = false;
}

// --- Schedule actions ---
function scheduleOpenNew() {
  scheduleFormModel.value = null;
  scheduleForm.value = { name: '', description: null, type: 'always', config: null };
  scheduleFormOpen.value = true;
}
function scheduleOpenEdit(item: MonSchedule) {
  scheduleFormModel.value = item;
  scheduleForm.value = {
    name: item.name,
    description: item.description ?? null,
    type: item.type,
    config: item.config ? { ...item.config } : null,
  };
  scheduleFormOpen.value = true;
}
function scheduleCloseForm() {
  scheduleFormOpen.value = false;
  scheduleFormModel.value = null;
}
async function scheduleSave() {
  const name = (scheduleForm.value.name ?? '').trim();
  const type = scheduleForm.value.type ?? 'always';
  if (!name) return;
  const payload: Partial<MonSchedule> = {
    name,
    description: (scheduleForm.value.description ?? '').trim() || null,
    type,
    config: type === 'scheduled' ? getScheduleConfig() : null,
  };
  const id = (scheduleFormModel.value as any)?.__dataId;
  if (id) await store.updateSchedule(id, payload);
  else await store.createSchedule(payload);
  scheduleCloseForm();
}
function scheduleOpenDelete(item: MonSchedule) {
  scheduleDeleteTarget.value = item;
  scheduleDeleteDialogOpen.value = true;
}
async function scheduleConfirmDelete() {
  if (!scheduleDeleteTarget.value) return;
  await store.deleteSchedule(scheduleDeleteTarget.value.__dataId);
  scheduleDeleteTarget.value = null;
  scheduleDeleteDialogOpen.value = false;
}

// --- Engine actions ---
function engineOpenNew() {
  engineFormModel.value = null;
  engineForm.value = {
    name: '', description: null, status: 'active', username: '', password: '',
    sendSchedule: '0 */5 * * *', configSyncPeriodMinutes: 10,
  };
  engineFormOpen.value = true;
  nextTick(() => engineFormRef.value?.resetValidation?.());
}
function engineOpenEdit(item: MonEngine) {
  engineFormModel.value = item;
  engineForm.value = {
    name: item.name, description: item.description ?? null, status: item.status,
    username: item.username, password: item.password, sendSchedule: item.sendSchedule,
    configSyncPeriodMinutes: item.configSyncPeriodMinutes ?? 10,
  };
  engineFormOpen.value = true;
  nextTick(() => engineFormRef.value?.resetValidation?.());
}
function engineCloseForm() {
  engineFormOpen.value = false;
  engineFormModel.value = null;
}
async function engineSave() {
  const { valid } = (await engineFormRef.value?.validate()) ?? { valid: false };
  if (!valid) return;
  const name = (engineForm.value.name ?? '').trim();
  const username = (engineForm.value.username ?? '').trim();
  const password = (engineForm.value.password ?? '').trim();
  const sendSchedule = (engineForm.value.sendSchedule ?? '').trim();
  const payload: Partial<MonEngine> = {
    name, description: (engineForm.value.description ?? '').trim() || null,
    status: engineForm.value.status ?? 'active', username, password, sendSchedule,
    configSyncPeriodMinutes: engineForm.value.configSyncPeriodMinutes ?? 10,
  };
  const id = (engineFormModel.value as any)?.__dataId;
  if (id) await store.updateEngine(id, payload);
  else await store.createEngine(payload);
  engineCloseForm();
}
function engineOpenDelete(item: MonEngine) {
  engineDeleteTarget.value = item;
  engineDeleteDialogOpen.value = true;
}
async function engineConfirmDelete() {
  if (!engineDeleteTarget.value) return;
  await store.deleteEngine(engineDeleteTarget.value.__dataId);
  engineDeleteTarget.value = null;
  engineDeleteDialogOpen.value = false;
}

// --- Config string (Reactor API) ---
const configStringModalOpen = ref(false);
const configStringEngineId = ref('');
const configStringEngineName = ref('');
const configStringValue = ref('');
const configStringLoading = ref(false);
const configStringError = ref('');
const configStringCopied = ref(false);

async function openConfigStringModal(engine: MonEngine) {
  configStringEngineId.value = engine.__dataId;
  configStringEngineName.value = engine.name ?? '';
  configStringValue.value = '';
  configStringError.value = '';
  configStringModalOpen.value = true;
  configStringLoading.value = true;
  try {
    const res = await $fetch<{ configString?: string }>('/api/reactor/v1/engine/config-string', {
      query: { engineId: engine.__dataId },
      credentials: 'include',
    });
    configStringValue.value = res?.configString ?? (res as any) ?? '';
    if (!configStringValue.value && typeof res === 'string') configStringValue.value = res;
  } catch (e: any) {
    configStringError.value = e?.data?.message ?? e?.message ?? mt('monitoring.engines.configStringError', 'Config string alınamadı.');
  } finally {
    configStringLoading.value = false;
  }
}

async function copyConfigStringToClipboard() {
  if (!configStringValue.value) return;
  try {
    await navigator.clipboard.writeText(configStringValue.value);
    configStringCopied.value = true;
    setTimeout(() => { configStringCopied.value = false; }, 2000);
  } catch {
    // fallback
  }
}

function closeConfigStringModal() {
  configStringModalOpen.value = false;
  configStringEngineId.value = '';
  configStringValue.value = '';
  configStringError.value = '';
}

// --- Agent actions ---
function agentOpenNew() {
  agentFormModel.value = null;
  agentForm.value = {
    name: '', description: null, status: 'active',
    engineId: store.engineOptions[0]?.value ?? '',
    defaultPeriodId: null, defaultScheduleId: null, asset_configs: [],
  };
  agentFormOpen.value = true;
}
function agentOpenEdit(item: MonAgent) {
  agentFormModel.value = item;
  agentForm.value = {
    name: item.name, description: item.description ?? null, status: item.status,
    engineId: item.engineId, defaultPeriodId: item.defaultPeriodId ?? null,
    defaultScheduleId: item.defaultScheduleId ?? null,
    asset_configs: (item.asset_configs ?? []).map((c) => ({ ...c })),
  };
  agentFormOpen.value = true;
}
function agentCloseForm() {
  agentFormOpen.value = false;
  agentFormModel.value = null;
}
function addAssetConfig() {
  const list = agentForm.value.asset_configs ?? [];
  agentForm.value.asset_configs = [...list, { assetId: '', periodId: null, scheduleId: null, active: true }];
}
function removeAssetConfig(index: number) {
  const list = [...(agentForm.value.asset_configs ?? [])];
  list.splice(index, 1);
  agentForm.value.asset_configs = list;
}
async function agentSave() {
  const name = (agentForm.value.name ?? '').trim();
  const engineId = (agentForm.value.engineId ?? '').trim();
  if (!name || !engineId) return;
  const configs = (agentForm.value.asset_configs ?? []).filter((c) => (c.assetId ?? '').trim());
  if (configs.length === 0) return;
  const payload: Partial<MonAgent> = {
    name, description: (agentForm.value.description ?? '').trim() || null,
    status: agentForm.value.status ?? 'active', engineId,
    defaultPeriodId: agentForm.value.defaultPeriodId || null,
    defaultScheduleId: agentForm.value.defaultScheduleId || null,
    asset_configs: configs.map((c) => ({
      assetId: (c.assetId ?? '').trim(),
      periodId: c.periodId || null,
      scheduleId: c.scheduleId || null,
      active: c.active ?? true,
      description: (c.description ?? '').trim() || null,
    })),
  };
  const id = (agentFormModel.value as any)?.__dataId;
  if (id) await store.updateAgent(id, payload);
  else await store.createAgent(payload);
  agentCloseForm();
}
function agentOpenDelete(item: MonAgent) {
  agentDeleteTarget.value = item;
  agentDeleteDialogOpen.value = true;
}
async function agentConfirmDelete() {
  if (!agentDeleteTarget.value) return;
  await store.deleteAgent(agentDeleteTarget.value.__dataId);
  agentDeleteTarget.value = null;
  agentDeleteDialogOpen.value = false;
}

// Slot adları (ESLint vue/valid-v-slot: nokta modifier kabul etmiyor)
const slotName = 'item.name';
const slotExpression = 'item.expression';
const slotDescription = 'item.description';
const slotActions = 'item.actions';
const slotType = 'item.type';
const slotStatus = 'item.status';
const slotHealth = 'item.health';
const slotHostAddress = 'item.hostAddress';
const slotLastSeenAt = 'item.lastSeenAt';
const slotErrors = 'item.errors';
const slotEngineName = 'item.engineName';
const slotAssetCount = 'item.assetCount';

function healthLabel(v: string | null | undefined) {
  const h = (v ?? 'ok').toLowerCase();
  if (h === 'degraded') return mt('monitoring.engines.healthDegraded', 'Bozuk');
  if (h === 'error') return mt('monitoring.engines.healthError', 'Hata');
  return mt('monitoring.engines.healthOk', 'İyi');
}
function healthColor(v: string | null | undefined) {
  const h = (v ?? 'ok').toLowerCase();
  if (h === 'error') return 'error';
  if (h === 'degraded') return 'warning';
  return 'success';
}
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
    <v-container fluid>
      <v-alert v-if="store.error" type="error" variant="tonal" dismissible class="mb-4" @click:close="store.clearError">
        {{ store.error }}
      </v-alert>

      <div class="d-flex align-center gap-2 mb-4">
        <v-btn variant="tonal" color="primary" size="small" to="/apps/monitoring/control">
          {{ mt('monitoring.control.pageTitle', 'Kontrol') }}
        </v-btn>
        <v-btn variant="outlined" size="small" to="/apps/monitoring/map">
          {{ mt('monitoring.map.pageTitle', 'Harita') }}
        </v-btn>
        <v-btn variant="outlined" size="small" to="/apps/monitoring/organization">
          {{ mt('organization.pageTitle', 'Organizasyon') }}
        </v-btn>
        <v-btn variant="outlined" size="small" to="/apps/monitoring/widgets">
          {{ mt('monitoring.widgets.pageTitle', 'Monitoring Widget\'ları') }}
        </v-btn>
        <v-btn variant="outlined" size="small" to="/apps/monitoring/config">
          {{ mt('monitoringConfig.title', 'İzleme Yapılandırması') }}
        </v-btn>
      </div>

      <v-tabs v-model="activeTab" class="mb-4">
        <v-tab value="periods">{{ mt('monitoring.collectionPeriods.tabTitle', 'Toplama periyotları') }}</v-tab>
        <v-tab value="schedules">{{ mt('monitoring.schedules.tabTitle', 'İzleme aralıkları') }}</v-tab>
        <v-tab value="engines">{{ mt('monitoring.engines.tabTitle', "Engine'ler") }}</v-tab>
        <v-tab value="agents">{{ mt('monitoring.agents.tabTitle', "Agent'lar") }}</v-tab>
      </v-tabs>

      <v-window v-model="activeTab">
        <!-- Periyotlar -->
        <v-window-item value="periods">
          <v-card>
            <v-card-text>
              <v-alert type="info" variant="tonal" density="comfortable" class="mb-4" border="start">
                {{ mt('monitoring.collectionPeriods.description', 'Toplama periyotları, izleme sisteminin metrikleri (sıcaklık, trafik, enerji vb.) ne sıklıkla toplayacağını tanımlar. Her periyot bir cron ifadesi ile "her 5 dakikada", "her saat başı" gibi tekrarlı zamanlamaları ifade eder. Agent\'lar bu periyotları kullanarak hangi asset\'lerden ne sıklıkla veri alınacağını belirler.') }}
              </v-alert>
              <div class="d-flex flex-wrap align-center gap-2 mb-4">
                <v-text-field v-model="periodSearch" :placeholder="mt('monitoring.common.searchPlaceholder', 'Ara...')" variant="outlined" density="compact" hide-details style="max-width: 260px;" />
                <v-spacer />
                <v-btn v-if="canEdit" color="primary" variant="flat" :disabled="store.loading" @click="periodOpenNew">
                  <PlusIcon size="20" class="mr-1" /> {{ mt('monitoring.collectionPeriods.newPeriod', 'Yeni periyot') }}
                </v-btn>
                <v-btn variant="outlined" :disabled="store.loading" @click="store.loadAll"> <RefreshIcon size="20" class="mr-1" /> {{ mt('monitoring.common.refresh', 'Yenile') }} </v-btn>
              </div>
              <v-data-table :headers="periodHeaders" :items="filteredPeriods" :loading="store.loading" item-value="__dataId" class="border rounded">
                <template #[slotName]="{ item }"><span class="font-weight-medium">{{ item.name }}</span></template>
                <template #[slotExpression]="{ item }"><code class="text-caption">{{ item.expression }}</code></template>
                <template #[slotDescription]="{ item }"><span class="text-body-2">{{ truncate(item.description, 50) }}</span></template>
                <template #[slotActions]="{ item }">
                  <v-btn v-if="canEdit" icon size="small" variant="text" @click="periodOpenEdit(item)"><EditIcon size="18" /></v-btn>
                  <v-btn v-if="canEdit" icon size="small" variant="text" color="error" @click="periodOpenDelete(item)"><TrashIcon size="18" /></v-btn>
                </template>
                <template #no-data><div class="text-center py-6 text-medium-emphasis">{{ mt('monitoring.collectionPeriods.noData', 'Henüz periyot tanımı yok.') }}</div></template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </v-window-item>

        <!-- Schedules -->
        <v-window-item value="schedules">
          <v-card>
            <v-card-text>
              <v-alert type="info" variant="tonal" density="comfortable" class="mb-4" border="start">
                {{ mt('monitoring.schedules.description', 'İzleme aralıkları, sistemin hangi zaman dilimlerinde izleme yapacağını tanımlar. "Sürekli (7/24)" ile veri toplama her zaman aktiftir; "Zamanlanmış" ile yalnızca belirlediğiniz günler ve saat aralığında toplama yapılır.') }}
              </v-alert>
              <div class="d-flex flex-wrap align-center gap-2 mb-4">
                <v-text-field v-model="scheduleSearch" :placeholder="mt('monitoring.common.searchPlaceholder', 'Ara...')" variant="outlined" density="compact" hide-details style="max-width: 260px;" />
                <v-spacer />
                <v-btn v-if="canEdit" color="primary" variant="flat" :disabled="store.loading" @click="scheduleOpenNew">
                  <PlusIcon size="20" class="mr-1" /> {{ mt('monitoring.schedules.newSchedule', 'Yeni izleme aralığı') }}
                </v-btn>
                <v-btn variant="outlined" :disabled="store.loading" @click="store.loadAll"> <RefreshIcon size="20" class="mr-1" /> {{ mt('monitoring.common.refresh', 'Yenile') }} </v-btn>
              </div>
              <v-data-table :headers="scheduleHeaders" :items="filteredSchedules" :loading="store.loading" item-value="__dataId" class="border rounded">
                <template #[slotName]="{ item }"><span class="font-weight-medium">{{ item.name }}</span></template>
                <template #[slotType]="{ item }">{{ scheduleTypeLabel(item.type) }}</template>
                <template #[slotDescription]="{ item }"><span class="text-body-2">{{ truncate(item.description, 50) }}</span></template>
                <template #[slotActions]="{ item }">
                  <v-btn v-if="canEdit" icon size="small" variant="text" @click="scheduleOpenEdit(item)"><EditIcon size="18" /></v-btn>
                  <v-btn v-if="canEdit" icon size="small" variant="text" color="error" @click="scheduleOpenDelete(item)"><TrashIcon size="18" /></v-btn>
                </template>
                <template #no-data><div class="text-center py-6 text-medium-emphasis">{{ mt('monitoring.schedules.noData', 'Henüz izleme aralığı tanımı yok.') }}</div></template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </v-window-item>

        <!-- Engines -->
        <v-window-item value="engines">
          <v-card>
            <v-card-text>
              <v-alert type="info" variant="tonal" density="comfortable" class="mb-4" border="start">
                {{ mt('monitoring.engines.description', "Engine'ler, izleme verisini toplayan ve MngReactor/veri ağ geçidine ileten çalışan servislerdir. Her engine için kimlik bilgileri, durum ve \"Veri gönderim cron\" ile toplanan verinin ne sıklıkla gönderileceği tanımlanır.") }}
              </v-alert>
              <div class="d-flex flex-wrap align-center gap-2 mb-4">
                <v-text-field v-model="engineSearch" :placeholder="mt('monitoring.common.searchPlaceholder', 'Ara...')" variant="outlined" density="compact" hide-details style="max-width: 260px;" />
                <v-spacer />
                <v-btn v-if="canEdit" color="primary" variant="flat" :disabled="store.loading" @click="engineOpenNew">
                  <PlusIcon size="20" class="mr-1" /> {{ mt('monitoring.engines.newEngine', 'Yeni engine') }}
                </v-btn>
                <v-btn variant="outlined" :disabled="store.loading" @click="store.loadAll"> <RefreshIcon size="20" class="mr-1" /> {{ mt('monitoring.common.refresh', 'Yenile') }} </v-btn>
              </div>
              <v-data-table :headers="engineHeaders" :items="filteredEngines" :loading="store.loading" item-value="__dataId" class="border rounded" show-expand>
                <template #[slotName]="{ item }"><span class="font-weight-medium">{{ item.name }}</span></template>
                <template #[slotStatus]="{ item }">
                  <v-chip size="small" :color="item.status === 'active' ? 'success' : item.status === 'maintenance' ? 'warning' : 'default'">{{ statusLabel(item.status) }}</v-chip>
                </template>
                <template #[slotHealth]="{ item }">
                  <v-chip size="small" :color="healthColor(item.health)" variant="tonal">{{ healthLabel(item.health) }}</v-chip>
                </template>
                <template #[slotHostAddress]="{ item }"><span class="text-caption font-mono">{{ item.hostAddress || '—' }}</span></template>
                <template #[slotLastSeenAt]="{ item }"><span class="text-caption">{{ formatLastSeen(item.lastSeenAt) }}</span></template>
                <template #[slotErrors]="{ item }">
                  <template v-if="(item.lastErrors?.length ?? 0) > 0">
                    <v-chip size="small" color="warning" variant="tonal">{{ (item.lastErrors?.length ?? 0) }} {{ mt('monitoring.engines.errorsCount', 'hata') }}</v-chip>
                  </template>
                  <span v-else class="text-caption text-medium-emphasis">—</span>
                </template>
                <template #expanded-row="{ columns, item }">
                  <tr v-if="(item.lastErrors?.length ?? 0) > 0">
                    <td :colspan="columns.length">
                      <div class="pa-3">
                        <div class="text-caption text-medium-emphasis mb-2">{{ mt('monitoring.engines.lastErrorsTitle', 'Son toplama hataları') }}</div>
                        <v-list density="compact" class="bg-transparent">
                          <v-list-item v-for="(err, idx) in (item.lastErrors ?? []).slice(0, 10)" :key="idx" class="text-caption">
                            <template #prepend>
                              <v-icon size="16" color="error" class="mr-2">mdi-alert-circle</v-icon>
                            </template>
                            <v-list-item-title>{{ err.errorCode }}: {{ truncate(err.message, 60) }}</v-list-item-title>
                            <v-list-item-subtitle>{{ mt('monitoring.engines.errorAsset', 'Asset') }}: {{ err.assetId }} · {{ formatLastSeen(err.occurredAt) }}</v-list-item-subtitle>
                          </v-list-item>
                        </v-list>
                        <p v-if="(item.lastErrors?.length ?? 0) > 10" class="text-caption text-medium-emphasis mt-2">
                          {{ mt('monitoring.engines.moreErrors', '... ve {n} hata daha').replace('{n}', String((item.lastErrors?.length ?? 0) - 10)) }}
                        </p>
                      </div>
                    </td>
                  </tr>
                </template>
                <template #[slotActions]="{ item }">
                  <v-btn icon size="small" variant="text" :title="mt('monitoring.engines.configStringButton', 'Config string')" @click="openConfigStringModal(item)">
                    <KeyIcon size="18" />
                  </v-btn>
                  <v-btn v-if="canEdit" icon size="small" variant="text" @click="engineOpenEdit(item)"><EditIcon size="18" /></v-btn>
                  <v-btn v-if="canEdit" icon size="small" variant="text" color="error" @click="engineOpenDelete(item)"><TrashIcon size="18" /></v-btn>
                </template>
                <template #no-data><div class="text-center py-6 text-medium-emphasis">{{ mt('monitoring.engines.noData', 'Henüz engine tanımı yok.') }}</div></template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </v-window-item>

        <!-- Agents -->
        <v-window-item value="agents">
          <v-card>
            <v-card-text>
              <v-alert type="info" variant="tonal" density="comfortable" class="mb-4" border="start">
                {{ mt('monitoring.agents.description', "Agent'lar, bir engine'e bağlanarak belirli asset'lerden veri toplar. Her agent için engine, varsayılan toplama periyodu ve izleme aralığı seçilir.") }}
              </v-alert>
              <div class="d-flex flex-wrap align-center gap-2 mb-4">
                <v-text-field v-model="agentSearch" :placeholder="mt('monitoring.common.searchPlaceholder', 'Ara...')" variant="outlined" density="compact" hide-details style="max-width: 260px;" />
                <v-spacer />
                <v-btn v-if="canEdit" color="primary" variant="flat" :disabled="store.loading" @click="agentOpenNew">
                  <PlusIcon size="20" class="mr-1" /> {{ mt('monitoring.agents.newAgent', 'Yeni agent') }}
                </v-btn>
                <v-btn variant="outlined" :disabled="store.loading" @click="refresh"> <RefreshIcon size="20" class="mr-1" /> {{ mt('monitoring.common.refresh', 'Yenile') }} </v-btn>
              </div>
              <v-data-table :headers="agentHeaders" :items="agentsWithMeta" :loading="store.loading" item-value="__dataId" class="border rounded">
                <template #[slotName]="{ item }"><span class="font-weight-medium">{{ item.name }}</span></template>
                <template #[slotEngineName]="{ item }">{{ item.engineName }}</template>
                <template #[slotStatus]="{ item }">
                  <v-chip size="small" :color="item.status === 'active' ? 'success' : item.status === 'maintenance' ? 'warning' : 'default'">{{ statusLabel(item.status) }}</v-chip>
                </template>
                <template #[slotAssetCount]="{ item }">{{ item.assetCount }}</template>
                <template #[slotActions]="{ item }">
                  <v-btn v-if="canEdit" icon size="small" variant="text" @click="agentOpenEdit(item)"><EditIcon size="18" /></v-btn>
                  <v-btn v-if="canEdit" icon size="small" variant="text" color="error" @click="agentOpenDelete(item)"><TrashIcon size="18" /></v-btn>
                </template>
                <template #no-data><div class="text-center py-6 text-medium-emphasis">{{ mt('monitoring.agents.noData', 'Henüz agent tanımı yok.') }}</div></template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </v-window-item>
      </v-window>

      <!-- Period form dialog -->
      <v-dialog v-model="periodFormOpen" max-width="500" persistent>
        <v-card>
          <v-card-title>{{ periodFormModel ? mt('monitoring.collectionPeriods.editPeriod', 'Periyot düzenle') : mt('monitoring.collectionPeriods.newPeriod', 'Yeni periyot') }}</v-card-title>
          <v-card-text>
            <v-text-field v-model="periodForm.name" :label="mt('monitoring.collectionPeriods.nameLabel', 'Ad') + ' *'" variant="outlined" density="comfortable" class="mb-3" hide-details />
            <div class="d-flex align-center gap-2 mb-3">
              <v-text-field v-model="periodForm.expression" :label="mt('monitoring.collectionPeriods.cronLabel', 'Cron ifadesi') + ' *'" :placeholder="mt('monitoring.collectionPeriods.cronPlaceholder', '0 */5 * * * *')" variant="outlined" density="comfortable" hide-details class="flex-grow-1" />
              <v-btn variant="outlined" color="primary" :title="mt('monitoring.collectionPeriods.cronHelperButton', 'Cron oluştur')" @click="openCronBuilder">
                <CalendarEventIcon size="20" />
              </v-btn>
            </div>
            <v-textarea v-model="periodForm.description" :label="mt('monitoring.collectionPeriods.descriptionLabel', 'Açıklama')" variant="outlined" rows="2" hide-details />
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="periodCloseForm">{{ mt('monitoring.common.cancel', 'İptal') }}</v-btn>
            <v-btn color="primary" variant="flat" :loading="store.loading" @click="periodSave">{{ mt('monitoring.common.save', 'Kaydet') }}</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <!-- Cron builder modal (6 alanlı Quartz) -->
      <v-dialog v-model="cronBuilderOpen" max-width="560" persistent scrollable>
        <v-card>
          <v-card-title class="d-flex align-center">
            <CalendarEventIcon size="24" class="mr-2" />
            {{ mt('monitoring.cronBuilder.title', 'Cron ifadesi oluştur') }}
          </v-card-title>
          <v-tabs v-model="cronBuilderTab" density="compact" class="px-3">
            <v-tab value="simple">{{ mt('monitoring.cronBuilder.tabSimple', 'Basit periyot') }}</v-tab>
            <v-tab value="advanced">{{ mt('monitoring.cronBuilder.tabAdvanced', 'Karma ifadeler') }}</v-tab>
          </v-tabs>
          <v-card-text class="pt-2">
            <template v-if="cronBuilderTab === 'simple'">
              <div class="text-subtitle-2 text-medium-emphasis mb-2">{{ mt('monitoring.cronBuilder.presetLabel', 'Hazır periyotlar') }}</div>
              <div class="d-flex flex-wrap gap-2 mb-3">
                <v-chip
                  v-for="p in cronPresets"
                  :key="p.value"
                  :color="cronBuilderSelectedPreset === p.value ? 'primary' : undefined"
                  variant="flat"
                  size="small"
                  class="cursor-pointer"
                  @click="selectCronPreset(p.value)"
                >
                  {{ mt(p.labelKey, p.value) }}
                </v-chip>
              </div>
              <v-divider class="my-3" />
              <div class="text-subtitle-2 text-medium-emphasis mb-2">{{ mt('monitoring.cronBuilder.customLabel', 'Özel zamanlama') }}</div>
              <div class="d-flex align-center gap-2 flex-wrap mb-2">
                <span class="text-body-2">{{ mt('monitoring.cronBuilder.every', 'Her') }}</span>
                <v-text-field
                  v-model.number="cronBuilderEveryN"
                  type="number"
                  min="1"
                  :max="cronBuilderUnit === 'hour' ? 23 : 59"
                  density="compact"
                  hide-details
                  style="max-width: 72px;"
                  @update:model-value="cronBuilderSelectedPreset = null"
                />
                <v-select
                  v-model="cronBuilderUnit"
                  :items="cronUnitOptions"
                  item-title="title"
                  item-value="value"
                  density="compact"
                  hide-details
                  style="max-width: 120px;"
                  @update:model-value="cronBuilderSelectedPreset = null"
                />
              </div>
              <p class="text-caption text-medium-emphasis mb-2">{{ mt('monitoring.cronBuilder.simpleIntervalHint', 'Periyot için aşağıda \'Her N (yukarıdaki)\' seçili olsun.') }}</p>
              <div class="text-caption text-medium-emphasis mb-2">{{ mt('monitoring.cronBuilder.dayWeekBlock', 'Gün / hafta') }}</div>
              <v-radio-group v-model="cronBuilderCustomMode" inline density="compact" hide-details class="mb-2" @update:model-value="cronBuilderSelectedPreset = null">
                <v-radio :label="mt('monitoring.cronBuilder.everyLabel', 'Her N (yukarıdaki)')" value="every" />
                <v-radio :label="mt('monitoring.cronBuilder.dailyLabel', 'Her gün belirli saatte')" value="daily" />
                <v-radio :label="mt('monitoring.cronBuilder.weeklyLabel', 'Haftanın belirli günü')" value="weekly" />
              </v-radio-group>
              <template v-if="cronBuilderCustomMode === 'daily'">
                <div class="d-flex align-center gap-2 flex-wrap">
                  <span class="text-body-2">{{ mt('monitoring.cronBuilder.at', 'Saat') }}</span>
                  <v-text-field v-model.number="cronBuilderDailyHour" type="number" min="0" max="23" density="compact" hide-details style="max-width: 70px;" @update:model-value="cronBuilderSelectedPreset = null" />
                  <span class="text-body-2">:</span>
                  <v-text-field v-model.number="cronBuilderDailyMinute" type="number" min="0" max="59" density="compact" hide-details style="max-width: 70px;" @update:model-value="cronBuilderSelectedPreset = null" />
                </div>
              </template>
              <template v-else-if="cronBuilderCustomMode === 'weekly'">
                <div class="d-flex align-center gap-2 flex-wrap">
                  <v-select v-model="cronBuilderWeeklyDay" :items="weekdayCronOptions" item-title="title" item-value="value" density="compact" hide-details style="max-width: 140px;" @update:model-value="cronBuilderSelectedPreset = null" />
                  <span class="text-body-2">{{ mt('monitoring.cronBuilder.at', 'Saat') }}</span>
                  <v-text-field v-model.number="cronBuilderDailyHour" type="number" min="0" max="23" density="compact" hide-details style="max-width: 70px;" />
                  <span class="text-body-2">:</span>
                  <v-text-field v-model.number="cronBuilderDailyMinute" type="number" min="0" max="59" density="compact" hide-details style="max-width: 70px;" />
                </div>
              </template>
            </template>
            <template v-else>
              <p class="text-body-2 text-medium-emphasis mb-2">{{ mt('monitoring.cronBuilder.advancedHint', '6 alanlı cron (saniye dakika saat gün ay haftanın_günü). İlerde belirli gün/saat sihirbazı eklenecek.') }}</p>
              <v-text-field v-model="cronBuilderRaw" :placeholder="'0 */5 * * * *'" variant="outlined" density="compact" hide-details />
            </template>
            <v-alert type="info" variant="tonal" density="compact" class="mt-4">
              <span class="text-caption text-medium-emphasis">{{ mt('monitoring.cronBuilder.resultLabel', 'Oluşan cron ifadesi') }}:</span>
              <code class="d-block mt-1 font-weight-medium">{{ cronBuilderDisplayValue }}</code>
            </v-alert>
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="cronBuilderOpen = false">{{ mt('monitoring.cronBuilder.cancel', 'İptal') }}</v-btn>
            <v-btn color="primary" variant="flat" @click="applyCronBuilder">{{ mt('monitoring.cronBuilder.apply', 'Uygula') }}</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>
      <v-dialog v-model="periodDeleteDialogOpen" max-width="440" persistent>
        <v-card v-if="periodDeleteTarget">
          <v-card-title>{{ mt('monitoring.collectionPeriods.deleteTitle', 'Periyotu sil') }}</v-card-title>
          <v-card-text>{{ mtParam('monitoring.collectionPeriods.deleteConfirm', { name: periodDeleteTarget.name ?? '' }, '"' + (periodDeleteTarget.name ?? '') + '" periyot tanımını silmek istediğinize emin misiniz?') }}</v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="periodDeleteDialogOpen = false; periodDeleteTarget = null">{{ mt('monitoring.common.cancel', 'İptal') }}</v-btn>
            <v-btn color="error" variant="flat" :loading="store.loading" @click="periodConfirmDelete">{{ mt('monitoring.common.delete', 'Sil') }}</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <!-- Schedule form dialog -->
      <v-dialog v-model="scheduleFormOpen" max-width="520" persistent>
        <v-card>
          <v-card-title>{{ scheduleFormModel ? mt('monitoring.schedules.editSchedule', 'İzleme aralığı düzenle') : mt('monitoring.schedules.newSchedule', 'Yeni izleme aralığı') }}</v-card-title>
          <v-card-text>
            <v-text-field v-model="scheduleForm.name" :label="mt('monitoring.schedules.nameLabel', 'Ad') + ' *'" variant="outlined" density="comfortable" class="mb-3" hide-details />
            <v-select v-model="scheduleForm.type" :items="scheduleTypeItems" item-title="title" item-value="value" :label="mt('monitoring.schedules.typeLabel', 'Tip')" variant="outlined" density="comfortable" class="mb-3" hide-details />
            <template v-if="scheduleForm.type === 'scheduled'">
              <div class="text-caption text-medium-emphasis mb-1">{{ mt('monitoring.schedules.weekdaysLabel', 'Çalışılacak günler') }}</div>
              <div class="d-flex flex-wrap gap-2 mb-2">
                <v-chip
                  v-for="w in [0,1,2,3,4,5,6]"
                  :key="w"
                  :color="(getScheduleConfig().weekdays ?? []).includes(Number(w)) ? 'primary' : undefined"
                  variant="flat"
                  size="small"
                  class="cursor-pointer"
                  @click="toggleScheduleWeekday(Number(w))"
                >
                  {{ weekdayLabels[Number(w)] }}
                </v-chip>
              </div>
              <v-row dense>
                <v-col cols="6">
                  <v-text-field :model-value="getScheduleConfig().startTime" @update:model-value="(v) => (getScheduleConfig().startTime = v ?? '08:00')" :label="mt('monitoring.schedules.startTimeLabel', 'Başlangıç (HH:mm)')" variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6">
                  <v-text-field :model-value="getScheduleConfig().endTime" @update:model-value="(v) => (getScheduleConfig().endTime = v ?? '18:00')" :label="mt('monitoring.schedules.endTimeLabel', 'Bitiş (HH:mm)')" variant="outlined" density="compact" hide-details />
                </v-col>
              </v-row>
            </template>
            <v-textarea v-model="scheduleForm.description" :label="mt('monitoring.schedules.descriptionLabel', 'Açıklama')" variant="outlined" rows="2" class="mt-3" hide-details />
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="scheduleCloseForm">{{ mt('monitoring.common.cancel', 'İptal') }}</v-btn>
            <v-btn color="primary" variant="flat" :loading="store.loading" @click="scheduleSave">{{ mt('monitoring.common.save', 'Kaydet') }}</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>
      <v-dialog v-model="scheduleDeleteDialogOpen" max-width="440" persistent>
        <v-card v-if="scheduleDeleteTarget">
          <v-card-title>{{ mt('monitoring.schedules.deleteTitle', 'İzleme aralığını sil') }}</v-card-title>
          <v-card-text>{{ mtParam('monitoring.schedules.deleteConfirm', { name: scheduleDeleteTarget.name ?? '' }, '"' + (scheduleDeleteTarget.name ?? '') + '" izleme aralığı tanımını silmek istediğinize emin misiniz?') }}</v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="scheduleDeleteDialogOpen = false; scheduleDeleteTarget = null">{{ mt('monitoring.common.cancel', 'İptal') }}</v-btn>
            <v-btn color="error" variant="flat" :loading="store.loading" @click="scheduleConfirmDelete">{{ mt('monitoring.common.delete', 'Sil') }}</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <!-- Engine form dialog -->
      <v-dialog v-model="engineFormOpen" max-width="560" persistent>
        <v-card>
          <v-card-title>{{ engineFormModel ? mt('monitoring.engines.editEngine', 'Engine düzenle') : mt('monitoring.engines.newEngine', 'Yeni engine') }}</v-card-title>
          <v-form ref="engineFormRef" @submit.prevent="engineSave">
            <v-card-text>
              <v-text-field v-model="engineForm.name" :label="mt('monitoring.engines.nameLabel', 'Engine adı') + ' *'" variant="outlined" density="comfortable" class="mb-3" :rules="[engineRequiredRule]" />
              <v-select v-model="engineForm.status" :items="statusItems" item-title="title" item-value="value" :label="mt('monitoring.engines.statusLabel', 'Durum')" variant="outlined" density="comfortable" class="mb-3" hide-details />
              <v-text-field v-model="engineForm.username" :label="mt('monitoring.engines.usernameLabel', 'Kullanıcı adı (auth)') + ' *'" variant="outlined" density="comfortable" class="mb-3" :rules="[engineRequiredRule]" />
              <v-text-field v-model="engineForm.password" :label="engineFormModel ? mt('monitoring.engines.passwordEditHint', 'Parola (değiştirmek için girin)') : mt('monitoring.engines.passwordLabel', 'Parola') + ' *'" type="password" variant="outlined" density="comfortable" class="mb-3" autocomplete="off" :rules="engineFormModel ? [] : [engineRequiredRule]" />
              <div class="d-flex align-center gap-2 mb-3">
                <v-text-field v-model="engineForm.sendSchedule" :label="mt('monitoring.engines.sendScheduleLabel', 'Veri gönderim cron') + ' *'" :placeholder="mt('monitoring.engines.sendSchedulePlaceholder', '0 */5 * * *')" variant="outlined" density="comfortable" class="flex-grow-1" :rules="[engineRequiredRule]" />
                <v-btn type="button" variant="outlined" color="primary" :title="mt('monitoring.engines.cronHelperButton', 'Cron oluştur')" @click="openCronBuilder('engine')">
                  <CalendarEventIcon size="20" />
                </v-btn>
              </div>
              <v-text-field v-model.number="engineForm.configSyncPeriodMinutes" :label="mt('monitoring.engines.configSyncPeriodLabel', 'Config sync (dakika)')" type="number" variant="outlined" density="comfortable" hide-details />
              <v-textarea v-model="engineForm.description" :label="mt('monitoring.engines.descriptionLabel', 'Açıklama')" variant="outlined" rows="2" class="mt-3" hide-details />
            </v-card-text>
            <v-card-actions>
              <v-spacer />
              <v-btn type="button" variant="text" @click="engineCloseForm">{{ mt('monitoring.common.cancel', 'İptal') }}</v-btn>
              <v-btn type="submit" color="primary" variant="flat" :loading="store.loading">{{ mt('monitoring.common.save', 'Kaydet') }}</v-btn>
            </v-card-actions>
          </v-form>
        </v-card>
      </v-dialog>
      <v-dialog v-model="engineDeleteDialogOpen" max-width="440" persistent>
        <v-card v-if="engineDeleteTarget">
          <v-card-title>{{ mt('monitoring.engines.deleteTitle', 'Engine sil') }}</v-card-title>
          <v-card-text>{{ mtParam('monitoring.engines.deleteConfirm', { name: engineDeleteTarget.name ?? '' }, '"' + (engineDeleteTarget.name ?? '') + '" engine tanımını silmek istediğinize emin misiniz?') }}</v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="engineDeleteDialogOpen = false; engineDeleteTarget = null">{{ mt('monitoring.common.cancel', 'İptal') }}</v-btn>
            <v-btn color="error" variant="flat" :loading="store.loading" @click="engineConfirmDelete">{{ mt('monitoring.common.delete', 'Sil') }}</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <!-- Config string modal -->
      <v-dialog v-model="configStringModalOpen" max-width="560" persistent>
        <v-card>
          <v-card-title class="d-flex align-center">
            <KeyIcon size="24" class="mr-2" />
            {{ mt('monitoring.engines.configStringTitle', 'Config string') }}
            <v-chip v-if="configStringEngineName" size="small" class="ml-2" variant="tonal">{{ configStringEngineName }}</v-chip>
          </v-card-title>
          <v-card-text>
            <p class="text-body-2 text-medium-emphasis mb-2">{{ mt('monitoring.engines.configStringHint', 'Bu metni kopyalayıp MngEngine uygulamasına yapıştırın.') }}</p>
            <v-progress-linear v-if="configStringLoading" indeterminate color="primary" class="mb-2" />
            <v-alert v-else-if="configStringError" type="error" variant="tonal" density="compact" class="mb-2">
              {{ configStringError }}
            </v-alert>
            <v-textarea
              v-else
              :model-value="configStringValue"
              readonly
              variant="outlined"
              rows="6"
              class="font-mono text-caption"
              hide-details
              auto-grow
            />
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="closeConfigStringModal">{{ mt('monitoring.common.cancel', 'İptal') }}</v-btn>
            <v-btn
              color="primary"
              variant="flat"
              :disabled="!configStringValue || configStringLoading"
              @click="copyConfigStringToClipboard"
            >
              {{ configStringCopied ? mt('monitoring.engines.configStringCopied', 'Kopyalandı') : mt('monitoring.engines.configStringCopy', 'Kopyala') }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <!-- Agent form dialog -->
      <v-dialog v-model="agentFormOpen" max-width="720" persistent scrollable>
        <v-card>
          <v-card-title>{{ agentFormModel ? mt('monitoring.agents.editAgent', 'Agent düzenle') : mt('monitoring.agents.newAgent', 'Yeni agent') }}</v-card-title>
          <v-card-text>
            <v-text-field v-model="agentForm.name" :label="mt('monitoring.agents.nameLabel', 'Agent adı') + ' *'" variant="outlined" density="comfortable" class="mb-3" hide-details />
            <v-select v-model="agentForm.engineId" :items="store.engineOptions" item-title="title" item-value="value" :label="mt('monitoring.agents.engineLabel', 'Engine') + ' *'" variant="outlined" density="comfortable" class="mb-3" hide-details />
            <v-select v-model="agentForm.defaultPeriodId" :items="[{ title: mt('monitoring.agents.defaultOption', '— Varsayılan —'), value: null }, ...store.periodOptions]" item-title="title" item-value="value" :label="mt('monitoring.agents.defaultPeriodLabel', 'Varsayılan periyot')" variant="outlined" density="comfortable" clearable class="mb-3" hide-details />
            <v-select v-model="agentForm.defaultScheduleId" :items="[{ title: mt('monitoring.agents.defaultOption', '— Varsayılan —'), value: null }, ...store.scheduleOptions]" item-title="title" item-value="value" :label="mt('monitoring.agents.defaultScheduleLabel', 'Varsayılan izleme aralığı')" variant="outlined" density="comfortable" clearable class="mb-3" hide-details />
            <v-select v-model="agentForm.status" :items="statusItems" item-title="title" item-value="value" :label="mt('monitoring.agents.statusLabel', 'Durum')" variant="outlined" density="comfortable" class="mb-4" hide-details />
            <div class="text-subtitle-2 text-medium-emphasis mb-2">{{ mt('monitoring.agents.assetConfigsLabel', 'Asset yapılandırmaları') }} *</div>
            <v-card v-if="(agentForm.asset_configs ?? []).length === 0" variant="outlined" class="pa-3 mb-3">
              <p class="text-caption text-medium-emphasis mb-2">{{ mt('monitoring.agents.emptyAssetHint', 'Bu agent için en az bir asset ekleyin.') }}</p>
              <v-btn size="small" variant="outlined" @click="addAssetConfig"><PlusIcon size="18" class="mr-1" /> {{ mt('monitoring.agents.addAsset', 'Asset ekle') }}</v-btn>
            </v-card>
            <template v-else>
              <div v-for="(cfg, idx) in agentForm.asset_configs" :key="idx" class="d-flex flex-wrap align-center gap-2 mb-2">
                <v-select v-model="cfg.assetId" :items="assetOptions" item-title="title" item-value="value" :label="mt('monitoring.agents.assetLabel', 'Asset')" variant="outlined" density="compact" hide-details style="min-width: 180px;" />
                <v-select v-model="cfg.periodId" :items="store.periodOptions" item-title="title" item-value="value" :label="mt('monitoring.agents.periodLabel', 'Periyot')" variant="outlined" density="compact" clearable hide-details style="min-width: 140px;" />
                <v-select v-model="cfg.scheduleId" :items="store.scheduleOptions" item-title="title" item-value="value" :label="mt('monitoring.agents.scheduleLabel', 'İzleme aralığı')" variant="outlined" density="compact" clearable hide-details style="min-width: 140px;" />
                <v-checkbox v-model="cfg.active" :label="mt('monitoring.agents.activeLabel', 'Aktif')" hide-details density="compact" class="shrink" />
                <v-btn icon size="small" variant="text" color="error" @click="removeAssetConfig(idx)"><TrashIcon size="18" /></v-btn>
              </div>
              <v-btn size="small" variant="outlined" class="mb-3" @click="addAssetConfig"><PlusIcon size="18" class="mr-1" /> {{ mt('monitoring.agents.addAsset', 'Asset ekle') }}</v-btn>
            </template>
            <v-textarea v-model="agentForm.description" :label="mt('monitoring.agents.descriptionLabel', 'Açıklama')" variant="outlined" rows="2" class="mt-2" hide-details />
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="agentCloseForm">{{ mt('monitoring.common.cancel', 'İptal') }}</v-btn>
            <v-btn color="primary" variant="flat" :loading="store.loading" @click="agentSave">{{ mt('monitoring.common.save', 'Kaydet') }}</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>
      <v-dialog v-model="agentDeleteDialogOpen" max-width="440" persistent>
        <v-card v-if="agentDeleteTarget">
          <v-card-title>{{ mt('monitoring.agents.deleteTitle', 'Agent sil') }}</v-card-title>
          <v-card-text>{{ mtParam('monitoring.agents.deleteConfirm', { name: agentDeleteTarget.name ?? '' }, '"' + (agentDeleteTarget.name ?? '') + '" agent tanımını silmek istediğinize emin misiniz?') }}</v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="agentDeleteDialogOpen = false; agentDeleteTarget = null">{{ mt('monitoring.common.cancel', 'İptal') }}</v-btn>
            <v-btn color="error" variant="flat" :loading="store.loading" @click="agentConfirmDelete">{{ mt('monitoring.common.delete', 'Sil') }}</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>
    </v-container>
  </div>
</template>
