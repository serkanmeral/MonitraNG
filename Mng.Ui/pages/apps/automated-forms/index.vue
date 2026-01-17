<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAutomatedFormsStore } from '@/stores/apps/automatedForms';
import { useAuthStore } from '@/stores/auth';
import { useLocaleStore } from '@/stores/locale';
import { EditIcon, EyeIcon, TrashIcon, FileCodeIcon, PlusIcon, RefreshIcon, ExternalLinkIcon } from 'vue-tabler-icons';

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

const formStore = useAutomatedFormsStore();
const authStore = useAuthStore();
const localeStore = useLocaleStore();
const router = useRouter();

const page = computed(() => ({ title: t('automated-forms.title') }));
const breadcrumbs = computed(() => [
  {
    text: t('automated-forms.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('automated-forms.breadcrumbs.automatedForms'),
    disabled: true,
    href: '#',
  },
]);

const search = ref('');
const selectedForms = ref([]);
const showDeleteDialog = ref(false);
const formToDelete = ref<string | null>(null);
const showViewDialog = ref(false);
const formToView = ref<any>(null);

// Server-side pagination options
const tableOptions = ref({
  page: 1,
  itemsPerPage: 20,
  sortBy: [] as Array<{ key: string; order: 'asc' | 'desc' }>,
});

const headers = computed(() => [
  { title: t('automated-forms.list.table.headers.formName'), key: 'formName', sortable: true },
  { title: t('automated-forms.list.table.headers.dataset'), key: 'datasetName', sortable: true },
  { title: t('automated-forms.list.table.headers.isActive'), key: 'isActive', sortable: true },
  { title: t('automated-forms.list.table.headers.sideMenu'), key: 'sideMenu', sortable: false },
  { title: t('automated-forms.list.table.headers.createdAt'), key: 'createInfo.createdAt', sortable: true },
  { title: t('automated-forms.list.table.headers.actions'), key: 'actions', sortable: false, align: 'end' },
]);

// Computed: Server items length (reactive)
const serverItemsLength = computed(() => {
  return formStore.totalCount || 0;
});

// Fetch forms function
const fetchForms = async (options?: any) => {
  const currentOptions = options || tableOptions.value;
  
  const params: {
    pageNumber?: number;
    pageSize?: number;
    formCode?: string;
    datasetName?: string;
    isActive?: boolean;
  } = {
    pageNumber: currentOptions.page || 1,
    pageSize: currentOptions.itemsPerPage || 20,
  };
  
  // Add search parameter if exists (formCode veya formName ile arama yapılabilir)
  if (search.value && search.value.trim()) {
    params.formCode = search.value.trim();
  }
  
  try {
    await formStore.fetchForms(params);
  } catch (error) {
    // Error handled by store
  }
};

// Flag to prevent watch from triggering on initial load
const isInitialLoad = ref(true);

// Watch tableOptions for changes (pagination, itemsPerPage)
watch(
  () => [tableOptions.value.page, tableOptions.value.itemsPerPage],
  () => {
    if (isInitialLoad.value) {
      return;
    }
    fetchForms();
  }
);

// Watch for search changes (debounced)
let searchTimeout: ReturnType<typeof setTimeout> | null = null;
watch(search, () => {
  if (tableOptions.value.page !== 1) {
    tableOptions.value.page = 1;
  }
  
  if (searchTimeout) {
    clearTimeout(searchTimeout);
  }
  searchTimeout = setTimeout(() => {
    fetchForms();
  }, 500);
});

onMounted(async () => {
  isInitialLoad.value = true;
  await fetchForms();
  isInitialLoad.value = false;
});

const editForm = (form: any) => {
  const formCode = form.formCode || form.FormCode;
  router.push(`/apps/automated-forms/edit/${encodeURIComponent(formCode)}`);
};

const viewForm = (form: any) => {
  formToView.value = form;
  showViewDialog.value = true;
};

const openForm = (form: any) => {
  const formCode = form.formCode || form.FormCode;
  router.push(`/apps/automated-forms/view/${encodeURIComponent(formCode)}`);
};

const deleteForm = (form: any) => {
  const dataId = form.__dataId || form.dataId || form.DataId;
  formToDelete.value = dataId;
  showDeleteDialog.value = true;
};

const confirmDelete = async () => {
  if (formToDelete.value) {
    try {
      await formStore.deleteForm(formToDelete.value);
      showDeleteDialog.value = false;
      formToDelete.value = null;
      
      await fetchForms();
    } catch (error) {
      // Error handled by store
    }
  }
};

const refreshForms = async () => {
  await fetchForms();
};

const createNewForm = () => {
  router.push('/apps/automated-forms/create');
};

const formatDate = (date: string | Date | null | undefined) => {
  if (!date) return '-';
  try {
    const localeStore = useLocaleStore();
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
};

const truncateText = (text: string | null | undefined, maxLength: number = 50) => {
  if (!text) return '-';
  if (text.length <= maxLength) return text;
  return text.substring(0, maxLength) + '...';
};

const isSideMenuEnabled = (form: any) => {
  return form.sideMenuConfig?.enabled || form.SideMenuConfig?.enabled || false;
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10">
    <v-card-item>
      <!-- Search and Actions -->
      <div class="d-flex justify-space-between align-center mb-4 flex-wrap ga-3">
        <div class="d-flex ga-3 align-center flex-wrap">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            :label="t('automated-forms.list.search.label')"
            :placeholder="t('automated-forms.list.search.placeholder')"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 300px;"
            clearable
            :hint="t('automated-forms.list.search.hint')"
            persistent-hint
          />
          
          <v-btn
            icon
            size="small"
            variant="outlined"
            color="default"
            @click="refreshForms"
            :loading="formStore.loading"
          >
            <RefreshIcon size="18" />
            <v-tooltip activator="parent" location="top">{{ t('automated-forms.list.buttons.refresh') }}</v-tooltip>
          </v-btn>
        </div>
        
        <v-btn 
          color="primary" 
          variant="flat"
          @click="createNewForm"
        >
          <PlusIcon class="mr-2" size="20" />
          {{ t('automated-forms.list.buttons.newForm') }}
        </v-btn>
      </div>

      <!-- Error Message -->
      <v-alert
        v-if="formStore.error"
        type="error"
        variant="tonal"
        density="compact"
        class="mb-4"
        closable
        @click:close="formStore.clearError"
      >
        {{ formStore.error }}
      </v-alert>

      <!-- Data Table -->
      <v-data-table
        v-model="selectedForms"
        v-model:options="tableOptions"
        :headers="headers"
        :items="formStore.forms"
        :loading="formStore.loading"
        :server-items-length="serverItemsLength"
        :items-per-page-options="[20, 50, 100]"
        item-value="dataId"
        class="border rounded-md"
        hide-default-footer
      >
        <!-- Form Name Column -->
        <template v-slot:item.formName="{ item }">
          <div class="d-flex align-center ga-2">
            <FileCodeIcon size="18" class="text-primary" />
            <span class="text-subtitle-1 font-weight-medium">
              {{ item.formName || item.FormName }}
            </span>
          </div>
        </template>

        <!-- Dataset Column -->
        <template v-slot:item.datasetName="{ item }">
          <v-chip
            size="small"
            variant="tonal"
            color="info"
          >
            {{ item.datasetName || item.DatasetName }}
          </v-chip>
        </template>

        <!-- Active Column -->
        <template v-slot:item.isActive="{ value }">
          <v-chip :color="value ? 'success' : 'error'" size="small" variant="flat">
            {{ value ? t('automated-forms.list.table.status.active') : t('automated-forms.list.table.status.inactive') }}
          </v-chip>
        </template>

        <!-- Side Menu Column -->
        <template v-slot:item.sideMenu="{ item }">
          <v-chip 
            :color="isSideMenuEnabled(item) ? 'success' : 'default'" 
            size="small" 
            variant="tonal"
          >
            {{ isSideMenuEnabled(item) ? t('automated-forms.list.table.sideMenu.added') : t('automated-forms.list.table.sideMenu.notAdded') }}
          </v-chip>
        </template>

        <!-- Created At Column -->
        <template v-slot:item.createInfo.createdAt="{ item }">
          <span class="text-caption">
            {{ formatDate(item.createInfo?.createdAt || item.CreateInfo?.createdAt) }}
          </span>
        </template>

        <!-- Actions Column -->
        <template v-slot:item.actions="{ item }">
          <div class="d-flex ga-2 justify-end">
            <v-btn
              icon
              size="small"
              variant="text"
              color="success"
              @click="openForm(item)"
              :disabled="!item.isActive && !item.IsActive"
            >
              <ExternalLinkIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('automated-forms.list.buttons.openForm') }}</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="primary"
              @click="viewForm(item)"
            >
              <EyeIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('automated-forms.list.buttons.view') }}</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="info"
              @click="editForm(item)"
            >
              <EditIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('automated-forms.list.buttons.edit') }}</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="error"
              @click="deleteForm(item)"
            >
              <TrashIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('automated-forms.list.buttons.delete') }}</v-tooltip>
            </v-btn>
          </div>
        </template>

        <!-- No Data -->
        <template v-slot:no-data>
          <div class="text-center py-8">
            <p class="text-subtitle-1 text-medium-emphasis">{{ t('automated-forms.list.table.noData.title') }}</p>
            <v-btn 
              color="primary" 
              variant="flat"
              @click="createNewForm" 
              class="mt-4"
            >
              <PlusIcon class="mr-2" size="20" />
              {{ t('automated-forms.list.table.noData.button') }}
            </v-btn>
          </div>
        </template>

        <!-- Custom bottom slot with total count and pagination -->
        <template v-slot:bottom>
          <div class="d-flex justify-space-between align-center pa-3 border-top">
            <div class="text-caption text-medium-emphasis">
              <strong>{{ t('automated-forms.list.table.pagination.total') }}</strong> {{ formStore.totalCount }} {{ t('automated-forms.list.table.pagination.records') }}
              <span v-if="formStore.totalPages > 1" class="ml-2">
                ({{ t('automated-forms.list.table.pagination.page') }} {{ tableOptions.page }} {{ t('automated-forms.list.table.pagination.of') }} {{ formStore.totalPages }})
              </span>
              <span class="ml-2">
                | {{ ((tableOptions.page - 1) * tableOptions.itemsPerPage) + 1 }} - 
                {{ Math.min(tableOptions.page * tableOptions.itemsPerPage, formStore.totalCount) }} 
                / {{ formStore.totalCount }} {{ t('automated-forms.list.table.pagination.showing') }}
              </span>
            </div>
            <div class="d-flex align-center ga-2">
              <span class="text-caption text-medium-emphasis">{{ t('automated-forms.list.table.pagination.itemsPerPage') }}</span>
              <v-select
                v-model="tableOptions.itemsPerPage"
                :items="[20, 50, 100]"
                density="compact"
                variant="outlined"
                hide-details
                style="max-width: 80px;"
              />
            </div>
          </div>
          <div class="d-flex justify-center pa-2 border-top">
            <v-pagination
              v-model="tableOptions.page"
              :length="formStore.totalPages"
              :total-visible="7"
              density="compact"
            />
          </div>
        </template>
      </v-data-table>
    </v-card-item>
  </v-card>

  <!-- View Form Dialog -->
  <v-dialog v-model="showViewDialog" max-width="700px">
    <v-card v-if="formToView">
      <v-card-title class="pa-4 bg-primary text-white">
        <span class="text-h6">{{ formToView.formName || formToView.FormName }}</span>
      </v-card-title>
      <v-card-text class="pa-6">
        <div class="mb-4">
          <strong class="text-subtitle-2">{{ t('automated-forms.list.viewDialog.fields.formCode') }}</strong>
          <p class="text-body-1 mt-1">
            <v-chip size="small" variant="tonal" color="info">
              {{ formToView.formCode || formToView.FormCode }}
            </v-chip>
          </p>
        </div>
        
        <div class="mb-4">
          <strong class="text-subtitle-2">{{ t('automated-forms.list.viewDialog.fields.description') }}</strong>
          <p class="text-body-1 mt-1">
            {{ formToView.description || formToView.Description || t('automated-forms.list.viewDialog.noDescription') }}
          </p>
        </div>
        
        <div class="mb-4">
          <strong class="text-subtitle-2">{{ t('automated-forms.list.viewDialog.fields.dataset') }}</strong>
          <p class="text-body-1 mt-1">
            <v-chip size="small" variant="tonal" color="info">
              {{ formToView.datasetName || formToView.DatasetName }}
            </v-chip>
          </p>
        </div>
        
        <div class="mb-4">
          <strong class="text-subtitle-2">{{ t('automated-forms.list.viewDialog.fields.status') }}</strong>
          <p class="text-body-1 mt-1">
            <v-chip 
              :color="(formToView.isActive ?? formToView.IsActive) ? 'success' : 'error'" 
              size="small" 
              variant="flat"
            >
              {{ (formToView.isActive ?? formToView.IsActive) ? t('automated-forms.list.table.status.active') : t('automated-forms.list.table.status.inactive') }}
            </v-chip>
          </p>
        </div>
        
        <div class="mb-4">
          <strong class="text-subtitle-2">{{ t('automated-forms.list.viewDialog.fields.sideMenu') }}</strong>
          <p class="text-body-1 mt-1">
            <v-chip 
              :color="isSideMenuEnabled(formToView) ? 'success' : 'default'" 
              size="small" 
              variant="tonal"
            >
              {{ isSideMenuEnabled(formToView) ? t('automated-forms.list.table.sideMenu.added') : t('automated-forms.list.table.sideMenu.notAdded') }}
            </v-chip>
            <span v-if="isSideMenuEnabled(formToView) && (formToView.sideMenuConfig?.routePath || formToView.SideMenuConfig?.routePath)" class="text-caption text-medium-emphasis ml-2">
              ({{ formToView.sideMenuConfig?.routePath || formToView.SideMenuConfig?.routePath }})
            </span>
          </p>
        </div>
        
        <v-divider class="my-4"></v-divider>
        
        <div class="mb-3">
          <strong class="text-subtitle-2">{{ t('automated-forms.list.viewDialog.fields.createdAt') }}</strong>
          <p class="text-body-2 mt-1">
            {{ formatDate(formToView.createInfo?.createdAt || formToView.CreateInfo?.createdAt) }}
            <span v-if="formToView.createInfo?.userInfo?.userName || formToView.CreateInfo?.userInfo?.userName" class="text-medium-emphasis">
              ({{ formToView.createInfo?.userInfo?.userName || formToView.CreateInfo?.userInfo?.userName }})
            </span>
          </p>
        </div>
        
        <div v-if="formToView.lastUpdateInfo || formToView.LastUpdateInfo" class="mb-3">
          <strong class="text-subtitle-2">{{ t('automated-forms.list.viewDialog.fields.updatedAt') }}</strong>
          <p class="text-body-2 mt-1">
            {{ formatDate((formToView.lastUpdateInfo || formToView.LastUpdateInfo)?.updatedAt) }}
            <span v-if="(formToView.lastUpdateInfo || formToView.LastUpdateInfo)?.userInfo?.userName" class="text-medium-emphasis">
              ({{ (formToView.lastUpdateInfo || formToView.LastUpdateInfo)?.userInfo?.userName }})
            </span>
          </p>
        </div>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn 
          color="success" 
          variant="flat" 
          @click="openForm(formToView); showViewDialog = false"
          :disabled="!(formToView.isActive ?? formToView.IsActive)"
        >
          <ExternalLinkIcon class="mr-2" size="18" />
          {{ t('automated-forms.list.viewDialog.buttons.openForm') }}
        </v-btn>
        <v-btn color="primary" variant="flat" @click="showViewDialog = false">
          {{ t('automated-forms.list.viewDialog.buttons.close') }}
        </v-btn>
        <v-btn
          color="info"
          variant="flat"
          @click="editForm(formToView); showViewDialog = false"
        >
          {{ t('automated-forms.list.viewDialog.buttons.edit') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <!-- Delete Confirmation Dialog -->
  <v-dialog v-model="showDeleteDialog" max-width="500px">
    <v-card>
      <v-card-title class="pa-4 bg-error text-white">
        <span class="text-h6">{{ t('automated-forms.list.deleteDialog.title') }}</span>
      </v-card-title>
      <v-card-text class="pa-6">
        <p class="text-subtitle-1">
          {{ t('automated-forms.list.deleteDialog.message') }}
        </p>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn color="error" variant="flat" @click="showDeleteDialog = false">
          {{ t('automated-forms.list.deleteDialog.buttons.cancel') }}
        </v-btn>
        <v-btn color="success" variant="flat" @click="confirmDelete">
          {{ t('automated-forms.list.deleteDialog.buttons.confirm') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
