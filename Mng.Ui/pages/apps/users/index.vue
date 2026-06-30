<script setup lang="ts">
import { ref, onMounted, computed, watch, nextTick } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useLocaleStore } from '@/stores/locale';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useUserStore } from '@/stores/apps/user';
import { useAuthStore } from '@/stores/auth';
import { canDeleteUser, userProvisioningSourceLabelKey } from '@/utils/userFieldPolicy';
import { provisioningSourceChipColor } from '@/utils/provisioningSourceUi';
import { EditIcon, EyeIcon, TrashIcon, UserPlusIcon, RefreshIcon, DownloadIcon } from 'vue-tabler-icons';

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

const userStore = useUserStore();
const authStore = useAuthStore();
const localeStore = useLocaleStore();
const router = useRouter();
const route = useRoute();

const page = computed(() => ({ 
  title: t('users.title') 
}));
const breadcrumbs = computed(() => [
  {
    text: t('users.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('users.breadcrumbs.users'),
    disabled: true,
    href: '#',
  },
]);

const search = ref('');
const selectedUsers = ref([]);
const showDeleteDialog = ref(false);
const userToDelete = ref<string | null>(null);
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
  { title: t('users.table.username'), key: 'username', sortable: true },
  { title: t('users.table.source'), key: 'provisioningSource', sortable: false },
  { title: t('users.table.email'), key: 'email', sortable: true },
  { title: t('users.table.fullName'), key: 'fullName', sortable: true },
  { title: t('users.table.status'), key: 'isActive', sortable: true },
  { title: t('users.applicationScope.column'), key: 'includeInApplication', sortable: false },
  { title: t('users.table.groups'), key: 'groups', sortable: false },
  { title: t('users.table.createdAt'), key: 'createdAt', sortable: true },
  { title: t('users.table.actions'), key: 'actions', sortable: false, align: 'end' },
]);

// Computed: Full name (for display only)
const usersWithFullName = computed(() => {
  return userStore.users.map(user => ({
    ...user,
    fullName: `${user.firstName} ${user.lastName}`.trim() || '-',
  }));
});

// Computed: Server items length (reactive)
const serverItemsLength = computed(() => {
  return userStore.totalCount || 0;
});

  // Fetch users function
  const fetchUsers = async () => {
    // Always use current tableOptions.value to ensure we have the latest values
    // Build query parameters for server-side pagination/filtering
    const params: {
      page?: number;
      pageSize?: number;
      search?: string;
      isActive?: boolean;
      sortBy?: string;
      sortOrder?: 'asc' | 'desc';
    } = {
      page: tableOptions.value.page || 1,
      pageSize: tableOptions.value.itemsPerPage || 10,
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
    
    // Add sorting if exists
    if (tableOptions.value.sortBy && tableOptions.value.sortBy.length > 0) {
      const sortOption = tableOptions.value.sortBy[0]; // Vuetify supports single sort
      // Map frontend field names to backend field names
      const fieldMapping: Record<string, string> = {
        'username': 'username',
        'email': 'email',
        'fullName': 'firstName', // Backend uses firstName for sorting
        'isActive': 'isActive',
        'createdAt': 'createdAt',
      };
      
      const backendField = fieldMapping[sortOption.key] || sortOption.key;
      if (backendField) {
        params.sortBy = backendField;
        params.sortOrder = sortOption.order || 'asc';
      }
    }
    
    // Fetch users with new parameters
    try {
      await userStore.fetchUsers(params);
    } catch (error) {
      console.error('Error loading users:', error);
    }
  };

  // Handle table options update (when user changes page or items per page)
  const handleTableOptionsUpdate = (newOptions: any) => {
    // Update tableOptions with new values
    tableOptions.value = {
      ...tableOptions.value,
      ...newOptions,
    };
    
    // Skip initial load (handled in onMounted)
    if (isInitialLoad.value) {
      return;
    }
    
    // Fetch users (will use updated tableOptions.value)
    fetchUsers();
  };

// Flag to prevent handler from triggering on initial load
const isInitialLoad = ref(true);

// Watch tableOptions.itemsPerPage for changes (when user changes items per page via custom select)
watch(
  () => tableOptions.value.itemsPerPage,
  async (newItemsPerPage, oldItemsPerPage) => {
    // Skip initial load (handled in onMounted)
    if (isInitialLoad.value) {
      return;
    }
    
    // Only fetch if itemsPerPage actually changed
    if (newItemsPerPage !== oldItemsPerPage && oldItemsPerPage !== undefined) {
      // Wait for next tick to ensure tableOptions.value is fully updated
      await nextTick();
      // Reset to first page when items per page changes
      tableOptions.value.page = 1;
      // Wait again after page reset
      await nextTick();
      fetchUsers();
    }
  }
);

// Watch tableOptions.page for changes (when user changes page via pagination)
watch(
  () => tableOptions.value.page,
  async (newPage, oldPage) => {
    // Skip initial load (handled in onMounted)
    if (isInitialLoad.value) {
      return;
    }
    
    // Only fetch if page actually changed
    if (newPage !== oldPage && oldPage !== undefined) {
      // Wait for next tick to ensure tableOptions.value is fully updated
      await nextTick();
      fetchUsers();
    }
  }
);

// Watch tableOptions.sortBy for changes (when user changes sorting)
watch(
  () => tableOptions.value.sortBy,
  async (newSortBy, oldSortBy) => {
    // Skip initial load (handled in onMounted)
    if (isInitialLoad.value) {
      return;
    }
    
    // Only fetch if sorting actually changed
    const newSortStr = JSON.stringify(newSortBy || []);
    const oldSortStr = JSON.stringify(oldSortBy || []);
    if (newSortStr !== oldSortStr) {
      // Reset to first page when sorting changes
      if (tableOptions.value.page !== 1) {
        tableOptions.value.page = 1;
      }
      await nextTick();
      fetchUsers();
    }
  },
  { deep: true }
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
    fetchUsers();
  }, 500);
});

