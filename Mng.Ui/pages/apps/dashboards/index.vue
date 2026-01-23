<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useDashboardStore } from '@/stores/apps/dashboard';
import { useLocaleStore } from '@/stores/locale';
import { EditIcon, EyeIcon, TrashIcon, LayoutDashboardIcon, PlusIcon, RefreshIcon, ExternalLinkIcon, MenuIcon } from 'vue-tabler-icons';

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: any) => {
  if (i18n?.t) return i18n.t(key, params);
  if (i18n?.global?.t) return i18n.global.t(key, params);
  return key;
};

const dashboardStore = useDashboardStore();
const router = useRouter();
const localeStore = useLocaleStore();

const page = computed(() => ({ title: t('dashboards.title') || 'Dashboards' }));
const breadcrumbs = computed(() => [
  { text: t('dashboards.breadcrumbs.home') || 'Dashboard', disabled: false, href: '/dashboards/analytical' },
  { text: t('dashboards.breadcrumbs.dashboards') || 'Dashboards', disabled: true, href: '#' },
]);

const search = ref('');
const selectedDashboards = ref<any[]>([]);
const showDeleteDialog = ref(false);
const dashboardToDelete = ref<string | null>(null);
const isActiveFilter = ref<boolean | 'all'>('all');

const tableOptions = ref({
  page: 1,
  itemsPerPage: 20,
  sortBy: [] as Array<{ key: string; order: 'asc' | 'desc' }>,
});

const headers = computed(() => [
  { title: t('dashboards.list.table.headers.name') || 'Ad', key: 'name', sortable: true },
  { title: t('dashboards.list.table.headers.title') || 'Başlık', key: 'title', sortable: true },
  { title: t('dashboards.list.table.headers.slug') || 'Slug', key: 'slug', sortable: true },
  { title: t('dashboards.list.table.headers.isDefault') || 'Varsayılan', key: 'isDefault', sortable: true },
  { title: t('dashboards.list.table.headers.isActive') || 'Aktif', key: 'isActive', sortable: true },
  { title: t('dashboards.list.table.headers.order') || 'Sıra', key: 'order', sortable: true },
  { title: t('dashboards.list.table.headers.createdAt') || 'Oluşturulma', key: 'createInfo.createdAt', sortable: true },
  { title: t('dashboards.list.table.headers.actions') || 'İşlemler', key: 'actions', sortable: false, align: 'end' as const },
]);

const serverItemsLength = computed(() => dashboardStore.totalCount);
const totalPages = computed(() => {
  const n = dashboardStore.totalCount;
  const size = tableOptions.value.itemsPerPage || 20;
  return n <= 0 ? 1 : Math.ceil(n / size);
});
const isInitialLoad = ref(true);

async function fetchDashboards(options?: typeof tableOptions.value) {
  const opts = options ?? tableOptions.value;
  const skip = ((opts.page || 1) - 1) * (opts.itemsPerPage || 20);
  const limit = opts.itemsPerPage || 20;
  const sort = 'order,name';
  let filter: string | undefined;
  if (isActiveFilter.value === true) filter = 'isActive:eq:true';
  else if (isActiveFilter.value === false) filter = 'isActive:eq:false';
  const params: any = { skip, limit, sort, filter };
  if (search.value?.trim()) params.search = search.value.trim();
  try {
    await dashboardStore.fetchDashboards(params);
  } catch {
    /* store sets error */
  }
}

watch(
  () => [tableOptions.value.page, tableOptions.value.itemsPerPage],
  ([_newPage, newSize], [_oldPage, oldSize]) => {
    if (isInitialLoad.value) return;
    if (oldSize !== undefined && newSize !== oldSize) tableOptions.value.page = 1;
    fetchDashboards();
  }
);

let searchTimeout: ReturnType<typeof setTimeout> | null = null;
watch(search, () => {
  if (tableOptions.value.page !== 1) tableOptions.value.page = 1;
  if (searchTimeout) clearTimeout(searchTimeout);
  searchTimeout = setTimeout(() => fetchDashboards(), 500);
});

watch(isActiveFilter, () => {
  if (tableOptions.value.page !== 1) tableOptions.value.page = 1;
  fetchDashboards();
});

