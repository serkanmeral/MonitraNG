<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useWidgetStore } from '@/stores/apps/widget';
import { useLocaleStore } from '@/stores/locale';
import { getWidgetCategoryDisplayName, buildModuleCategorySelectOptions } from '@/utils/widgets/widgetCategoryDomains';
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

const tablePage = ref(1);
const tableItemsPerPage = ref(20);

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

const isInitialLoad = ref(true);

// Modul kategorileri (card/chart/table tur degil)
const moduleCategoryOptions = computed(() => {
  const options = [{ value: 'all' as const, title: t('widgets.list.filters.allModules') }];
  const modules = buildModuleCategorySelectOptions(widgetStore.activeCategories, localeStore.locale);
  return [...options, ...modules];
});

const typeOptions = computed(() => [
  { value: 'all' as const, title: t('widgets.list.filters.allTypes') },
  { value: 'card' as const, title: t('widgets.list.types.card') },
  { value: 'chart' as const, title: t('widgets.list.types.chart') },
  { value: 'table' as const, title: t('widgets.list.types.table') },
  { value: 'banner' as const, title: t('widgets.list.types.banner') },
]);

const isActiveOptions = [
  { value: 'all' as const, title: 'Tümü' },
  { value: true as const, title: 'Aktif' },
  { value: false as const, title: 'Pasif' },
];

async function fetchWidgets() {
  const skip = (tablePage.value - 1) * tableItemsPerPage.value;
  const limit = tableItemsPerPage.value;
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

watch([tablePage, tableItemsPerPage], ([, newSize], [, oldSize]) => {
  if (isInitialLoad.value) return;
  if (oldSize !== undefined && newSize !== oldSize) tablePage.value = 1;
  void fetchWidgets();
});

let searchTimeout: ReturnType<typeof setTimeout> | null = null;
watch(search, () => {
  if (tablePage.value !== 1) tablePage.value = 1;
  if (searchTimeout) clearTimeout(searchTimeout);
  searchTimeout = setTimeout(() => fetchWidgets(), 500);
});

watch([isActiveFilter, categoryFilter, typeFilter], () => {
  if (tablePage.value !== 1) tablePage.value = 1;
  void fetchWidgets();
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
    return getWidgetCategoryDisplayName(cat ?? undefined, localeStore.locale);
  }
  if (widget.category && typeof widget.category === 'object') {
    return getWidgetCategoryDisplayName(widget.category as import('@/stores/apps/widget').WidgetCategory, localeStore.locale);
  }
  return '—';
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
              :items="moduleCategoryOptions"
              item-title="title"
              item-value="value"
              :label="t('widgets.list.filters.module')"
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

        <v-data-table-server
          v-model="selectedWidgets"
          v-model:page="tablePage"
          v-model:items-per-page="tableItemsPerPage"
          :headers="headers"
          :items="widgetStore.widgets"
          :loading="widgetStore.loading"
          :items-length="serverItemsLength"
          :items-per-page-options="[10, 20, 50, 100]"
          item-value="__dataId"
          class="border rounded-md"
        >
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
        </v-data-table-server>
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
