<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
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

// Load user data
const loadUser = async () => {
  loading.value = true;
  try {
    await userStore.fetchUserById(userId);
  } catch (error: any) {
    console.error('Error loading user:', error);
  } finally {
    loading.value = false;
  }
};

onMounted(() => {
  loadUser();
});

watch(() => route.params.id, async (newId) => {
  if (newId) {
    await loadUser();
  }
});

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
  
  <v-card elevation="10" v-if="!loading || userStore.currentUser">
    <v-card-item>
      <div class="d-flex justify-space-between align-center mb-6">
        <h5 class="text-h5 font-weight-semibold">Kullanıcı Detayı</h5>
        <v-btn color="primary" @click="editUser" flat>
          <EditIcon class="mr-2" size="18" />
          Düzenle
        </v-btn>
      </div>

      <div v-if="loading && !userStore.currentUser" class="text-center py-8">
        <v-progress-circular indeterminate color="primary" />
        <p class="text-subtitle-1 mt-4">Yükleniyor...</p>
      </div>

      <div v-else-if="userStore.currentUser" class="pa-4">
        <v-row>
          <!-- User Info Card -->
          <v-col cols="12" md="6">
            <v-card variant="outlined" class="pa-4">
              <h6 class="text-h6 mb-4 font-weight-semibold">Kullanıcı Bilgileri</h6>
              
              <v-list lines="two" density="compact">
                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Kullanıcı Adı:</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ userStore.currentUser.username }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Email:</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ userStore.currentUser.email }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Ad Soyad:</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ userStore.currentUser.firstName }} {{ userStore.currentUser.lastName }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Durum:</v-label>
                  </template>
                  <v-list-item-title>
                    <v-chip :color="userStore.currentUser.isActive ? 'success' : 'error'" size="small" variant="flat">
                      {{ userStore.currentUser.isActive ? 'Aktif' : 'Pasif' }}
                    </v-chip>
                  </v-list-item-title>
                </v-list-item>
              </v-list>
            </v-card>
          </v-col>

          <!-- Groups & Metadata Card -->
          <v-col cols="12" md="6">
            <v-card variant="outlined" class="pa-4">
              <h6 class="text-h6 mb-4 font-weight-semibold">Gruplar ve Metadata</h6>
              
              <v-list lines="two" density="compact">
                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Gruplar:</v-label>
                  </template>
                  <v-list-item-title>
                    <div class="d-flex ga-1 flex-wrap mt-2">
                      <v-chip
                        v-for="group in userStore.currentUser.groups"
                        :key="group"
                        size="small"
                        color="primary"
                        variant="outlined"
                      >
                        {{ group }}
                      </v-chip>
                      <span v-if="!userStore.currentUser.groups || userStore.currentUser.groups.length === 0" class="text-caption text-medium-emphasis">
                        Grup yok
                      </span>
                    </div>
                  </v-list-item-title>
                </v-list-item>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Oluşturulma:</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ formatDate(userStore.currentUser.createdAt) }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item v-if="userStore.currentUser.lastLoginAt">
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Son Giriş:</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ formatDate(userStore.currentUser.lastLoginAt) }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item v-if="userStore.currentUser.updatedAt">
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Son Güncelleme:</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ formatDate(userStore.currentUser.updatedAt) }}
                  </v-list-item-title>
                </v-list-item>
              </v-list>
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

