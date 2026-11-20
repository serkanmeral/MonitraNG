<script setup lang="ts">
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { fetchFromDataGateway } from '@/services/apiService';

definePageMeta({ layout: 'default' });

type Row = Record<string, unknown>;
type Card = { key: string; label: string; value: number };
type Person = { name: string; entry: string; exit: string; started: string; startedAt?: string | null };
type LivePayload = {
  stamp?: number;
  unchanged?: boolean;
  cards?: Card[];
  inside?: Person[];
  breaks?: Person[];
  left?: Person[];
  message?: string;
};

const auth = useAuthStore();
const config = useRuntimeConfig();

const locationKey = ref('');
const locations = ref<Row[]>([]);
const cards = ref<Card[]>([]);
const inside = ref<Person[]>([]);
const breaks = ref<Person[]>([]);
const left = ref<Person[]>([]);
const errorText = ref('');
const loading = ref(false);
const seenStamp = ref<number | null>(null);
const tick = ref(Date.now());

let pollTimer = 0;
let clockTimer = 0;

const breadcrumbs = [
  { text: 'AnkaraBT', disabled: false, href: '/dashboards/analytical' },
  { text: 'PDKS', disabled: true, href: '#' },
  { text: 'Anlık durum', disabled: true, href: '#' },
];

const locationItems = computed(() => locations.value.map((row) => ({
  title: String(row.title || row.locationKey || ''),
  value: String(row.locationKey || ''),
})));

const cardTone: Record<string, string> = {
  arrived: 'primary',
  inside: 'success',
  break: 'warning',
  left: 'secondary',
};

function gateway() {
  return String(config.public.gatewayUrl || '').replace(/\/$/, '');
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
  return data?.error?.message || data?.message || (error instanceof Error ? error.message : '');
}

function elapsed(startedAt?: string | null) {
  if (!startedAt) return '';
  const start = new Date(startedAt).getTime();
  if (!Number.isFinite(start)) return '';
  const seconds = Math.max(0, Math.floor((tick.value - start) / 1000));
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const rest = seconds % 60;
  if (hours > 0) return `${hours} sa ${String(minutes).padStart(2, '0')} dk`;
  return `${minutes} dk ${String(rest).padStart(2, '0')} sn`;
}

async function loadLocations() {
  const payload = await fetchFromDataGateway('/api/v1/data/pdks_lokasyonlar?limit=200&expand=false');
  locations.value = asRows(payload).filter((row) => row.active !== false && row.locationKey);
  if (!locationKey.value && locationItems.value.length) locationKey.value = locationItems.value[0].value;
  if (!locationItems.value.length) errorText.value = 'Lokasyon bulunamadı.';
}

async function refresh(force = false) {
  if (!locationKey.value) return;
  const stamp = force ? '' : (seenStamp.value ?? '');
  const url = `${gateway()}/extensions/api/v1/pdks/live?locationKey=${encodeURIComponent(locationKey.value)}&stamp=${stamp}`;
  const payload = await $fetch<LivePayload>(url, {
    headers: { Authorization: `Bearer ${auth.accessToken}` },
  });
  if (typeof payload.stamp === 'number') seenStamp.value = payload.stamp;
  if (payload.unchanged) return;
  cards.value = Array.isArray(payload.cards) ? payload.cards : [];
  inside.value = Array.isArray(payload.inside) ? payload.inside : [];
  breaks.value = Array.isArray(payload.breaks) ? payload.breaks : [];
  left.value = Array.isArray(payload.left) ? payload.left : [];
}

async function reload(force = false) {
  if (force) loading.value = true;
  errorText.value = '';
  try {
    await refresh(force);
  } catch (error: unknown) {
    errorText.value = messageOf(error) || 'Anlık durum alınamadı.';
  } finally {
    loading.value = false;
  }
}

