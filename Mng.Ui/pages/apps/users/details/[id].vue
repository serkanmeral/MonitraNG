<script setup lang="ts">
import { ref, onMounted, watch, nextTick } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useUserStore } from '@/stores/apps/user';
import { EditIcon } from 'vue-tabler-icons';

const route = useRoute();
const router = useRouter();
const userStore = useUserStore();

const userId = route.params.id as string;

const page = ref({ title: 'Kullanıcı Detayı' });
const breadcrumbs = ref([
  {
    text: 'Dashboard',
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: 'Kullanıcı Yönetimi',
    disabled: false,
    href: '/apps/users',
  },
  {
    text: 'Kullanıcı Detayı',
    disabled: true,
    href: '#',
  },
]);

const loading = ref(false);
const photoError = ref(false);

// Load user data
const loadUser = async () => {
  loading.value = true;
  photoError.value = false;
  try {
    await userStore.fetchUserById(userId);
    
    // Wait for reactivity to update using nextTick
    await nextTick();
  } catch (error: any) {
    // Error is handled by store
  } finally {
    loading.value = false;
  }
};

// Check if photo URL is valid
const hasValidPhoto = () => {
  const photoUrl = userStore.viewingUser?.photoUrl;
  return photoUrl && photoUrl.trim() !== '' && photoUrl !== null && !photoError.value;
};

// Handle photo load error
const handlePhotoError = () => {
  photoError.value = true;
};

// Handle photo load success
const handlePhotoLoad = () => {
  photoError.value = false;
};

onMounted(() => {
  loadUser();
});

watch(() => route.params.id, async (newId) => {
  if (newId) {
    photoError.value = false;
    await loadUser();
  }
});

// Watch for viewingUser changes to reset photo error
watch(() => userStore.viewingUser?.photoUrl, () => {
  photoError.value = false;
});

// Get correct photo URL
const getPhotoUrl = () => {
  const photoUrl = userStore.viewingUser?.photoUrl;
  if (!photoUrl) return null;
  
  let finalUrl: string = photoUrl;
  
  // If it's already a full URL (starts with http:// or https://), check and fix if needed
  if (photoUrl.startsWith('http://') || photoUrl.startsWith('https://')) {
    // Replace /keeper/api/ with /api/keeper/ in full URLs
    finalUrl = photoUrl.replace('/keeper/api/', '/api/keeper/');
  }
  // If it starts with /api/keeper/, return as is
  else if (photoUrl.startsWith('/api/keeper/')) {
    finalUrl = photoUrl;
  }
  // If it starts with /keeper/api/, replace with /api/keeper/
  else if (photoUrl.startsWith('/keeper/api/')) {
    finalUrl = photoUrl.replace('/keeper/api/', '/api/keeper/');
  }
  // If it contains /keeper/api/ anywhere, replace it
  else if (photoUrl.includes('/keeper/api/')) {
    finalUrl = photoUrl.replace('/keeper/api/', '/api/keeper/');
  }
  // If it starts with /, add /api/keeper/ prefix
  else if (photoUrl.startsWith('/')) {
    finalUrl = `/api/keeper${photoUrl}`;
  }
  // Otherwise, add /api/keeper/ prefix
  else {
    finalUrl = `/api/keeper/${photoUrl}`;
  }
  
  return finalUrl;
};

