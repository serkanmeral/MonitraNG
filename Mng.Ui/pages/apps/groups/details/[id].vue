<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useLocaleStore } from '@/stores/locale';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import GroupUserManagementModal from '@/components/apps/groups/GroupUserManagementModal.vue';
import { useGroupStore } from '@/stores/apps/group';
import { useGroupFieldPolicies } from '@/composables/useGroupFieldPolicies';
import { EditIcon, UserPlusIcon, UsersIcon } from 'vue-tabler-icons';

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
const route = useRoute();
const router = useRouter();
const groupStore = useGroupStore();

const groupId = route.params.id as string;

const currentGroupRef = computed(() => groupStore.currentGroup);
const { canEdit, canChangeApplicationScope, canManageMembers, sourceLabelKey, sourceChipColor } =
  useGroupFieldPolicies(currentGroupRef);

const page = computed(() => ({ title: t('groups.details.title') }));
const breadcrumbs = computed(() => [
  {
    text: t('groups.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('groups.breadcrumbs.groups'),
    disabled: false,
    href: '/apps/groups',
  },
  {
    text: t('groups.breadcrumbs.details'),
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
    const localeMap: Record<string, string> = {
      tr: 'tr-TR',
      en: 'en-US',
      fr: 'fr-FR',
      ar: 'ar-SA',
      zh: 'zh-CN',
    };
    const locale = localeMap[localeStore.locale] || 'tr-TR';
    
    return new Date(date).toLocaleString(locale, {
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
        <h5 class="text-h5 font-weight-semibold">{{ t('groups.details.title') }}</h5>
        <div class="d-flex align-center flex-wrap ga-2">
          <v-chip v-if="groupStore.currentGroup" size="small" variant="tonal" :color="sourceChipColor">
            {{ t(sourceLabelKey) }}
          </v-chip>
          <v-btn v-if="canManageMembers" color="info" @click="openUserManagement" flat>
            <UserPlusIcon class="mr-2" size="18" />
            {{ t('groups.details.userManagement') }}
          </v-btn>
          <v-btn v-if="canEdit || canChangeApplicationScope" color="primary" @click="editGroup" flat>
            <EditIcon class="mr-2" size="18" />
            {{ t('groups.details.edit') }}
          </v-btn>
        </div>
      </div>

      <div v-if="loading && !groupStore.currentGroup" class="text-center py-8">
        <v-progress-circular indeterminate color="primary" />
        <p class="text-subtitle-1 mt-4">{{ t('groups.details.loading') }}</p>
      </div>

      <div v-else-if="groupStore.currentGroup" class="pa-4">
        <v-row>
          <!-- Group Info Card -->
          <v-col cols="12" md="6">
            <v-card variant="outlined" class="pa-4">
              <h6 class="text-h6 mb-4 font-weight-semibold">{{ t('groups.details.groupInfo') }}</h6>
              
              <v-list lines="two" density="compact">
                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">{{ t('groups.details.name') }}</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ groupStore.currentGroup.name }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item v-if="groupStore.currentGroup.description">
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">{{ t('groups.details.description') }}</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ groupStore.currentGroup.description }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">{{ t('groups.details.status') }}</v-label>
                  </template>
                  <v-list-item-title>
                    <v-chip :color="groupStore.currentGroup.isActive ? 'success' : 'error'" size="small" variant="flat">
                      {{ groupStore.currentGroup.isActive ? t('groups.details.statusActive') : t('groups.details.statusInactive') }}
                    </v-chip>
                  </v-list-item-title>
                </v-list-item>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">{{ t('groups.details.memberCount') }}</v-label>
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
              <h6 class="text-h6 mb-4 font-weight-semibold">{{ t('groups.details.metadata') }}</h6>
              
              <v-list lines="two" density="compact">
                <v-list-item>
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">{{ t('groups.details.createdAt') }}</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ formatDate(groupStore.currentGroup.createdAt) }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item v-if="groupStore.currentGroup.updatedAt">
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">{{ t('groups.details.updatedAt') }}</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ formatDate(groupStore.currentGroup.updatedAt) }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item v-if="groupStore.currentGroup.createdBy">
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">{{ t('groups.details.createdBy') }}</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ groupStore.currentGroup.createdBy }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item v-if="groupStore.currentGroup.updatedBy">
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">{{ t('groups.details.updatedBy') }}</v-label>
                  </template>
                  <v-list-item-title class="text-subtitle-1">
                    {{ groupStore.currentGroup.updatedBy }}
                  </v-list-item-title>
                </v-list-item>

                <v-list-item v-if="groupStore.currentGroup.permissions && groupStore.currentGroup.permissions.length > 0">
                  <template v-slot:prepend>
                    <v-label class="text-subtitle-2 font-weight-medium">{{ t('groups.details.permissions') }}</v-label>
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
            {{ t('groups.details.backToList') }}
          </v-btn>
        </div>
      </div>

      <div v-else class="text-center py-8">
        <p class="text-subtitle-1 text-medium-emphasis">{{ t('groups.details.notFound') }}</p>
        <v-btn color="primary" @click="router.push('/apps/groups')" class="mt-4">
          {{ t('groups.details.backToList') }}
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