onMounted(async () => {
  // Manager/Admin kontrolü
  if (!authStore.isManager) {
    // İsteğe bağlı: Kullanıcıyı başka bir sayfaya yönlendirebilirsiniz
    // router.push('/dashboards/analytical');
  }
  
  // Initial load
  isInitialLoad.value = true;
  await fetchUsers();
  isInitialLoad.value = false;
});

// Watch route query for refresh parameter
watch(() => route.query.refresh, async (refreshParam) => {
  if (refreshParam) {
    // Reset to first page when returning to list (to see newly created items)
    if (tableOptions.value.page !== 1) {
      tableOptions.value.page = 1;
    }
    // Refresh users list
    await fetchUsers();
    // Remove refresh parameter from URL
    router.replace({ path: route.path, query: {} });
  }
});

// Manual refresh function
const refreshUsers = async () => {
  await fetchUsers();
};

// Export users function
const exportUsers = async (format: 'csv' | 'xlsx' | 'json') => {
  exportError.value = '';
  isExporting.value = true;
  
  try {
    const params: {
      search?: string;
      isActive?: boolean;
      sortBy?: string;
      sortOrder?: 'asc' | 'desc';
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
    
    // Add sorting if exists
    if (tableOptions.value.sortBy && tableOptions.value.sortBy.length > 0) {
      const sortOption = tableOptions.value.sortBy[0];
      const fieldMapping: Record<string, string> = {
        'username': 'username',
        'email': 'email',
        'fullName': 'firstName',
        'isActive': 'isActive',
        'createdAt': 'createdAt',
      };
      
      const backendField = fieldMapping[sortOption.key] || sortOption.key;
      if (backendField) {
        params.sortBy = backendField;
        params.sortOrder = sortOption.order || 'asc';
      }
    }
    
    await userStore.exportUsers(format, params);
    
    // Success - file will be downloaded automatically
  } catch (error: any) {
    exportError.value = error.message || t('users.export.error');
    console.error('Export error:', error);
  } finally {
    isExporting.value = false;
  }
};

const editUser = (user: any) => {
  // Try id first (if not empty), then userId, then keycloakUserId
  const userId = (user.id && user.id.trim() !== '') ? user.id : (user.userId || user.keycloakUserId);
  
  if (userId && userId.trim() !== '') {
    router.push(`/apps/users/edit/${userId}`);
  } else {
    userStore.error = t('users.errors.userIdNotFound');
  }
};

const viewUser = (user: any) => {
  // Try id first (if not empty), then userId, then keycloakUserId
  const userId = (user.id && user.id.trim() !== '') ? user.id : (user.userId || user.keycloakUserId);
  
  if (userId && userId.trim() !== '') {
    router.push(`/apps/users/details/${userId}`);
  } else {
    userStore.error = t('users.errors.userIdNotFound');
  }
};

const deleteUser = (user: any) => {
  userToDelete.value = user.id || user.userId;
  showDeleteDialog.value = true;
};

const confirmDelete = async () => {
  if (userToDelete.value) {
    deleteError.value = '';
    try {
      await userStore.deleteUser(userToDelete.value);
      showDeleteDialog.value = false;
      userToDelete.value = null;
      // Refresh list after deletion
      await fetchUsers();
    } catch (error: any) {
      // Show error message in dialog
      deleteError.value = error.message || t('users.errors.deleteFailed');
      console.error('Error deleting user:', error);
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
            :label="t('users.search.placeholder')"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 300px;"
            clearable
          />
          
          <v-select
            v-model="statusFilter"
            :items="[
              { title: t('users.status.all'), value: 'all' },
              { title: t('users.status.active'), value: 'active' },
              { title: t('users.status.inactive'), value: 'inactive' },
            ]"
            :label="t('users.status.label')"
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
                {{ t('users.buttons.export') }}
                <v-icon class="ml-1" size="16">mdi-chevron-down</v-icon>
              </v-btn>
            </template>
            <v-list>
              <v-list-item @click="exportUsers('csv')">
                <template v-slot:prepend>
                  <v-icon>mdi-file-delimited</v-icon>
                </template>
                <v-list-item-title>{{ t('users.export.csv') }}</v-list-item-title>
              </v-list-item>
              <v-list-item @click="exportUsers('json')">
                <template v-slot:prepend>
                  <v-icon>mdi-code-json</v-icon>
                </template>
                <v-list-item-title>{{ t('users.export.json') }}</v-list-item-title>
              </v-list-item>
              <v-list-item @click="exportUsers('xlsx')" disabled>
                <template v-slot:prepend>
                  <v-icon>mdi-file-excel</v-icon>
                </template>
                <v-list-item-title>{{ t('users.export.xlsx') }}</v-list-item-title>
                <template v-slot:append>
                  <v-chip size="x-small" color="warning">{{ t('users.export.comingSoon') }}</v-chip>
                </template>
              </v-list-item>
            </v-list>
          </v-menu>
          
          <v-btn
            color="primary"
            variant="outlined"
            flat
            @click="refreshUsers"
            :loading="userStore.loading"
          >
            <RefreshIcon class="mr-2" size="20" />
            {{ t('users.buttons.refresh') }}
          </v-btn>
          <v-btn color="primary" to="/apps/users/create" flat>
            <UserPlusIcon class="mr-2" size="20" />
            {{ t('users.buttons.newUser') }}
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
        <strong>{{ t('users.warning.title') }}</strong> {{ t('users.warning.message') }}
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
        v-if="userStore.error"
        type="error"
        variant="tonal"
        density="compact"
        class="mb-4"
        closable
        @click:close="userStore.clearError"
      >
        {{ userStore.error }}
        <div v-if="userStore.error.includes('403') || userStore.error.includes('Forbidden')" class="mt-2 text-caption">
          <strong>{{ t('users.errors.forbiddenNote') }}</strong>
        </div>
      </v-alert>

      <!-- Data Table -->
      <v-data-table
        v-model="selectedUsers"
        v-model:options="tableOptions"
        :headers="headers"
        :items="usersWithFullName"
        :loading="userStore.loading"
        :server-items-length="serverItemsLength"
        :items-per-page="tableOptions.itemsPerPage"
        :items-per-page-options="[10, 25, 50, 100]"
        show-select
        item-value="id"
        class="border rounded-md"
        hide-default-footer
        @update:options="handleTableOptionsUpdate"
      >
        <!-- Provisioning source -->
        <template v-slot:item.provisioningSource="{ item }">
          <v-chip
            size="small"
            variant="tonal"
            :color="provisioningSourceChipColor(item.provisioningSource)"
          >
            {{ t(userProvisioningSourceLabelKey(item.provisioningSource)) }}
          </v-chip>
        </template>

        <!-- Full Name Column -->
        <template v-slot:item.fullName="{ item }">
          <span class="text-subtitle-1 font-weight-medium">
            {{ item.fullName }}
          </span>
        </template>

        <!-- Status Column -->
        <template v-slot:item.isActive="{ value }">
          <v-chip :color="value ? 'success' : 'error'" size="small" variant="flat">
            {{ value ? t('users.status.active') : t('users.status.inactive') }}
          </v-chip>
        </template>

        <template v-slot:item.includeInApplication="{ value }">
          <v-chip :color="value ? 'primary' : 'warning'" size="small" variant="tonal">
            {{ value ? t('users.applicationScope.inApp') : t('users.applicationScope.hidden') }}
          </v-chip>
        </template>

        <!-- Groups Column -->
        <template v-slot:item.groups="{ item }">
          <div class="d-flex ga-1 flex-wrap">
            <v-chip
              v-for="group in item.groups"
              :key="group"
              size="small"
              color="primary"
              variant="outlined"
            >
              {{ group }}
            </v-chip>
            <span v-if="!item.groups || item.groups.length === 0" class="text-caption text-medium-emphasis">
              {{ t('users.groups.none') }}
            </span>
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
              @click="viewUser(item)"
            >
              <EyeIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('users.actions.view') }}</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="info"
              @click="editUser(item)"
            >
              <EditIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('users.actions.edit') }}</v-tooltip>
            </v-btn>
            <v-btn
              v-if="canDeleteUser(item)"
              icon
              size="small"
              variant="text"
              color="error"
              @click="deleteUser(item)"
            >
              <TrashIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('users.actions.delete') }}</v-tooltip>
            </v-btn>
          </div>
        </template>

        <!-- No Data -->
        <template v-slot:no-data>
          <div class="text-center py-8">
            <p class="text-subtitle-1 text-medium-emphasis">{{ t('users.noData.title') }}</p>
            <v-btn color="primary" to="/apps/users/create" class="mt-4">
              <UserPlusIcon class="mr-2" size="20" />
              {{ t('users.noData.button') }}
            </v-btn>
          </div>
        </template>

        <!-- Custom bottom slot with total count and pagination -->
        <template v-slot:bottom>
          <div class="d-flex justify-space-between align-center pa-3 border-top">
            <div class="text-caption text-medium-emphasis">
              <strong>{{ t('users.pagination.total') }}</strong> {{ userStore.totalCount }} {{ t('users.pagination.records') }}
              <span v-if="userStore.totalPages > 1" class="ml-2">
                ({{ t('users.pagination.page') }} {{ tableOptions.page }} / {{ userStore.totalPages }})
              </span>
              <span class="ml-2">
                | {{ ((tableOptions.page - 1) * tableOptions.itemsPerPage) + 1 }} - 
                {{ Math.min(tableOptions.page * tableOptions.itemsPerPage, userStore.totalCount) }} 
                / {{ userStore.totalCount }} {{ t('users.pagination.showing') }}
              </span>
            </div>
            <div class="d-flex align-center ga-2">
              <span class="text-caption text-medium-emphasis">{{ t('users.pagination.itemsPerPage') }}</span>
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
              :length="userStore.totalPages"
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
        <span class="text-h6">{{ t('users.delete.title') }}</span>
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
          {{ t('users.delete.message') }}
        </p>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn 
          color="error" 
          variant="flat" 
          @click="showDeleteDialog = false; deleteError = ''; userToDelete = null"
        >
          {{ deleteError ? t('users.delete.close') : t('users.delete.cancel') }}
        </v-btn>
        <v-btn 
          v-if="!deleteError"
          color="success" 
          variant="flat" 
          @click="confirmDelete"
          :loading="userStore.loading"
        >
          {{ t('users.delete.confirm') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

