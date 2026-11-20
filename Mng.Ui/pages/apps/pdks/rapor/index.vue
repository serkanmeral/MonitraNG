<script setup lang="ts">
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';

definePageMeta({ layout: 'default' });

type Row = Record<string, unknown>;
type Day = {
  date: string;
  absent: boolean;
  open: boolean;
  entry: string;
  entryClass: string;
  exit: string;
  exitClass: string;
  breakCount: number;
  breakMinutes: number;
  workMinutes: number | null;
};
type Person = {
  personId: string;
  name: string;
  cameDays: number;
  lateEntries: number;
  earlyExits: number;
  lateExits: number;
  breakCount: number;
  breakMinutes: number;
  workMinutes: number;
  open: boolean;
  days: Day[];
};

const ZONE = 'Europe/Istanbul';
const auth = useAuthStore();
const config = useRuntimeConfig();

const today = todayInIstanbul();
const from = ref(monthStart(today));
const to = ref(monthEnd(today));
const locationKey = ref('');
const locations = ref<Row[]>([]);
const people = ref<Person[]>([]);
const errorText = ref('');
const loading = ref(false);

const breadcrumbs = [
  { text: 'AnkaraBT', disabled: false, href: '/dashboards/analytical' },
  { text: 'PDKS', disabled: true, href: '#' },
  { text: 'Rapor', disabled: true, href: '#' },
];

const locationItems = computed(() => [
  { title: 'Tümü', value: '' },
  ...locations.value.map((row) => ({
    title: String(row.title || row.locationKey || ''),
    value: String(row.locationKey || ''),
  })),
]);

const dates = computed(() => people.value[0]?.days ?? []);

const preset = computed(() => {
  if (from.value === today && to.value === today) return 'today';
  if (from.value === weekStart(today) && to.value === today) return 'week';
  if (from.value === monthStart(today) && to.value === monthEnd(today)) return 'month';
  return '';
});

function gateway() {
  return String(config.public.gatewayUrl || '').replace(/\/$/, '');
}