watch(locationKey, () => {
  seenStamp.value = null;
  cards.value = [];
  inside.value = [];
  breaks.value = [];
  left.value = [];
  void reload(true);
});

onMounted(async () => {
  clockTimer = window.setInterval(() => { tick.value = Date.now(); }, 1000);
  try {
    await loadLocations();
  } catch (error: unknown) {
    errorText.value = messageOf(error) || 'Lokasyonlar alınamadı.';
  }
  pollTimer = window.setInterval(() => { void reload(false); }, 3000);
});

onUnmounted(() => {
  window.clearInterval(pollTimer);
  window.clearInterval(clockTimer);
});
</script>

<template>
  <div>
    <BaseBreadcrumb title="Anlık durum" :breadcrumbs="breadcrumbs" />
    <v-alert v-if="errorText" type="error" variant="tonal" class="mb-4">{{ errorText }}</v-alert>
    <div class="d-flex flex-wrap align-end ga-3 mb-4">
      <v-select
        v-model="locationKey"
        :items="locationItems"
        label="Lokasyon"
        density="comfortable"
        hide-details
        style="min-width: 240px; max-width: 320px"
      />
      <span class="text-body-2 text-medium-emphasis pb-3">Bugün, Europe/Istanbul. Her kabul edilen geçişte yenilenir.</span>
    </div>

    <v-row class="mb-2">
      <v-col v-for="card in cards" :key="card.key" cols="12" sm="6" md="3">
        <v-card variant="outlined" class="rounded-lg" :loading="loading">
          <v-card-text>
            <div class="text-body-2 text-medium-emphasis">{{ card.label }}</div>
            <div class="text-h4 font-weight-medium" :class="`text-${cardTone[card.key] || 'primary'}`">{{ card.value }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-row>
      <v-col cols="12" lg="4">
        <v-card variant="outlined" class="rounded-lg">
          <v-card-title class="text-subtitle-1">İçeride</v-card-title>
          <v-divider />
          <v-table density="comfortable">
            <thead>
              <tr>
                <th>Ad</th>
                <th>Giriş</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(person, index) in inside" :key="`${person.name}-${index}`">
                <td>{{ person.name }}</td>
                <td>{{ person.entry }}</td>
              </tr>
              <tr v-if="!inside.length">
                <td colspan="2" class="text-medium-emphasis">İçeride kimse yok.</td>
              </tr>
            </tbody>
          </v-table>
        </v-card>
      </v-col>
      <v-col cols="12" lg="4">
        <v-card variant="outlined" class="rounded-lg">
          <v-card-title class="text-subtitle-1">Molada</v-card-title>
          <v-divider />
          <v-table density="comfortable">
            <thead>
              <tr>
                <th>Ad</th>
                <th>Başlangıç</th>
                <th>Süre</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(person, index) in breaks" :key="`${person.name}-${index}`">
                <td>{{ person.name }}</td>
                <td>{{ person.started }}</td>
                <td>{{ elapsed(person.startedAt) }}</td>
              </tr>
              <tr v-if="!breaks.length">
                <td colspan="3" class="text-medium-emphasis">Molada kimse yok.</td>
              </tr>
            </tbody>
          </v-table>
        </v-card>
      </v-col>
      <v-col cols="12" lg="4">
        <v-card variant="outlined" class="rounded-lg">
          <v-card-title class="text-subtitle-1">Çıkan</v-card-title>
          <v-divider />
          <v-table density="comfortable">
            <thead>
              <tr>
                <th>Ad</th>
                <th>Giriş</th>
                <th>Çıkış</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(person, index) in left" :key="`${person.name}-${index}`">
                <td>{{ person.name }}</td>
                <td>{{ person.entry }}</td>
                <td>{{ person.exit }}</td>
              </tr>
              <tr v-if="!left.length">
                <td colspan="3" class="text-medium-emphasis">Çıkan kimse yok.</td>
              </tr>
            </tbody>
          </v-table>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>