const formatDate = (date: string | Date | null | undefined) => {
  if (!date) return '-';
  try {
    return new Date(date).toLocaleString('tr-TR', {
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

const editUser = () => {
  router.push(`/apps/users/edit/${userId}`);
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10" v-if="!loading || userStore.viewingUser">
    <v-card-item>
      <div class="d-flex justify-space-between align-center mb-6">
        <div>
          <h5 class="text-h5 font-weight-semibold mb-1">Kullanıcı Detayı</h5>
          <div class="text-caption text-medium-emphasis" v-if="userStore.viewingUser">
            <v-icon size="14" class="mr-1">mdi-identifier</v-icon>
            ID: {{ userStore.viewingUser.id || userStore.viewingUser.userId }}
          </div>
        </div>
        <v-btn color="primary" @click="editUser" flat>
          <EditIcon class="mr-2" size="18" />
          Düzenle
        </v-btn>
      </div>

      <div v-if="loading && !userStore.viewingUser" class="text-center py-8">
        <v-progress-circular indeterminate color="primary" />
        <p class="text-subtitle-1 mt-4">Yükleniyor...</p>
      </div>

      <div v-else-if="userStore.viewingUser" class="pa-4">
        <v-row>
          <!-- User Info Card -->
          <v-col cols="12" md="6">
            <v-card variant="outlined" class="pa-4">
              <div class="d-flex align-center mb-4">
                <v-icon class="mr-2" color="primary">mdi-account-circle</v-icon>
                <h6 class="text-h6 font-weight-semibold">Kullanıcı Bilgileri</h6>
              </div>
              
              <v-list lines="two" density="comfortable">
                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-identifier</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Kullanıcı ID</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1 font-mono">
                    {{ userStore.viewingUser.id || userStore.viewingUser.userId }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2"></v-divider>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-account</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Kullanıcı Adı</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.username }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2"></v-divider>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-email</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Email</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.email }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2"></v-divider>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-account-box</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Ad Soyad</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.firstName }} {{ userStore.viewingUser.lastName }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-list-item v-if="userStore.viewingUser.title">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-briefcase</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Ünvan</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.title }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-list-item v-if="userStore.viewingUser.department">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-office-building</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Departman</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.department }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-list-item v-if="userStore.viewingUser.phoneNumber">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-phone</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Telefon</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.phoneNumber }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2"></v-divider>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-gender-male-female</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Cinsiyet</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ 
                      userStore.viewingUser.gender === 1 ? 'Erkek' : 
                      userStore.viewingUser.gender === 2 ? 'Kadın' : 
                      'Belirtilmemiş' 
                    }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2"></v-divider>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" :color="userStore.viewingUser.isActive ? 'success' : 'error'">mdi-check-circle</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Durum</v-list-item-title>
                  <v-list-item-subtitle>
                    <v-chip :color="userStore.viewingUser.isActive ? 'success' : 'error'" size="small" variant="flat">
                      {{ userStore.viewingUser.isActive ? 'Aktif' : 'Pasif' }}
                    </v-chip>
                  </v-list-item-subtitle>
                </v-list-item>
              </v-list>
            </v-card>
          </v-col>

          <!-- Groups & Metadata Card -->
          <v-col cols="12" md="6">
            <v-card variant="outlined" class="pa-4">
              <div class="d-flex align-center mb-4">
                <v-icon class="mr-2" color="primary">mdi-account-group</v-icon>
                <h6 class="text-h6 font-weight-semibold">Gruplar ve Roller</h6>
              </div>
              
              <v-list lines="two" density="comfortable">
                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-account-multiple</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-2">Gruplar</v-list-item-title>
                  <v-list-item-subtitle>
                    <div class="d-flex ga-1 flex-wrap">
                      <v-chip
                        v-for="group in userStore.viewingUser.groups"
                        :key="group"
                        size="small"
                        color="primary"
                        variant="outlined"
                      >
                        {{ group }}
                      </v-chip>
                      <span v-if="!userStore.viewingUser.groups || userStore.viewingUser.groups.length === 0" class="text-caption text-medium-emphasis">
                        Grup yok
                      </span>
                    </div>
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2" v-if="userStore.viewingUser.roles && userStore.viewingUser.roles.length > 0"></v-divider>

                <v-list-item v-if="userStore.viewingUser.roles && userStore.viewingUser.roles.length > 0">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-shield-account</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-2">Roller</v-list-item-title>
                  <v-list-item-subtitle>
                    <div class="d-flex ga-1 flex-wrap">
                      <v-chip
                        v-for="role in userStore.viewingUser.roles"
                        :key="role"
                        size="small"
                        color="secondary"
                        variant="outlined"
                      >
                        {{ role }}
                      </v-chip>
                    </div>
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2"></v-divider>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-calendar-plus</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Oluşturulma</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ formatDate(userStore.viewingUser.createdAt) }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-list-item v-if="userStore.viewingUser.createdBy">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-account-plus</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Oluşturan</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.createdBy }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2" v-if="userStore.viewingUser.lastLoginAt"></v-divider>

                <v-list-item v-if="userStore.viewingUser.lastLoginAt">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-login</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Son Giriş</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ formatDate(userStore.viewingUser.lastLoginAt) }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2" v-if="userStore.viewingUser.updatedAt"></v-divider>

                <v-list-item v-if="userStore.viewingUser.updatedAt">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-calendar-edit</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Son Güncelleme</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ formatDate(userStore.viewingUser.updatedAt) }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-list-item v-if="userStore.viewingUser.updatedBy">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-account-edit</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">Güncelleyen</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.updatedBy }}
                  </v-list-item-subtitle>
                </v-list-item>
              </v-list>

              <!-- Photo Section -->
              <v-divider class="my-4"></v-divider>
              <div class="text-center">
                <div class="d-flex align-center justify-center mb-3">
                  <v-icon class="mr-2" color="primary">mdi-camera</v-icon>
                  <h6 class="text-h6 font-weight-semibold">Fotoğraf</h6>
                </div>
                <div class="d-flex justify-center mb-2">
                  <div v-if="hasValidPhoto()" style="position: relative; width: 200px; height: 200px; border-radius: 8px; overflow: hidden; border: 2px solid rgba(var(--v-theme-on-surface), 0.12);">
                    <img 
                      :src="getPhotoUrl()" 
                      :alt="`${userStore.viewingUser.firstName} ${userStore.viewingUser.lastName}`"
                      style="width: 100%; height: 100%; object-fit: cover; display: block;"
                      @error="handlePhotoError"
                      @load="handlePhotoLoad"
                    />
                  </div>
                  <v-avatar v-else size="200" class="mb-2">
                    <v-icon size="100" color="grey-lighten-1">
                      mdi-account-circle
                    </v-icon>
                  </v-avatar>
                </div>
                <div v-if="hasValidPhoto()" class="text-caption text-medium-emphasis mt-2 text-truncate" style="max-width: 100%;">
                  {{ getPhotoUrl() }}
                </div>
                <div v-else class="text-caption text-medium-emphasis mt-2">
                  {{ userStore.viewingUser?.photoUrl ? 'Fotoğraf yüklenemedi' : 'Fotoğraf yok' }}
                </div>
              </div>
            </v-card>
          </v-col>
        </v-row>

        <!-- Actions -->
        <div class="d-flex justify-end ga-3 mt-6">
          <v-btn color="primary" variant="flat" @click="router.push('/apps/users')">
            Listeye Dön
          </v-btn>
        </div>
      </div>

      <div v-else class="text-center py-8">
        <p class="text-subtitle-1 text-medium-emphasis">Kullanıcı bulunamadı</p>
        <v-btn color="primary" @click="router.push('/apps/users')" class="mt-4">
          Listeye Dön
        </v-btn>
      </div>
    </v-card-item>
  </v-card>
</template>

