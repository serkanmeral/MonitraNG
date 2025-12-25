<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import GroupUserManagementModal from '@/components/apps/groups/GroupUserManagementModal.vue';
import { useGroupStore } from '@/stores/apps/group';
import { EditIcon, UserPlusIcon, UsersIcon } from 'vue-tabler-icons';

const route = useRoute();
const router = useRouter();
const groupStore = useGroupStore();

const groupId = route.params.id as string;

const page = ref({ title: 'Grup Detayı' });
const breadcrumbs = ref([
  {
    text: 'Dashboard',
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: 'Grup Yönetimi',
    disabled: false,
    href: '/apps/groups',
  },
  {
    text: 'Grup Detayı',
    disabled: true,
    href: '#',
  },
]);

const loading = ref(false);
const showUserManagementModal = ref(false);

// Load group data
const loadGroup = async () => {
  loading.value = true;
  try {
    await groupStore.fetchGroupById(groupId);
  } catch (error: any) {
    console.error('Error loading group:', error);
  } finally {
    loading.value = false;
  }
};

onMounted(() => {
  loadGroup();
});

watch(() => route.params.id, async (newId) => {
  if (newId) {
    await loadGroup();
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

const editGroup = () => {
  router.push(`/apps/groups/edit/${groupId}`);
};

const openUserManagement = () => {
  showUserManagementModal.value = true;
};

const onUserManagementUpdated = async () => {
  // Modal'da kullanıcı ekleme/çıkarma yapıldığında grup bilgilerini yenile
  await loadGroup();
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10" v-if="!loading || groupStore.currentGroup">
    <v-card-item>
      <div class="d-flex justify-space-between align-center mb-6">
        <h5 class="text-h5 font-weight-semibold">Grup Detayı</h5>
        <div class="d-flex ga-2">
          <v-btn color="info" @click="openUserManagement" flat>
            <UserPlusIcon class="mr-2" size="18" />
            Üye Yönetimi
          </v-btn>
          <v-btn color="primary" @click="editGroup" flat>
            <EditIcon class="mr-2" size="18" />
            Düzenle
          </v-btn>
        </div>
      </div>

      <div v-if="loading && !groupStore.currentGroup" class="text-center py-8">
        <v-progress-circular indeterminate color="primary" />
        <p class="text-subtitle-1 mt-4">Yükleniyor...</p>
      </div>

      <div v-else-if="groupStore.currentGroup" class="pa-4">
        <v-row>
          <!-- Group Info Card -->
          <v-col cols="12" md="6">
            <v-card variant="outlined" class="pa-4">
              <h6 class="text-h6 mb-4 font-weight-semibold">Grup Bilgileri</h6>
              
              <v-list lines="two" density="compact">
                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Grup Adı:</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ groupStore.currentGroup.name }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item v-if="groupStore.currentGroup.description">
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Açıklama:</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ groupStore.currentGroup.description }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Durum:</v-label>
                  </template>
                  <v-list-item-title>
                    <v-chip :color="groupStore.currentGroup.isActive ? 'success' : 'error'" size="small" variant="flat">
                      {{ groupStore.currentGroup.isActive ? 'Aktif' : 'Pasif' }}
                    </v-chip>
                  </v-list-item-title>
                </v-list-item>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Üye Sayısı:</v-label>
                  </template>
                  <v-list-item-title>
                    <div class="d-flex align-center ga-2">
                      <UsersIcon size="18" />
                      <v-chip size="small" color="info" variant="outlined">
                        {{ groupStore.currentGroup.memberCount || 0 }}
                      </v-chip>
                    </div>
                  </v-list-item-title>
                </v-list-item>
              </v-list>
            </v-card>
          </v-col>

          <!-- Metadata Card -->
          <v-col cols="12" md="6">
            <v-card variant="outlined" class="pa-4">
              <h6 class="text-h6 mb-4 font-weight-semibold">Metadata</h6>
              
              <v-list lines="two" density="compact">
                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Oluşturulma:</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ formatDate(groupStore.currentGroup.createdAt) }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item v-if="groupStore.currentGroup.updatedAt">
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Son Güncelleme:</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ formatDate(groupStore.currentGroup.updatedAt) }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item v-if="groupStore.currentGroup.createdBy">
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Oluşturan:</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ groupStore.currentGroup.createdBy }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item v-if="groupStore.currentGroup.updatedBy">
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">Güncelleyen:</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ groupStore.currentGroup.updatedBy }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item v-if="groupStore.currentGroup.permissions && groupStore.currentGroup.permissions.length > 0">
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">İzinler:</v-label>
                  </template>
                  <v-list-item-title>
                    <div class="d-flex ga-1 flex-wrap mt-2">
                      <v-chip
                        v-for="permission in groupStore.currentGroup.permissions"
                        :key="permission"
                        size="small"
                        color="primary"
                        variant="outlined"
                      >
                        {{ permission }}
                      </v-chip>
                    </div>
                  </v-list-item-title>
                </v-list-item>
              </v-list>
            </v-card>
          </v-col>
        </v-row>

        <!-- Actions -->
        <div class="d-flex justify-end ga-3 mt-6">
          <v-btn color="primary" variant="flat" @click="router.push('/apps/groups')">
            Listeye Dön
          </v-btn>
        </div>
      </div>

      <div v-else class="text-center py-8">
        <p class="text-subtitle-1 text-medium-emphasis">Grup bulunamadı</p>
        <v-btn color="primary" @click="router.push('/apps/groups')" class="mt-4">
          Listeye Dön
        </v-btn>
      </div>
    </v-card-item>
  </v-card>

  <!-- User Management Modal -->
  <GroupUserManagementModal
    v-if="groupStore.currentGroup"
    :groupId="groupId"
    :groupName="groupStore.currentGroup.name"
    :isOpen="showUserManagementModal"
    @close="showUserManagementModal = false"
    @updated="onUserManagementUpdated"
  />
</template>

