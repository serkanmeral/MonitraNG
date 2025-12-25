<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useUserStore } from '@/stores/apps/user';
import { useAuthStore } from '@/stores/auth';
import { EditIcon, EyeIcon, TrashIcon, UserPlusIcon } from 'vue-tabler-icons';

const userStore = useUserStore();
const authStore = useAuthStore();
const router = useRouter();

const page = ref({ title: 'Kullanıcı Yönetimi' });
const breadcrumbs = ref([
  {
    text: 'Dashboard',
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: 'Kullanıcı Yönetimi',
    disabled: true,
    href: '#',
  },
]);

const search = ref('');
const selectedUsers = ref([]);
const showDeleteDialog = ref(false);
const userToDelete = ref<string | null>(null);
const statusFilter = ref<string>('all'); // all, active, inactive

// Server-side pagination options
const tableOptions = ref({
  page: 1,
  itemsPerPage: 10,
  sortBy: [] as Array<{ key: string; order: 'asc' | 'desc' }>,
});

const headers = [
  { title: 'Kullanıcı Adı', key: 'username', sortable: true },
  { title: 'Email', key: 'email', sortable: true },
  { title: 'Ad Soyad', key: 'fullName', sortable: true },
  { title: 'Durum', key: 'isActive', sortable: true },
  { title: 'Gruplar', key: 'groups', sortable: false },
  { title: 'Oluşturulma', key: 'createdAt', sortable: true },
  { title: 'İşlemler', key: 'actions', sortable: false, align: 'end' },
];

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
  const fetchUsers = async (options?: any) => {
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
    
    // Fetch users with new parameters
    try {
      await userStore.fetchUsers(params);
    } catch (error) {
      console.error('Error loading users:', error);
    }
  };

// Flag to prevent watch from triggering on initial load
const isInitialLoad = ref(true);

// Watch tableOptions for changes (pagination, itemsPerPage, sorting)
// This will trigger when user changes page or items per page
watch(
  () => [tableOptions.value.page, tableOptions.value.itemsPerPage],
  () => {
    // Skip initial load (handled in onMounted)
    if (isInitialLoad.value) {
      return;
    }
    fetchUsers();
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
    fetchUsers();
  }, 500);
});

onMounted(async () => {
  // Manager/Admin kontrolü
  if (!authStore.isManager) {
    console.warn('Kullanıcı manager veya admin değil. User management sayfasına erişim için manager/admin yetkisi gereklidir.');
    // İsteğe bağlı: Kullanıcıyı başka bir sayfaya yönlendirebilirsiniz
    // router.push('/dashboards/analytical');
  }
  
  // Initial load
  isInitialLoad.value = true;
  await fetchUsers();
  isInitialLoad.value = false;
});

const editUser = (user: any) => {
  router.push(`/apps/users/edit/${user.id || user.userId}`);
};

const viewUser = (user: any) => {
  router.push(`/apps/users/details/${user.id || user.userId}`);
};

const deleteUser = (user: any) => {
  userToDelete.value = user.id || user.userId;
  showDeleteDialog.value = true;
};

const confirmDelete = async () => {
  if (userToDelete.value) {
    try {
      await userStore.deleteUser(userToDelete.value);
      showDeleteDialog.value = false;
      userToDelete.value = null;
    } catch (error) {
      console.error('Error deleting user:', error);
    }
  }
};

const formatDate = (date: string | Date | null | undefined) => {
  if (!date) return '-';
  try {
    return new Date(date).toLocaleDateString('tr-TR', {
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
            label="Kullanıcı Ara"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 300px;"
            clearable
          />
          
          <v-select
            v-model="statusFilter"
            :items="[
              { title: 'Tümü', value: 'all' },
              { title: 'Aktif', value: 'active' },
              { title: 'Pasif', value: 'inactive' },
            ]"
            label="Durum"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 150px;"
          />
        </div>
        
        <v-btn color="primary" to="/apps/users/create" flat>
          <UserPlusIcon class="mr-2" size="20" />
          Yeni Kullanıcı
        </v-btn>
      </div>

      <!-- Manager/Admin Warning -->
      <v-alert
        v-if="!authStore.isManager"
        type="warning"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        <strong>Uyarı:</strong> Bu sayfaya erişim için manager veya admin yetkisi gereklidir. 
        Kullanıcı yönetimi işlemlerini gerçekleştirmek için lütfen bir yönetici ile iletişime geçin.
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
          <strong>Not:</strong> Bu hata, admin yetkisi gerektirdiğini gösterir. Kullanıcınızın "admins" grubunda olduğundan emin olun.
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
        :items-per-page-options="[10, 25, 50, 100]"
        show-select
        item-value="id"
        class="border rounded-md"
        hide-default-footer
      >
        <!-- Full Name Column -->
        <template v-slot:item.fullName="{ item }">
          <span class="text-subtitle-1 font-weight-medium">
            {{ item.fullName }}
          </span>
        </template>

        <!-- Status Column -->
        <template v-slot:item.isActive="{ value }">
          <v-chip :color="value ? 'success' : 'error'" size="small" variant="flat">
            {{ value ? 'Aktif' : 'Pasif' }}
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
              Grup yok
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
              <v-tooltip activator="parent" location="top">Görüntüle</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="info"
              @click="editUser(item)"
            >
              <EditIcon size="18" />
              <v-tooltip activator="parent" location="top">Düzenle</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="error"
              @click="deleteUser(item)"
            >
              <TrashIcon size="18" />
              <v-tooltip activator="parent" location="top">Sil</v-tooltip>
            </v-btn>
          </div>
        </template>

        <!-- No Data -->
        <template v-slot:no-data>
          <div class="text-center py-8">
            <p class="text-subtitle-1 text-medium-emphasis">Kullanıcı bulunamadı</p>
            <v-btn color="primary" to="/apps/users/create" class="mt-4">
              <UserPlusIcon class="mr-2" size="20" />
              İlk Kullanıcıyı Oluştur
            </v-btn>
          </div>
        </template>

        <!-- Custom bottom slot with total count and pagination -->
        <template v-slot:bottom>
          <div class="d-flex justify-space-between align-center pa-3 border-top">
            <div class="text-caption text-medium-emphasis">
              <strong>Toplam:</strong> {{ userStore.totalCount }} kayıt
              <span v-if="userStore.totalPages > 1" class="ml-2">
                (Sayfa {{ tableOptions.page }} / {{ userStore.totalPages }})
              </span>
              <span class="ml-2">
                | {{ ((tableOptions.page - 1) * tableOptions.itemsPerPage) + 1 }} - 
                {{ Math.min(tableOptions.page * tableOptions.itemsPerPage, userStore.totalCount) }} 
                / {{ userStore.totalCount }} gösteriliyor
              </span>
            </div>
            <div class="d-flex align-center ga-2">
              <span class="text-caption text-medium-emphasis">Sayfa başına kayıt:</span>
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
        <span class="text-h6">Kullanıcıyı Sil</span>
      </v-card-title>
      <v-card-text class="pa-6">
        <p class="text-subtitle-1">
          Bu kullanıcıyı silmek istediğinizden emin misiniz? Bu işlem geri alınamaz.
        </p>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn color="error" variant="flat" @click="showDeleteDialog = false">
          İptal
        </v-btn>
        <v-btn color="success" variant="flat" @click="confirmDelete">
          Evet, Sil
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

