<script setup lang="ts">
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';

definePageMeta({ layout: 'default' });

type Row = Record<string, unknown>;

const ZONE = 'Europe/Istanbul';
const auth = useAuthStore();
const config = useRuntimeConfig();

const day = ref(todayInIstanbul());
const locationKey = ref('');
const doorKey = ref('');
const person = ref('');
const result = ref('');
const loading = ref(false);
const errorText = ref('');
const rows = ref<Row[]>([]);
const locations = ref<Row[]>([]);
const doors = ref<Row[]>([]);

const breadcrumbs = [
  { text: 'AnkaraBT', disabled: false, href: '/dashboards/analytical' },
  { text: 'PDKS', disabled: true, href: '#' },
  { text: 'Gün listesi', disabled: true, href: '#' },
];

const resultItems = [
  { title: 'Hepsi', value: '' },
  { title: 'Kabul', value: 'accepted' },
  { title: 'Çift okuma', value: 'ignored' },
  { title: 'Red', value: 'rejected' },
];

const reasonLabels: Record<string, string> = {
  sure: 'Süre doldu',
  kullanilmis: 'Kod kullanılmış',
  yetki: 'Yetki yok',
  pasif: 'Kapı pasif',
  tanimsiz: 'Kapı tanımsız',
  bilinmiyor: 'Kod bilinmiyor',
};

const locationItems = computed(() => locations.value.map((row) => ({
  title: String(row.title || row.locationKey || ''),
  value: String(row.locationKey || ''),
})));

const doorItems = computed(() => {
  const locationId = locationKey.value
    ? idOf(locations.value.find((row) => String(row.locationKey) === locationKey.value) || {})
    : '';
  return doors.value
    .filter((row) => !locationId || row.lokasyon === locationId)
    .map((row) => ({
      title: String(row.title || row.doorKey || ''),
      value: String(row.doorKey || ''),
    }));
});

function todayInIstanbul() {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: ZONE,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date());
}

function zoneOffsetMs(instant: Date) {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: ZONE,
    hourCycle: 'h23',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).formatToParts(instant);
  const pick = (type: string) => Number(parts.find((part) => part.type === type)?.value || 0);
  const asUtc = Date.UTC(pick('year'), pick('month') - 1, pick('day'), pick('hour'), pick('minute'), pick('second'));
  return asUtc - instant.getTime();
}

function startOfDay(value: string) {
  const [year, month, date] = value.split('-').map(Number);
  const utcMidnight = new Date(Date.UTC(year, month - 1, date, 0, 0, 0));
  return new Date(utcMidnight.getTime() - zoneOffsetMs(utcMidnight));
}