function todayInIstanbul() {
  return new Intl.DateTimeFormat('en-CA', { timeZone: ZONE, year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
}

function shift(iso: string, days: number) {
  const [year, month, day] = iso.split('-').map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

function weekStart(iso: string) {
  const [year, month, day] = iso.split('-').map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  const weekday = date.getUTCDay();
  return shift(iso, weekday === 0 ? -6 : 1 - weekday);
}

function monthStart(iso: string) {
  return `${iso.slice(0, 8)}01`;
}

function monthEnd(iso: string) {
  const [year, month] = iso.split('-').map(Number);
  const date = new Date(Date.UTC(year, month, 0));
  return date.toISOString().slice(0, 10);
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

function span(minutes: number) {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours > 0 && rest > 0) return `${hours} sa ${rest} dk`;
  if (hours > 0) return `${hours} sa`;
  return `${rest} dk`;
}

function work(minutes: number | null, open = false) {
  if (open) return 'Sürüyor';
  if (minutes == null) return '—';
  return span(minutes);
}

function groupLabel(iso: string) {
  const [year, month, day] = iso.split('-').map(Number);
  return new Intl.DateTimeFormat('tr-TR', {
    day: 'numeric',
    month: 'long',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(year, month - 1, day)));
}

function dayOf(person: Person, date: string) {
  return person.days.find((day) => day.date === date);
}

function tone(kind: string, direction: 'in' | 'out') {
  if (direction === 'in' && kind === 'gec') return 'late-in';
  if (kind === 'zamaninda') return 'on-time';
  if (direction === 'out' && kind === 'erken') return 'early-out';
  if (direction === 'out' && kind === 'gec') return 'late-out';
  return '';
}

function note(kind: string, direction: 'in' | 'out') {
  if (direction === 'in' && kind === 'gec') return 'geç';
  if (direction === 'out' && kind === 'erken') return 'erken';
  if (direction === 'out' && kind === 'gec') return 'geç';
  return '';
}

function choose(start: string, end: string) {
  from.value = start;
  to.value = end;
  void load();
}

async function loadLocations() {
  const payload = await $fetch(`${gateway()}/data/api/v1/data/pdks_lokasyonlar?limit=200&expand=false`, {
    headers: { Authorization: `Bearer ${auth.accessToken}` },
  });
  locations.value = asRows(payload).filter((row) => row.active !== false && row.locationKey);
}

async function load() {
  loading.value = true;
  errorText.value = '';
  try {
    const payload = await $fetch<{ people?: Person[] }>(
      `${gateway()}/extensions/api/v1/pdks/report?from=${from.value}&to=${to.value}&locationKey=${encodeURIComponent(locationKey.value)}`,
      { headers: { Authorization: `Bearer ${auth.accessToken}` } },
    );
    people.value = Array.isArray(payload.people) ? payload.people : [];
  } catch (error: unknown) {
    people.value = [];
    errorText.value = messageOf(error) || 'Rapor alınamadı.';
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadLocations().catch((error: unknown) => {
    errorText.value = messageOf(error) || 'Lokasyonlar alınamadı.';
  });
  void load();
});
</script>

<template>
  <div>
    <BaseBreadcrumb title="PDKS raporu" :breadcrumbs="breadcrumbs" />
    <v-alert v-if="errorText" type="error" variant="tonal" class="mb-4">{{ errorText }}</v-alert>
    <div class="d-flex flex-wrap align-end ga-3 mb-2">
      <v-btn-toggle :model-value="preset" density="comfortable" divided variant="outlined" mandatory>
        <v-btn value="today" @click="choose(today, today)">Bugün</v-btn>
        <v-btn value="week" @click="choose(weekStart(today), today)">Bu hafta</v-btn>
        <v-btn value="month" @click="choose(monthStart(today), monthEnd(today))">Bu ay</v-btn>
      </v-btn-toggle>
      <v-text-field v-model="from" type="date" label="Başlangıç" density="comfortable" hide-details style="max-width: 180px" @update:model-value="load" />
      <v-text-field v-model="to" type="date" label="Bitiş" density="comfortable" hide-details style="max-width: 180px" @update:model-value="load" />
      <v-select
        v-model="locationKey"
        :items="locationItems"
        label="Lokasyon"
        density="comfortable"
        hide-details
        style="min-width: 220px; max-width: 280px"
        @update:model-value="load"
      />
    </div>
    <p class="text-body-2 text-medium-emphasis mb-4">
      <span class="legend late-in">Geç geliş</span>
      <span class="legend on-time">Zamanında</span>
      <span class="legend early-out">Erken çıkış</span>
      <span class="legend late-out">Geç çıkış</span>
      <span>Bugünden sonrası listelenmez.</span>
    </p>

    <v-card variant="outlined" class="rounded-lg" :loading="loading">
      <div class="pivot-wrap">
        <table class="pivot-table">
          <thead>
            <tr>
              <th rowspan="2" class="name-col">Personel</th>
              <th v-for="day in dates" :key="day.date" colspan="5" class="day-head">{{ groupLabel(day.date) }}</th>
            </tr>
            <tr>
              <template v-for="day in dates" :key="`${day.date}-cols`">
                <th class="day-start">Giriş</th>
                <th>Çıkış</th>
                <th>Mola sayısı</th>
                <th>Mola süresi</th>
                <th>Çalışma</th>
              </template>
            </tr>
          </thead>
          <tbody>
            <tr v-for="person in people" :key="person.personId">
              <td class="name-col font-weight-medium">{{ person.name }}</td>
              <template v-for="column in dates" :key="`${person.personId}-${column.date}`">
                <template v-if="dayOf(person, column.date)?.absent !== false">
                  <td colspan="5" class="text-medium-emphasis text-center day-start">Gelmedi</td>
                </template>
                <template v-else>
                  <td class="day-start" :class="tone(dayOf(person, column.date)?.entryClass || '', 'in')">
                    {{ dayOf(person, column.date)?.entry }}
                    <span v-if="note(dayOf(person, column.date)?.entryClass || '', 'in')" class="ml-1">{{ note(dayOf(person, column.date)?.entryClass || '', 'in') }}</span>
                  </td>
                  <td :class="tone(dayOf(person, column.date)?.exitClass || '', 'out')">
                    <template v-if="dayOf(person, column.date)?.exit">
                      {{ dayOf(person, column.date)?.exit }}
                      <span v-if="note(dayOf(person, column.date)?.exitClass || '', 'out')" class="ml-1">{{ note(dayOf(person, column.date)?.exitClass || '', 'out') }}</span>
                    </template>
                    <span v-else class="text-medium-emphasis">—</span>
                  </td>
                  <td>{{ dayOf(person, column.date)?.breakCount || 0 }}</td>
                  <td>{{ span(dayOf(person, column.date)?.breakMinutes || 0) }}</td>
                  <td>{{ work(dayOf(person, column.date)?.workMinutes ?? null, dayOf(person, column.date)?.open) }}</td>
                </template>
              </template>
            </tr>
            <tr v-if="!people.length">
              <td class="text-medium-emphasis">Bu aralıkta personel yok.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </v-card>
  </div>
</template>

<style scoped>
.pivot-wrap {
  overflow: auto;
  max-height: calc(100vh - 280px);
  background: rgb(var(--v-theme-surface));
}
.pivot-table {
  border-collapse: separate;
  border-spacing: 0;
  min-width: 100%;
  color: rgb(var(--v-theme-textPrimary));
}
.pivot-table th,
.pivot-table td {
  box-sizing: border-box;
  white-space: nowrap;
  height: 44px;
  padding: 0 14px;
  border-bottom: 1px solid rgb(var(--v-theme-borderColor));
  background: rgb(var(--v-theme-surface));
  color: rgb(var(--v-theme-textPrimary));
  font-weight: 400;
  text-align: left;
}
.pivot-table tbody tr:hover td {
  background: rgb(var(--v-theme-hoverColor));
}
.pivot-table thead th {
  position: sticky;
  z-index: 1;
  background: rgb(var(--v-theme-grey100));
  color: rgba(var(--v-theme-on-surface), var(--v-medium-emphasis-opacity));
  font-size: 0.75rem;
  font-weight: 600;
}
.pivot-table thead tr:first-child th {
  top: 0;
}
.pivot-table thead tr:nth-child(2) th {
  top: 44px;
}
.pivot-table thead th.day-head {
  height: 44px;
  background: rgb(var(--v-theme-lightprimary));
  color: rgb(var(--v-theme-primary));
  font-size: 0.875rem;
  font-weight: 600;
  text-align: center;
}
.name-col {
  position: sticky;
  left: 0;
  z-index: 2;
  min-width: 180px;
  box-shadow: 1px 0 0 rgb(var(--v-theme-borderColor));
}
.pivot-table thead .name-col {
  z-index: 3;
  height: 88px;
  background: rgb(var(--v-theme-grey100));
  color: rgb(var(--v-theme-textPrimary));
}
.day-start {
  border-left: 1px solid rgb(var(--v-theme-borderColor));
}
.legend {
  margin-right: 14px;
  font-weight: 600;
}
.pivot-table td.late-in,
.legend.late-in {
  color: #9a6700;
}
.pivot-table td.on-time,
.legend.on-time {
  color: #0f9f6e;
}
.pivot-table td.early-out,
.legend.early-out {
  color: #b91c1c;
}
.pivot-table td.late-out,
.legend.late-out {
  color: rgb(var(--v-theme-primary));
}
:global(.v-theme--dark) .pivot-table td.late-in,
:global(.v-theme--dark) .legend.late-in {
  color: rgb(var(--v-theme-warning));
}
:global(.v-theme--dark) .pivot-table td.on-time,
:global(.v-theme--dark) .legend.on-time {
  color: rgb(var(--v-theme-success));
}
:global(.v-theme--dark) .pivot-table td.early-out,
:global(.v-theme--dark) .legend.early-out {
  color: rgb(var(--v-theme-error));
}
</style>
