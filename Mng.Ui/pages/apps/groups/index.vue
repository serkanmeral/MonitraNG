<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useLocaleStore } from '@/stores/locale';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useGroupStore } from '@/stores/apps/group';
import { useAuthStore } from '@/stores/auth';
import { EditIcon, EyeIcon, TrashIcon, PlusIcon, UsersIcon, RefreshIcon, DownloadIcon } from 'vue-tabler-icons';

// Get i18n instance for legacy mode
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: any) => {
  if (i18n && i18n.t) {
    return i18n.t(key, params);
  }
  // Fallback: try global.t if available
  if (i18n?.global?.t) {
    return i18n.global.t(key, params);
  }
  return key;
};
const localeStore = useLocaleStore();
const groupStore = useGroupStore();
const authStore = useAuthStore();
const router = useRouter();
const route = useRoute();

const page = computed(() => ({ 
  title: t('groups.title') 
}));
const breadcrumbs = computed(() => [
  {
    text: t('groups.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('groups.breadcrumbs.groups'),
    disabled: true,
    href: '#',
  },
]);

const search = ref('');
const selectedGroups = ref([]);
const showDeleteDialog = ref(false);
const groupToDelete = ref<string | null>(null);
const deleteError = ref('');
const statusFilter = ref<string>('all'); // all, active, inactive
const isExporting = ref(false);
const exportError = ref('');

// Server-side pagination options
const tableOptions = ref({
  page: 1,
  itemsPerPage: 10,
  sortBy: [] as Array<{ key: string; order: 'asc' | 'desc' }>,
});

const headers = computed(() => [
  { title: t('groups.table.name'), key: 'name', sortable: true },
  { title: t('groups.table.memberCount'), key: 'memberCount', sortable: true },
  { title: t('groups.table.createdAt'), key: 'createdAt', sortable: true },
  { title: t('groups.table.actions'), key: 'actions', sortable: false, align: 'end' },
]);

// Computed: Server items length (reactive)
const serverItemsLength = computed(() => {
  return groupStore.totalCount || 0;
});

  // Fetch groups function
const fetchGroups = async (options?: any) => {
  // Use provided options or current tableOptions
  const currentOptions = options || tableOptions.value;
  
  // Build query parameters for server-side pagination/filtering
  const params: {
    page?: number;
    pageSize?: number;
    search?: string;
    isActive?: boolean;
  } = {
    page: currentOptions.page || 1,
    pageSize: currentOptions.itemsPerPage || 10,
  };
  
  // Add search term if exists
  if (search.value && search.value.trim()) {
    params.search = search.value.trim();
  }
  
  // Add status filter if not 'all'
  if (statusFilter.value === 'active') {
    params.isActive = true;
  } else if (statusFilter.value === 'inactive') {
    params.isActive = false;
  }
  
  // Fetch groups with new parameters
  try {
    await groupStore.fetchGroups(params);
  } catch (error) {
    console.error('[GroupsPage] ❌ Error loading groups:', error);
  }
};

// Flag to prevent watch from triggering on initial load
const isInitialLoad = ref(true);

// Watch tableOptions for changes (pagination, itemsPerPage, sorting)
watch(
  () => [tableOptions.value.page, tableOptions.value.itemsPerPage],
  () => {
    // Skip initial load (handled in onMounted)
    if (isInitialLoad.value) {
      return;
    }
    fetchGroups();
  }
);

// Watch for search and status filter changes
let searchTimeout: ReturnType<typeof setTimeout> | null = null;
watch([search, statusFilter], () => {
  // Reset to first page when filter changes
  if (tableOptions.value.page !== 1) {
    tableOptions.value.page = 1;
  }
  
  // Debounce search input (500ms)
  if (searchTimeout) {
    clearTimeout(searchTimeout);
  }
  searchTimeout = setTimeout(() => {
    fetchGroups();
  }, 500);
});

onMounted(async () => {
  // Manager/Admin kontrolü
  if (!authStore.isManager) {
  }
  
  // Initial load
  isInitialLoad.value = true;
  await fetchGroups();
  isInitialLoad.value = false;
});

// Watch route query for refresh parameter
watch(() => route.query.refresh, async (refreshParam) => {
  if (refreshParam) {
    // Reset to first page when returning to list (to see newly created items)
    if (tableOptions.value.page !== 1) {
      tableOptions.value.page = 1;
    }
    // Refresh groups list
    await fetchGroups();
    // Remove refresh parameter from URL
    router.replace({ path: route.path, query: {} });
  }
});

// Manual refresh function
const refreshGroups = async () => {
  await fetchGroups();
};

// Export groups function
const exportGroups = async (format: 'csv' | 'xlsx') => {
  exportError.value = '';
  isExporting.value = true;
  
  try {
    const params: {
      search?: string;
      isActive?: boolean;
    } = {};
    
    // Add search term if exists
    if (search.value && search.value.trim()) {
      params.search = search.value.trim();
    }
    
    // Add status filter if not 'all'
    if (statusFilter.value === 'active') {
      params.isActive = true;
    } else if (statusFilter.value === 'inactive') {
      params.isActive = false;
    }
    
    await groupStore.exportGroups(format, params);
    
    // Success - file will be downloaded automatically
  } catch (error: any) {
    exportError.value = error.message || t('groups.errors.export');
    console.error('Export error:', error);
  } finally {
    isExporting.value = false;
  }
};

const editGroup = (item: any) => {
  router.push(`/apps/groups/edit/${item.id || item.groupId}`);
};

const viewGroup = (item: any) => {
  router.push(`/apps/groups/details/${item.id || item.groupId}`);
};

const deleteGroup = (item: any) => {
  // Check if group has members
  if (item.memberCount > 0) {
    // Show error message in dialog
    deleteError.value = t('groups.delete.error', { count: item.memberCount });
    groupToDelete.value = item.id || item.groupId;
    showDeleteDialog.value = true;
    return;
  }
  
  groupToDelete.value = item.id || item.groupId;
  deleteError.value = '';
  showDeleteDialog.value = true;
};

const confirmDelete = async () => {
  if (groupToDelete.value) {
    deleteError.value = '';
    try {
      await groupStore.deleteGroup(groupToDelete.value);
      showDeleteDialog.value = false;
      groupToDelete.value = null;
      // Refresh list after deletion
      await fetchGroups();
    } catch (error: any) {
      // Show error message in dialog
      deleteError.value = error.message || t('groups.errors.delete');
      console.error('Error deleting group:', error);
    }
  }
};

const formatDate = (date: string | Date | null | undefined) => {
  if (!date) return '-';
  try {
    const localeMap: Record<string, string> = {
      tr: 'tr-TR',
      en: 'en-US',
      fr: 'fr-FR',
      ar: 'ar-SA',
      zh: 'zh-CN',
    };
    const locale = localeMap[localeStore.locale] || 'tr-TR';
    
    return new Date(date).toLocaleDateString(locale, {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    });
  } catch {
    return '-';
  }
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
            :label="t('groups.list.search')"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 300px;"
            clearable
          />
          
          <v-select
            v-model="statusFilter"
            :items="[
              { title: t('groups.list.statusAll'), value: 'all' },
              { title: t('groups.list.statusActive'), value: 'active' },
              { title: t('groups.list.statusInactive'), value: 'inactive' },
            ]"
            :label="t('groups.list.status')"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 150px;"
          />
        </div>
        
        <div class="d-flex ga-2">
          <v-menu>
            <template v-slot:activator="{ props }">
              <v-btn
                color="primary"
                variant="outlined"
                flat
                v-bind="props"
                :loading="isExporting"
                :disabled="isExporting"
              >
                <DownloadIcon class="mr-2" size="20" />
                {{ t('groups.list.export') }}
                <v-icon class="ml-1" size="16">mdi-chevron-down</v-icon>
              </v-btn>
            </template>
            <v-list>
              <v-list-item @click="exportGroups('csv')">
                <template v-slot:prepend>
                  <v-icon>mdi-file-delimited</v-icon>
                </template>
                <v-list-item-title>{{ t('groups.list.exportCsv') }}</v-list-item-title>
              </v-list-item>
              <v-list-item @click="exportGroups('xlsx')" disabled>
                <template v-slot:prepend>
                  <v-icon>mdi-file-excel</v-icon>
                </template>
                <v-list-item-title>{{ t('groups.list.exportExcel') }}</v-list-item-title>
                <template v-slot:append>
                  <v-chip size="x-small" color="warning">{{ t('groups.list.exportSoon') }}</v-chip>
                </template>
              </v-list-item>
            </v-list>
          </v-menu>
          
          <v-btn
            color="primary"
            variant="outlined"
            flat
            @click="refreshGroups"
            :loading="groupStore.loading"
          >
            <RefreshIcon class="mr-2" size="20" />
            {{ t('groups.list.refresh') }}
          </v-btn>
          <v-btn color="primary" to="/apps/groups/create" flat>
            <PlusIcon class="mr-2" size="20" />
            {{ t('groups.list.newGroup') }}
          </v-btn>
        </div>
      </div>

      <!-- Manager/Admin Warning -->
      <v-alert
        v-if="!authStore.isManager"
        type="warning"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        <strong>{{ t('groups.list.warning') }}:</strong> {{ t('groups.list.warningMessage') }}
      </v-alert>

      <!-- Export Error Message -->
      <v-alert
        v-if="exportError"
        type="error"
        variant="tonal"
        density="compact"
        class="mb-4"
        closable
        @click:close="exportError = ''"
      >
        {{ exportError }}
      </v-alert>

      <!-- Error Message -->
      <v-alert
        v-if="groupStore.error"
        type="error"
        variant="tonal"
        density="compact"
        class="mb-4"
        closable
        @click:close="groupStore.clearError"
      >
        {{ groupStore.error }}
      </v-alert>

      <!-- Data Table -->
      <v-data-table
        v-model:options="tableOptions"
        :headers="headers"
        :items="groupStore.groups"
        :loading="groupStore.loading"
        :server-items-length="serverItemsLength"
        :items-per-page-options="[10, 25, 50, 100]"
        item-value="id"
        class="border rounded-md"
        hide-default-footer
      >
        <!-- Name Column -->
        <template v-slot:item.name="{ item }">
          <span class="text-subtitle-1 font-weight-medium">
            {{ item.name }}
          </span>
        </template>

        <!-- Member Count Column -->
        <template v-slot:item.memberCount="{ value }">
          <div class="d-flex align-center ga-2">
            <UsersIcon size="18" />
            <span class="text-subtitle-1">{{ value || 0 }}</span>
          </div>
        </template>

        <!-- Created At Column -->
        <template v-slot:item.createdAt="{ value }">
          <span class="text-caption">
            {{ formatDate(value) }}
          </span>
        </template>

        <!-- Actions Column -->
        <template v-slot:item.actions="{ item }">
          <div class="d-flex ga-2 justify-end">
            <v-btn
              icon
              size="small"
              variant="text"
              color="primary"
              @click="viewGroup(item)"
            >
              <EyeIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('groups.table.view') }}</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="info"
              @click="editGroup(item)"
            >
              <EditIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('groups.table.edit') }}</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="error"
              @click="deleteGroup(item)"
              :disabled="item.memberCount > 0"
            >
              <TrashIcon size="18" />
              <v-tooltip activator="parent" location="top">
                {{ item.memberCount > 0 ? t('groups.table.deleteTooltip') : t('groups.table.delete') }}
              </v-tooltip>
            </v-btn>
          </div>
        </template>

        <!-- No Data -->
        <template v-slot:no-data>
          <div class="text-center py-8">
            <p class="text-subtitle-1 text-medium-emphasis">{{ t('groups.list.noData') }}</p>
            <v-btn color="primary" to="/apps/groups/create" class="mt-4">
              <PlusIcon class="mr-2" size="20" />
              {{ t('groups.list.createFirst') }}
            </v-btn>
          </div>
        </template>

        <!-- Custom bottom slot with total count and pagination -->
        <template v-slot:bottom>
          <div class="d-flex justify-space-between align-center pa-3 border-top">
            <div class="text-caption text-medium-emphasis">
              <strong>{{ t('groups.list.total') }}:</strong> {{ groupStore.totalCount }} {{ t('groups.list.records') }}
              <span v-if="groupStore.totalPages > 1" class="ml-2">
                ({{ t('groups.list.page') }} {{ tableOptions.page }} {{ t('groups.list.of') }} {{ groupStore.totalPages }})
              </span>
              <span class="ml-2">
                | {{ ((tableOptions.page - 1) * tableOptions.itemsPerPage) + 1 }} - 
                {{ Math.min(tableOptions.page * tableOptions.itemsPerPage, groupStore.totalCount) }} 
                {{ t('groups.list.of') }} {{ groupStore.totalCount }} {{ t('groups.list.showing') }}
              </span>
            </div>
            <div class="d-flex align-center ga-2">
              <span class="text-caption text-medium-emphasis">{{ t('groups.list.itemsPerPage') }}</span>
              <v-select
                v-model="tableOptions.itemsPerPage"
                :items="[10, 25, 50, 100]"
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
              :length="groupStore.totalPages"
              :total-visible="7"
              density="compact"
            />
          </div>
        </template>
      </v-data-table>
    </v-card-item>
  </v-card>

  <!-- Delete Confirmation Dialog -->
  <v-dialog v-model="showDeleteDialog" max-width="500px">
    <v-card>
      <v-card-title class="pa-4 bg-error text-white">
        <span class="text-h6">{{ t('groups.delete.title') }}</span>
      </v-card-title>
      <v-card-text class="pa-6">
        <!-- Error Message -->
        <v-alert
          v-if="deleteError"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
        >
          {{ deleteError }}
        </v-alert>
        
        <p v-if="!deleteError" class="text-subtitle-1">
          {{ t('groups.delete.confirm') }}
        </p>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn 
          color="error" 
          variant="flat" 
          @click="showDeleteDialog = false; deleteError = ''; groupToDelete = null"
        >
          {{ deleteError ? t('groups.delete.close') : t('groups.delete.cancel') }}
        </v-btn>
        <v-btn 
          v-if="!deleteError"
          color="success" 
          variant="flat" 
          @click="confirmDelete"
          :loading="groupStore.loading"
        >
          {{ t('groups.delete.confirmButton') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

