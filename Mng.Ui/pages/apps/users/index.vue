<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
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

const headers = [
  { title: 'Kullanıcı Adı', key: 'username', sortable: true },
  { title: 'Email', key: 'email', sortable: true },
  { title: 'Ad Soyad', key: 'fullName', sortable: true },
  { title: 'Durum', key: 'isActive', sortable: true },
  { title: 'Gruplar', key: 'groups', sortable: false },
  { title: 'Oluşturulma', key: 'createdAt', sortable: true },
  { title: 'İşlemler', key: 'actions', sortable: false, align: 'end' },
];

// Computed: Full name
const usersWithFullName = computed(() => {
  return userStore.users.map(user => ({
    ...user,
    fullName: `${user.firstName} ${user.lastName}`.trim() || '-',
  }));
});

// Computed: Filtered users
const filteredUsers = computed(() => {
  let filtered = usersWithFullName.value;
  
  // Status filter
  if (statusFilter.value === 'active') {
    filtered = filtered.filter(u => u.isActive);
  } else if (statusFilter.value === 'inactive') {
    filtered = filtered.filter(u => !u.isActive);
  }
  
  return filtered;
});

onMounted(async () => {
  // Admin kontrolü
  if (!authStore.isAdmin) {
    console.warn('Kullanıcı admin değil. User management sayfasına erişim için admin yetkisi gereklidir.');
    // İsteğe bağlı: Kullanıcıyı başka bir sayfaya yönlendirebilirsiniz
    // router.push('/dashboards/analytical');
  }
  
  try {
    await userStore.fetchUsers({
      page: 1,
      pageSize: 100, // İlk yüklemede daha fazla kayıt
    });
  } catch (error) {
    console.error('Error loading users:', error);
    // Error zaten store'da tutuluyor, kullanıcıya gösterilebilir
  }
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

      <!-- Admin Warning -->
      <v-alert
        v-if="!authStore.isAdmin"
        type="warning"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        <strong>Uyarı:</strong> Bu sayfaya erişim için admin yetkisi gereklidir. 
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
        :headers="headers"
        :items="filteredUsers"
        :search="search"
        :loading="userStore.loading"
        :items-per-page="10"
        show-select
        item-value="id"
        class="border rounded-md"
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