function nextDay(value: string) {
  const [year, month, date] = value.split('-').map(Number);
  const next = new Date(Date.UTC(year, month - 1, date + 1));
  const y = next.getUTCFullYear();
  const m = String(next.getUTCMonth() + 1).padStart(2, '0');
  const d = String(next.getUTCDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function gateway() {
  return String(config.public.gatewayUrl || '').replace(/\/$/, '');
}

function idOf(row: Row) {
  return String(row.__dataId || row.dataId || '');
}

function asRows(payload: unknown): Row[] {
  if (Array.isArray(payload)) return payload as Row[];
  if (payload && typeof payload === 'object') {
    const data = (payload as { data?: unknown }).data;
    if (Array.isArray(data)) return data as Row[];
  }
  return [];
}

function messageOf(error: unknown) {
  const data = (error as { data?: { message?: string; error?: { message?: string } } })?.data;
  return data?.error?.message || data?.message || '';
}

function clock(value: unknown) {
  const text = String(value || '');
  const parsed = new Date(text);
  if (Number.isNaN(parsed.getTime())) return '';
  return new Intl.DateTimeFormat('tr-TR', {
    timeZone: ZONE,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(parsed);
}

function doorTitle(key: unknown) {
  const found = doors.value.find((row) => row.doorKey === key);
  return found ? String(found.title || found.doorKey || '') : String(key || '');
}

function locationTitle(key: unknown) {
  const found = locations.value.find((row) => row.locationKey === key);
  return found ? String(found.title || found.locationKey || '') : String(key || '');
}

function directionLabel(value: unknown) {
  if (value === 'in') return 'Giriş';
  if (value === 'out') return 'Çıkış';
  if (value === 'break') return 'Mola';
  if (value === 'return') return 'Dönüş';
  return String(value || '');
}

function resultLabel(value: unknown) {
  return resultItems.find((item) => item.value === value)?.title || String(value || '');
}

function reasonLabel(value: unknown) {
  const key = String(value || '');
  return key ? reasonLabels[key] || key : '';
}

async function dg(path: string) {
  return $fetch(`${gateway()}${path}`, {
    headers: { Authorization: `Bearer ${auth.accessToken}` },
  });
}

async function loadLookups() {
  const [locationPayload, doorPayload] = await Promise.all([
    dg('/data/api/v1/data/pdks_lokasyonlar?limit=200&expand=false'),
    dg('/data/api/v1/data/pdks_kapilar?limit=200&expand=false'),
  ]);
  locations.value = asRows(locationPayload);
  doors.value = asRows(doorPayload);
}

async function load() {
  loading.value = true;
  errorText.value = '';
  try {
    const start = startOfDay(day.value).toISOString();
    const end = startOfDay(nextDay(day.value)).toISOString();
    const clauses = [`occurredAt:gte:${start}`, `occurredAt:lt:${end}`];
    if (locationKey.value) clauses.push(`locationKey:eq:${locationKey.value}`);
    if (doorKey.value) clauses.push(`doorKey:eq:${doorKey.value}`);
    const name = person.value.trim();
    if (name) clauses.push(`displayName:contains:${name}`);
    if (result.value) clauses.push(`result:eq:${result.value}`);
    const filter = encodeURIComponent(clauses.join(','));
    const payload = await dg(`/data/api/v1/data/pdks_gecisler?limit=1000&expand=false&sort=-occurredAt&filter=${filter}`);
    rows.value = asRows(payload);
  } catch (error: unknown) {
    rows.value = [];
    errorText.value = messageOf(error) || 'Liste alınamadı.';
  } finally {
    loading.value = false;
  }
}

watch(locationKey, () => {
  if (doorKey.value && !doorItems.value.some((item) => item.value === doorKey.value)) doorKey.value = '';
});

onMounted(async () => {
  try {
    await loadLookups();
  } catch (error: unknown) {
    errorText.value = messageOf(error) || 'Kapılar alınamadı.';
  }
  await load();
});
</script>

<template>
  <div>
    <BaseBreadcrumb title="Gün listesi" :breadcrumbs="breadcrumbs" />
    <v-alert v-if="errorText" type="error" variant="tonal" class="mb-4">{{ errorText }}</v-alert>
    <v-card variant="outlined" class="rounded-lg">
      <div class="d-flex flex-wrap align-end ga-3 pa-4">
        <v-text-field v-model="day" type="date" label="Gün" density="comfortable" hide-details style="max-width: 180px" />
        <v-select
          v-model="locationKey"
          :items="locationItems"
          label="Lokasyon"
          density="comfortable"
          hide-details
          clearable
          style="min-width: 180px"
        />
        <v-select
          v-model="doorKey"
          :items="doorItems"
          label="Kapı"
          density="comfortable"
          hide-details
          clearable
          style="min-width: 180px"
        />
        <v-text-field v-model="person" label="Kişi" density="comfortable" hide-details style="max-width: 180px" @keyup.enter="load" />
        <v-select
          v-model="result"
          :items="resultItems"
          label="Sonuç"
          density="comfortable"
          hide-details
          style="max-width: 160px"
        />
        <v-btn color="primary" class="text-none" :loading="loading" @click="load">Göster</v-btn>
      </div>
      <p class="text-body-2 text-medium-emphasis px-4 mb-2">
        Gün sınırı Europe/Istanbul. Çift okuma ve red de listede durur. {{ rows.length }} kayıt.
      </p>
      <v-divider />
      <v-data-table
        :loading="loading"
        :items="rows"
        :headers="[
          { title: 'Saat', key: 'occurredAt' },
          { title: 'Ad', key: 'displayName' },
          { title: 'Yön', key: 'direction' },
          { title: 'Kapı', key: 'doorKey' },
          { title: 'Lokasyon', key: 'locationKey' },
          { title: 'Sonuç', key: 'result' },
          { title: 'Red nedeni', key: 'rejectReason' },
        ]"
        item-value="__dataId"
        density="comfortable"
        :items-per-page="25"
      >
        <template #item.occurredAt="{ value }">{{ clock(value) }}</template>
        <template #item.direction="{ value }">{{ directionLabel(value) }}</template>
        <template #item.doorKey="{ value }">{{ doorTitle(value) }}</template>
        <template #item.locationKey="{ value }">{{ locationTitle(value) }}</template>
        <template #item.result="{ value }">{{ resultLabel(value) }}</template>
        <template #item.rejectReason="{ value }">{{ reasonLabel(value) }}</template>
        <template #no-data>Bu günde geçiş yok.</template>
      </v-data-table>
    </v-card>
  </div>
</template>