onMounted(async () => {
  isInitialLoad.value = true;
  await fetchDashboards();
  isInitialLoad.value = false;
});

function previewDashboard(item: any) {
  const slug = item.slug ?? item.name ?? '';
  if (!slug) return;
  const path = `/dashboards/${encodeURIComponent(slug)}`;
  router.push(path);
}

function editDashboard(item: any) {
  const id = item.__dataId ?? item.dataId;
  if (!id) return;
  router.push(`/apps/dashboards/${encodeURIComponent(id)}/edit`);
}

function deleteDashboard(item: any) {
  const id = item.__dataId ?? item.dataId;
  if (!id) return;
  dashboardToDelete.value = id;
  showDeleteDialog.value = true;
}

async function confirmDelete() {
  if (!dashboardToDelete.value) return;
  try {
    await dashboardStore.deleteDashboard(dashboardToDelete.value);
    showDeleteDialog.value = false;
    dashboardToDelete.value = null;
    await fetchDashboards();
  } catch {
    /* store sets error */
  }
}

function createNew() {
  router.push('/apps/dashboards/new');
}

function addToSideMenu(dashboard: any) {
  const dashboardId = dashboard.__dataId ?? dashboard.dataId ?? '';
  const slug = dashboard.slug ?? dashboard.name ?? '';
  const routePath = slug ? `/dashboards/${slug}` : undefined;
  
  // Side Menu Manager create sayfasına yönlendir ve query params ile dashboard bilgilerini geç
  const query: any = {
    source: 'dashboard',
    dashboardId,
    title: dashboard.title ?? dashboard.name,
    routePath: routePath,
  };
  
  router.push({
    path: '/apps/side-menu-manager',
    query,
  });
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

const isActiveOptions = computed(() => [
  { value: 'all' as const, title: t('dashboards.list.statusAll') || 'Tümü' },
  { value: true as const, title: t('dashboards.list.statusActive') || 'Aktif' },
  { value: false as const, title: t('dashboards.list.statusInactive') || 'Pasif' },
]);
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
            :label="t('dashboards.list.search') || 'Dashboard ara'"
            :placeholder="t('dashboards.list.searchPlaceholder') || 'Ad veya başlık...'"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 280px;"
            clearable
          />
          <v-select
            v-model="isActiveFilter"
            :items="isActiveOptions"
            item-title="title"
            item-value="value"
            :label="t('dashboards.list.status') || 'Durum'"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 140px;"
          />
          <v-btn icon size="small" variant="outlined" color="default" :loading="dashboardStore.loading" @click="fetchDashboards">
            <RefreshIcon size="18" />
            <v-tooltip activator="parent" location="top">{{ t('dashboards.list.refresh') || 'Yenile' }}</v-tooltip>
          </v-btn>
        </div>
        <v-btn color="primary" variant="flat" @click="createNew">
          <PlusIcon class="mr-2" size="20" />
          {{ t('dashboards.list.createNew') || 'Yeni Dashboard' }}
        </v-btn>
      </div>

      <v-alert
        v-if="dashboardStore.error"
        type="error"
        variant="tonal"
        density="compact"
        class="mb-4"
        closable
        @click:close="dashboardStore.clearError"
      >
        {{ dashboardStore.error }}
      </v-alert>

      <v-data-table
        v-model="selectedDashboards"
        v-model:options="tableOptions"
        :headers="headers"
        :items="dashboardStore.dashboards"
        :loading="dashboardStore.loading"
        :server-items-length="serverItemsLength"
        :items-per-page-options="[10, 20, 50, 100]"
        item-value="__dataId"
        class="border rounded-md"
        hide-default-footer
      >
        <template v-slot:bottom>
          <div class="d-flex justify-space-between align-center pa-3 border-top">
            <div class="text-caption text-medium-emphasis">
              <strong>{{ t('dashboards.list.total') || 'Toplam' }}:</strong> {{ dashboardStore.totalCount }} {{ t('dashboards.list.records') || 'kayıt' }}
              <span v-if="totalPages > 1" class="ml-2">({{ t('dashboards.list.page') || 'Sayfa' }} {{ tableOptions.page }} / {{ totalPages }})</span>
              <span class="ml-2">
                | {{ ((tableOptions.page - 1) * tableOptions.itemsPerPage) + 1 }} -
                {{ Math.min(tableOptions.page * tableOptions.itemsPerPage, dashboardStore.totalCount) }}
                / {{ dashboardStore.totalCount }} {{ t('dashboards.list.showing') || 'gösteriliyor' }}
              </span>
            </div>
            <div class="d-flex align-center ga-2">
              <span class="text-caption text-medium-emphasis">{{ t('dashboards.list.itemsPerPage') || 'Sayfa başına' }}:</span>
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
            <v-pagination
              v-model="tableOptions.page"
              :length="totalPages"
              :total-visible="7"
              density="compact"
            />
          </div>
        </template>
        <template #item.name="{ item }">
          <div class="d-flex align-center ga-2">
            <LayoutDashboardIcon size="18" class="text-primary" />
            <span class="text-subtitle-1 font-weight-medium">{{ item.name ?? item.Name }}</span>
          </div>
        </template>

        <template #item.title="{ item }">
          <span>{{ item.title ?? item.Title }}</span>
        </template>

        <template #item.slug="{ item }">
          <v-chip size="small" variant="tonal" color="info">
            {{ item.slug ?? item.name ?? '-' }}
          </v-chip>
        </template>

        <template #item.isDefault="{ item }">
          <v-chip :color="item.isDefault ? 'primary' : 'default'" size="small" variant="tonal">
            {{ item.isDefault ? (t('dashboards.list.yes') || 'Evet') : (t('dashboards.list.no') || 'Hayır') }}
          </v-chip>
        </template>

        <template #item.isActive="{ item }">
          <v-chip :color="item.isActive ? 'success' : 'error'" size="small" variant="flat">
            {{ item.isActive ? (t('dashboards.list.active') || 'Aktif') : (t('dashboards.list.inactive') || 'Pasif') }}
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
            <v-btn
              icon
              size="small"
              variant="text"
              color="secondary"
              @click="addToSideMenu(item)"
            >
              <MenuIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('dashboards.list.actions.addToSideMenu') || 'Side Menu\'ye Ekle' }}</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="success"
              :disabled="!(item.isActive ?? true)"
              @click="previewDashboard(item)"
            >
              <ExternalLinkIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('dashboards.list.actions.preview') || 'Önizle' }}</v-tooltip>
            </v-btn>
            <v-btn icon size="small" variant="text" color="info" @click="editDashboard(item)">
              <EditIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('dashboards.list.actions.edit') || 'Düzenle' }}</v-tooltip>
            </v-btn>
            <v-btn icon size="small" variant="text" color="error" @click="deleteDashboard(item)">
              <TrashIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('dashboards.list.actions.delete') || 'Sil' }}</v-tooltip>
            </v-btn>
          </div>
        </template>

        <template #no-data>
          <div class="text-center py-8">
            <p class="text-subtitle-1 text-medium-emphasis">{{ t('dashboards.list.noData') || 'Dashboard bulunamadı' }}</p>
            <v-btn color="primary" variant="flat" class="mt-4" @click="createNew">
              <PlusIcon class="mr-2" size="20" />
              {{ t('dashboards.list.createFirst') || 'İlk Dashboard\'u Oluştur' }}
            </v-btn>
          </div>
        </template>
      </v-data-table>
    </v-card-item>
    </v-card>

    <v-dialog v-model="showDeleteDialog" max-width="420" persistent>
      <v-card>
        <v-card-title>{{ t('dashboards.list.deleteDialog.title') || 'Dashboard\'ı sil' }}</v-card-title>
        <v-card-text>
          {{ t('dashboards.list.deleteDialog.message') || 'Bu dashboard\'ı silmek istediğinizden emin misiniz? Bu işlem geri alınamaz.' }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="showDeleteDialog = false; dashboardToDelete = null">{{ t('dashboards.list.deleteDialog.cancel') || 'İptal' }}</v-btn>
          <v-btn color="error" variant="flat" :loading="dashboardStore.loading" @click="confirmDelete">
            {{ t('dashboards.list.deleteDialog.confirm') || 'Evet, Sil' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
