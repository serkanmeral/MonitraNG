<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';

definePageMeta({
  middleware: ['domain-mail-log'],
});

interface MailLogItem {
  sentAt: string;
  subject: string;
  recipients: string[];
  cc: string[];
  success: boolean;
  error?: string | null;
}

interface MailLogPage {
  total: number;
  items: MailLogItem[];
}

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: Record<string, unknown>) => {
  if (i18n && i18n.t) return i18n.t(key, params);
  if (i18n?.global?.t) return i18n.global.t(key, params);
  return key;
};

const page = computed(() => ({ title: t('domain.mailLog.title') }));
const breadcrumbs = computed(() => [
  { text: t('domain.breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('domain.breadcrumbs.domain'), disabled: false, href: '/apps/domain' },
  { text: t('domain.mailLog.title'), disabled: true, href: '#' },
]);

const pageNumber = ref(1);
const itemsPerPage = ref(25);
const pageSizes = [10, 25, 50, 100];
const sortBy = ref<{ key: string; order: 'asc' | 'desc' }[]>([{ key: 'sentAt', order: 'desc' }]);
const search = ref('');
const successFilter = ref<'all' | 'true' | 'false'>('all');
const total = ref(0);
const items = ref<MailLogItem[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);
const loaded = ref(false);

const headers = computed(() => [
  { title: t('domain.mailLog.columns.sentAt'), key: 'sentAt', sortable: true, width: 168 },
  { title: t('domain.mailLog.columns.subject'), key: 'subject', sortable: true },
  { title: t('domain.mailLog.columns.recipients'), key: 'recipients', sortable: true, width: '34%' },
  { title: t('domain.mailLog.columns.success'), key: 'success', sortable: true, width: 160 },
]);

const successItems = computed(() => [
  { title: t('domain.mailLog.resultAll'), value: 'all' },
  { title: t('domain.mailLog.sent'), value: 'true' },
  { title: t('domain.mailLog.failed'), value: 'false' },
]);

const rows = computed(() =>
  items.value.map((item, index) => ({
    ...item,
    rowKey: `${item.sentAt}-${index}`,
  }))
);

const formatWhen = (value: string) => {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat('tr-TR', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
};

const addressLines = (row: MailLogItem) => {
  const lines = [...(row.recipients || [])];
  for (const address of row.cc || []) lines.push(`Cc: ${address}`);
  return lines.length > 0 ? lines : ['—'];
};

type TableOptions = {
  page: number;
  itemsPerPage: number;
  sortBy?: { key: string; order?: 'asc' | 'desc' | boolean }[];
};

const load = async () => {
  loading.value = true;
  error.value = null;
  const size = pageSizes.includes(itemsPerPage.value) ? itemsPerPage.value : 25;
  const sort = sortBy.value[0] ?? { key: 'sentAt', order: 'desc' as const };
  const query: Record<string, string | number | boolean> = {
    skip: Math.max(0, (pageNumber.value - 1) * size),
    take: size,
    sortBy: sort.key,
    sortDesc: sort.order !== 'asc',
  };
  const term = search.value.trim();
  if (term) query.search = term;
  if (successFilter.value === 'true') query.success = true;
  if (successFilter.value === 'false') query.success = false;

  try {
    const data = await $fetch<MailLogPage>('/api/notifier/v1/notifications/mail-log', { query });
    items.value = data?.items || [];
    total.value = data?.total || 0;
  } catch (err: any) {
    items.value = [];
    total.value = 0;
    error.value = err?.statusMessage || err?.message || t('domain.mailLog.loadError');
  } finally {
    loading.value = false;
    loaded.value = true;
  }
};

function onTableOptions(options: TableOptions) {
  const nextSort = options.sortBy?.length
    ? options.sortBy.map((entry) => ({
        key: entry.key,
        order: entry.order === 'asc' || entry.order === false ? ('asc' as const) : ('desc' as const),
      }))
    : [{ key: 'sentAt', order: 'desc' as const }];
  const sortChanged = JSON.stringify(nextSort) !== JSON.stringify(sortBy.value);
  const nextSize = pageSizes.includes(options.itemsPerPage) ? options.itemsPerPage : 25;
  const sizeChanged = nextSize !== itemsPerPage.value;
  let nextPage = options.page > 0 ? options.page : 1;
  if (sortChanged || sizeChanged) nextPage = 1;
  const pageChanged = nextPage !== pageNumber.value;
  if (loaded.value && !sortChanged && !pageChanged && !sizeChanged) return;
  sortBy.value = nextSort;
  pageNumber.value = nextPage;
  itemsPerPage.value = nextSize;
  void load();
}

let searchTimer: ReturnType<typeof setTimeout> | null = null;
watch(search, () => {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    if (pageNumber.value !== 1) pageNumber.value = 1;
    void load();
  }, 350);
});

watch(successFilter, () => {
  if (!loaded.value) return;
  if (pageNumber.value !== 1) pageNumber.value = 1;
  void load();
});
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

  <v-row>
    <v-col cols="12">
      <v-alert v-if="error" type="error" variant="tonal" class="mb-4" closable @click:close="error = null">
        {{ error }}
      </v-alert>

      <v-card elevation="10">
        <v-card-title>{{ t('domain.mailLog.title') }}</v-card-title>
        <div class="d-flex flex-wrap align-center ga-3 px-4 pb-3">
          <v-text-field
            v-model="search"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            prepend-inner-icon="mdi-magnify"
            :placeholder="t('domain.mailLog.search')"
            style="max-width: 320px"
          />
          <v-select
            v-model="successFilter"
            :items="successItems"
            item-title="title"
            item-value="value"
            density="compact"
            variant="outlined"
            hide-details
            style="max-width: 200px"
          />
          <v-btn icon variant="text" :loading="loading" @click="load">
            <v-icon>mdi-refresh</v-icon>
          </v-btn>
        </div>
        <v-divider />
        <v-data-table-server
          :headers="headers"
          :items="rows"
          :items-length="total"
          :loading="loading"
          :page="pageNumber"
          :items-per-page="itemsPerPage"
          :items-per-page-options="pageSizes"
          :sort-by="sortBy"
          item-value="rowKey"
          class="mail-log-table border rounded-md"
          density="comfortable"
          hover
          @update:options="onTableOptions"
        >
          <template #item.sentAt="{ item }">
            <span class="text-no-wrap">{{ formatWhen(item.sentAt) }}</span>
          </template>
          <template #item.subject="{ item }">
            {{ item.subject || '—' }}
          </template>
          <template #item.recipients="{ item }">
            <div v-for="(line, index) in addressLines(item)" :key="index" class="mail-log-address">
              {{ line }}
            </div>
          </template>
          <template #item.success="{ item }">
            <v-chip :color="item.success ? 'success' : 'error'" size="small" variant="tonal">
              {{ item.success ? t('domain.mailLog.sent') : t('domain.mailLog.failed') }}
            </v-chip>
            <div v-if="!item.success && item.error" class="text-caption text-error mt-1">
              {{ item.error }}
            </div>
          </template>
          <template #no-data>
            <div class="text-center text-medium-emphasis py-8">
              {{ t('domain.mailLog.empty') }}
            </div>
          </template>
        </v-data-table-server>
      </v-card>
    </v-col>
  </v-row>
</template>

<style scoped>
.mail-log-table :deep(table) {
  table-layout: fixed;
  width: 100%;
}

.mail-log-table :deep(th.v-data-table__th) {
  background: rgba(var(--v-theme-on-surface), 0.04);
  white-space: nowrap;
}

.mail-log-table :deep(td.v-data-table__td) {
  text-wrap: wrap;
  white-space: normal;
  vertical-align: top;
  overflow-wrap: anywhere;
}

.mail-log-address + .mail-log-address {
  margin-top: 2px;
}
</style>
