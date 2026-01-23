<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useWidgetStore } from '@/stores/apps/widget';
import { useLocaleStore } from '@/stores/locale';
import { EditIcon, TrashIcon, PlusIcon, RefreshIcon, EyeIcon } from 'vue-tabler-icons';
import WidgetRenderer from '@/components/widgets/WidgetRenderer.vue';

// Get i18n instance for legacy mode
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;

const t = (key: string, params?: any) => {
  if (i18n && i18n.t) {
    return i18n.t(key, params);
  }
  if (i18n?.global?.t) {
    return i18n.global.t(key, params);
  }
  return key;
};

const widgetStore = useWidgetStore();
const router = useRouter();
const localeStore = useLocaleStore();

const page = computed(() => ({ title: t('widgets.title') }));
const breadcrumbs = computed(() => [
  {
    text: t('widgets.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('widgets.breadcrumbs.widgets'),
    disabled: true,
    href: '#',
  },
]);

const search = ref('');
const selectedWidgets = ref<any[]>([]);
const showDeleteDialog = ref(false);
const widgetToDelete = ref<string | null>(null);
const showPreviewDialog = ref(false);
const widgetToPreview = ref<string | null>(null);
const isActiveFilter = ref<boolean | 'all'>('all');
const categoryFilter = ref<string | 'all'>('all');
const typeFilter = ref<'card' | 'chart' | 'table' | 'banner' | 'all'>('all');

const tableOptions = ref({
  page: 1,
  itemsPerPage: 20,
  sortBy: [] as Array<{ key: string; order: 'asc' | 'desc' }>,
});

const headers = computed(() => [
  { title: t('widgets.list.table.headers.name'), key: 'name', sortable: true },
  { title: t('widgets.list.table.headers.title'), key: 'title', sortable: true },
  { title: t('widgets.list.table.headers.category'), key: 'category', sortable: true },
  { title: t('widgets.list.table.headers.type'), key: 'type', sortable: true },
  { title: t('widgets.list.table.headers.isActive'), key: 'isActive', sortable: true },
  { title: t('widgets.list.table.headers.order'), key: 'order', sortable: true },
  { title: t('widgets.list.table.headers.createdAt'), key: 'createInfo.createdAt', sortable: true },
  { title: t('widgets.list.table.headers.actions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const serverItemsLength = computed(() => widgetStore.totalCount);
const totalPages = computed(() => {
  const n = widgetStore.totalCount;
  const size = tableOptions.value.itemsPerPage || 20;
  return n <= 0 ? 1 : Math.ceil(n / size);
});
const isInitialLoad = ref(true);

// Category options
const categoryOptions = computed(() => {
  const options = [{ value: 'all' as const, title: t('widgets.list.filters.allCategories') }];
  return [
    ...options,
    ...widgetStore.activeCategories.map((cat) => ({
      value: cat.__dataId ?? cat.dataId ?? '',
      title: cat.name,
    })),
  ];
});

const typeOptions = computed(() => [
  { value: 'all' as const, title: t('widgets.list.filters.allTypes') },
  { value: 'card' as const, title: 'Card' },
  { value: 'chart' as const, title: 'Chart' },
  { value: 'table' as const, title: 'Table' },
  { value: 'banner' as const, title: 'Banner' },
]);

const isActiveOptions = [
  { value: 'all' as const, title: 'Tümü' },
  { value: true as const, title: 'Aktif' },
  { value: false as const, title: 'Pasif' },
];

async function fetchWidgets(options?: typeof tableOptions.value) {
  const opts = options ?? tableOptions.value;
  const skip = ((opts.page || 1) - 1) * (opts.itemsPerPage || 20);
  const limit = opts.itemsPerPage || 20;
  const sort = 'order,name';
  let filter: string | undefined;
  const filters: string[] = [];
  if (isActiveFilter.value === true) filters.push('isActive:eq:true');
  else if (isActiveFilter.value === false) filters.push('isActive:eq:false');
  if (categoryFilter.value !== 'all') filters.push(`category:eq:${categoryFilter.value}`);
  if (typeFilter.value !== 'all') filters.push(`type:eq:${typeFilter.value}`);
  if (filters.length > 0) filter = filters.join(',');
  const params: any = { skip, limit, sort, filter };
  if (search.value?.trim()) params.search = search.value.trim();
  try {
    await widgetStore.fetchWidgets(params);
  } catch {
    /* store sets error */
  }
}

watch(
  () => [tableOptions.value.page, tableOptions.value.itemsPerPage],
  ([_newPage, newSize], [_oldPage, oldSize]) => {
    if (isInitialLoad.value) return;
    if (oldSize !== undefined && newSize !== oldSize) tableOptions.value.page = 1;
    fetchWidgets();
  }
);

let searchTimeout: ReturnType<typeof setTimeout> | null = null;
watch(search, () => {
  if (tableOptions.value.page !== 1) tableOptions.value.page = 1;
  if (searchTimeout) clearTimeout(searchTimeout);
  searchTimeout = setTimeout(() => fetchWidgets(), 500);
});

watch([isActiveFilter, categoryFilter, typeFilter], () => {
  if (tableOptions.value.page !== 1) tableOptions.value.page = 1;
  fetchWidgets();
});

onMounted(async () => {
  isInitialLoad.value = true;
  try {
    await widgetStore.fetchWidgetCategories();
    await fetchWidgets();
  } catch {
    /* store sets error */
  }
  isInitialLoad.value = false;
});

function editWidget(item: any) {
  const id = item.__dataId ?? item.dataId;
  if (!id) return;
  router.push(`/apps/widgets/${encodeURIComponent(id)}/edit`);
}

function deleteWidget(item: any) {
  const id = item.__dataId ?? item.dataId;
  if (!id) return;
  widgetToDelete.value = id;
  showDeleteDialog.value = true;
}

function previewWidget(item: any) {
  const id = item.__dataId ?? item.dataId;
  if (!id) return;
  widgetToPreview.value = id;
  showPreviewDialog.value = true;
}

async function confirmDelete() {
  if (!widgetToDelete.value) return;
  try {
    await widgetStore.deleteWidget(widgetToDelete.value);
    showDeleteDialog.value = false;
    widgetToDelete.value = null;
    await fetchWidgets();
  } catch {
    /* store sets error */
  }
}

function createNew() {
  router.push('/apps/widgets/new');
}

function formatDate(date: string | Date | null | undefined) {
  if (!date) return '-';
  try {
    const locale = localeStore.locale || 'tr';
    const localeMap: Record<string, string> = { tr: 'tr-TR', en: 'en-US', fr: 'fr-FR', ar: 'ar-SA', zh: 'zh-CN' };
    return new Date(date).toLocaleDateString(localeMap[locale] || 'tr-TR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return '-';
  }
}

function getCategoryName(widget: any): string {
  if (typeof widget.category === 'string') {
    const cat = widgetStore.getCategoryById(widget.category);
    return cat?.name ?? widget.category;
  }
  return widget.category?.name ?? '-';
}

function getTypeIcon(type: string): string {
  const icons: Record<string, string> = {
    card: 'mdi-card',
    chart: 'mdi-chart-line',
    table: 'mdi-table',
    banner: 'mdi-view-dashboard',
  };
  return icons[type] || 'mdi-widgets';
}
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-card elevation="10">
      <v-card-item>
        <div class="d-flex justify-space-between align-center mb-4 flex-wrap ga-3">
          <div class="d-flex ga-3 align-center flex-wrap">
            <v-text-field
              v-model="search"
              prepend-inner-icon="mdi-magnify"
              :label="t('widgets.list.search')"
              placeholder="Ad veya başlık..."
              variant="outlined"
              density="compact"
              hide-details
              style="max-width: 280px;"
              clearable
            />
            <v-select
              v-model="categoryFilter"
              :items="categoryOptions"
              item-title="title"
              item-value="value"
              :label="t('widgets.list.filters.category')"
              variant="outlined"
              density="compact"
              hide-details
              style="max-width: 160px;"
            />
            <v-select
              v-model="typeFilter"
              :items="typeOptions"
              item-title="title"
              item-value="value"
              :label="t('widgets.list.filters.type')"
              variant="outlined"
              density="compact"
              hide-details
              style="max-width: 140px;"
            />
            <v-select
              v-model="isActiveFilter"
              :items="isActiveOptions"
              item-title="title"
              item-value="value"
              :label="t('widgets.list.filters.status')"
              variant="outlined"
              density="compact"
              hide-details
              style="max-width: 140px;"
            />
            <v-btn icon size="small" variant="outlined" color="default" :loading="widgetStore.loading" @click="fetchWidgets">
              <RefreshIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('widgets.list.refresh') }}</v-tooltip>
            </v-btn>
          </div>
          <v-btn color="primary" variant="flat" @click="createNew">
            <PlusIcon class="mr-2" size="20" />
            {{ t('widgets.list.createNew') }}
          </v-btn>
        </div>

        <v-alert
          v-if="widgetStore.error"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          closable
          @click:close="widgetStore.clearError"
        >
          {{ widgetStore.error }}
        </v-alert>

        <v-data-table
          v-model="selectedWidgets"
          v-model:options="tableOptions"
          :headers="headers"
          :items="widgetStore.widgets"
          :loading="widgetStore.loading"
          :server-items-length="serverItemsLength"
          :items-per-page-options="[10, 20, 50, 100]"
          item-value="__dataId"
          class="border rounded-md"
          hide-default-footer
        >
          <template v-slot:bottom>
            <div class="d-flex justify-space-between align-center pa-3 border-top">
              <div class="text-caption text-medium-emphasis">
                <strong>{{ t('widgets.list.total') }}:</strong> {{ widgetStore.totalCount }}
                {{ t('widgets.list.records') }}
                <span v-if="totalPages > 1" class="ml-2">({{ t('widgets.list.page') }} {{ tableOptions.page }} / {{ totalPages }})</span>
                <span class="ml-2">
                  | {{ ((tableOptions.page - 1) * tableOptions.itemsPerPage) + 1 }} -
                  {{ Math.min(tableOptions.page * tableOptions.itemsPerPage, widgetStore.totalCount) }}
                  / {{ widgetStore.totalCount }} {{ t('widgets.list.showing') }}
                </span>
              </div>
              <div class="d-flex align-center ga-2">
                <span class="text-caption text-medium-emphasis">{{ t('widgets.list.itemsPerPage') }}:</span>
                <v-select
                  v-model="tableOptions.itemsPerPage"
                  :items="[10, 20, 50, 100]"
                  density="compact"
                  variant="outlined"
                  hide-details
                  style="max-width: 80px;"
                />
              </div>
            </div>
            <div v-if="totalPages > 1" class="d-flex justify-center pa-2 border-top">
              <v-pagination v-model="tableOptions.page" :length="totalPages" :total-visible="7" density="compact" />
            </div>
          </template>
          <template #item.name="{ item }">
            <div class="d-flex align-center ga-2">
              <v-icon size="18" class="text-primary">mdi-widgets</v-icon>
              <span class="text-subtitle-1 font-weight-medium">{{ item.name ?? item.Name }}</span>
            </div>
          </template>

          <template #item.title="{ item }">
            <span>{{ item.title ?? item.Title }}</span>
          </template>

          <template #item.category="{ item }">
            <v-chip size="small" variant="tonal" color="secondary">
              {{ getCategoryName(item) }}
            </v-chip>
          </template>

          <template #item.type="{ item }">
            <v-chip size="small" variant="tonal" color="info">
              <v-icon start size="14">{{ getTypeIcon(item.type) }}</v-icon>
              {{ item.type }}
            </v-chip>
          </template>

          <template #item.isActive="{ item }">
            <v-chip :color="item.isActive ? 'success' : 'error'" size="small" variant="flat">
              {{ item.isActive ? t('widgets.list.active') : t('widgets.list.inactive') }}
            </v-chip>
          </template>

          <template #item.order="{ item }">
            <span class="text-caption">{{ item.order ?? item.Order ?? 0 }}</span>
          </template>

          <template #item.createInfo.createdAt="{ item }">
            <span class="text-caption">{{ formatDate(item.createInfo?.createdAt ?? item.CreateInfo?.createdAt) }}</span>
          </template>

          <template #item.actions="{ item }">
            <div class="d-flex ga-2 justify-end">
              <v-btn icon size="small" variant="text" color="secondary" @click="previewWidget(item)">
                <EyeIcon size="18" />
                <v-tooltip activator="parent" location="top">{{ t('widgets.list.preview') || 'Önizle' }}</v-tooltip>
              </v-btn>
              <v-btn icon size="small" variant="text" color="info" @click="editWidget(item)">
                <EditIcon size="18" />
                <v-tooltip activator="parent" location="top">{{ t('widgets.list.edit') }}</v-tooltip>
              </v-btn>
              <v-btn icon size="small" variant="text" color="error" @click="deleteWidget(item)">
                <TrashIcon size="18" />
                <v-tooltip activator="parent" location="top">{{ t('widgets.list.delete') }}</v-tooltip>
              </v-btn>
            </div>
          </template>

          <template #no-data>
            <div class="text-center py-8">
              <p class="text-subtitle-1 text-medium-emphasis">{{ t('widgets.list.noData') }}</p>
              <v-btn color="primary" variant="flat" class="mt-4" @click="createNew">
                <PlusIcon class="mr-2" size="20" />
                {{ t('widgets.list.createFirst') }}
              </v-btn>
            </div>
          </template>
        </v-data-table>
      </v-card-item>
    </v-card>

    <v-dialog v-model="showDeleteDialog" max-width="420" persistent>
      <v-card>
        <v-card-title>{{ t('widgets.list.deleteDialog.title') }}</v-card-title>
        <v-card-text>
          {{ t('widgets.list.deleteDialog.message') }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showDeleteDialog = false; widgetToDelete = null">
            {{ t('widgets.list.deleteDialog.cancel') }}
          </v-btn>
          <v-btn color="error" variant="flat" :loading="widgetStore.loading" @click="confirmDelete">
            {{ t('widgets.list.deleteDialog.confirm') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Preview Dialog -->
    <v-dialog v-model="showPreviewDialog" max-width="600" scrollable>
      <v-card>
        <v-card-title class="d-flex align-center">
          <v-icon class="mr-2">mdi-eye</v-icon>
          <span>{{ t('widgets.list.preview') || 'Widget Önizleme' }}</span>
          <v-spacer />
          <v-btn icon variant="text" size="small" @click="showPreviewDialog = false; widgetToPreview = null">
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <div v-if="widgetToPreview">
            <WidgetRenderer :widget-id="widgetToPreview" :t="t" />
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showPreviewDialog = false; widgetToPreview = null">
            {{ t('widgets.list.close') || 'Kapat' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
