<script setup lang="ts">
import { ref, onMounted, watch, nextTick, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useLocaleStore } from '@/stores/locale';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AvatarDisplay from '@/components/apps/profile/AvatarDisplay.vue';
import { useUserStore } from '@/stores/apps/user';
import { EditIcon } from 'vue-tabler-icons';

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

const route = useRoute();
const router = useRouter();
const userStore = useUserStore();
const localeStore = useLocaleStore();

const userId = route.params.id as string;

const page = computed(() => ({ 
  title: t('users.details.title') 
}));
const breadcrumbs = computed(() => [
  {
    text: t('users.details.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('users.details.breadcrumbs.users'),
    disabled: false,
    href: '/apps/users',
  },
  {
    text: t('users.details.breadcrumbs.details'),
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

const editUser = () => {
  router.push(`/apps/users/edit/${userId}`);
};

const resettingPassword = ref(false);
const resetPasswordSuccess = ref(false);
const resetPasswordError = ref<string | null>(null);

const handleResetPassword = async () => {
  if (!userStore.viewingUser) return;
  
  resettingPassword.value = true;
  resetPasswordSuccess.value = false;
  resetPasswordError.value = null;
  
  try {
    const userId = userStore.viewingUser.id || userStore.viewingUser.userId;
    if (!userId) {
      resetPasswordError.value = t('users.details.resetPassword.userIdNotFound');
      return;
    }
    
    const result = await userStore.requestPasswordReset(userId);
    
    if (result.isSuccess) {
      resetPasswordSuccess.value = true;
      // Hide success message after 5 seconds
      setTimeout(() => {
        resetPasswordSuccess.value = false;
      }, 5000);
    } else {
      resetPasswordError.value = result.error || t('users.details.resetPassword.error');
    }
  } catch (error: any) {
    resetPasswordError.value = error.message || t('users.details.resetPassword.genericError');
  } finally {
    resettingPassword.value = false;
  }
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <!-- Success/Error Messages -->
  <v-alert
    v-if="resetPasswordSuccess"
    type="success"
    variant="tonal"
    closable
    class="mb-4"
    @click:close="resetPasswordSuccess = false"
  >
    {{ t('users.details.resetPassword.success') }}
  </v-alert>
  
  <v-alert
    v-if="resetPasswordError"
    type="error"
    variant="tonal"
    closable
    class="mb-4"
    @click:close="resetPasswordError = null"
  >
    {{ resetPasswordError }}
  </v-alert>
  
  <v-card elevation="10" v-if="!loading || userStore.viewingUser">
    <v-card-item>
      <div class="d-flex justify-space-between align-center mb-6">
        <div>
          <h5 class="text-h5 font-weight-semibold mb-1">{{ t('users.details.title') }}</h5>
          <div class="text-caption text-medium-emphasis" v-if="userStore.viewingUser">
            <v-icon size="14" class="mr-1">mdi-identifier</v-icon>
            ID: {{ userStore.viewingUser.id || userStore.viewingUser.userId }}
          </div>
        </div>
        <div class="d-flex ga-2">
          <v-btn 
            color="warning" 
            @click="handleResetPassword" 
            flat
            :loading="resettingPassword"
            :disabled="resettingPassword || !userStore.viewingUser?.email"
          >
            <v-icon class="mr-2" size="18">mdi-lock-reset</v-icon>
            {{ t('users.details.buttons.resetPassword') }}
          </v-btn>
          <v-btn color="primary" @click="editUser" flat>
            <EditIcon class="mr-2" size="18" />
            {{ t('users.details.buttons.edit') }}
          </v-btn>
        </div>
      </div>

      <div v-if="loading && !userStore.viewingUser" class="text-center py-8">
        <v-progress-circular indeterminate color="primary" />
        <p class="text-subtitle-1 mt-4">{{ t('users.details.loading') }}</p>
      </div>

      <div v-else-if="userStore.viewingUser" class="pa-4">
        <v-row>
          <!-- User Info Card -->
          <v-col cols="12" md="6">
            <v-card variant="outlined" class="pa-4">
              <div class="d-flex align-center mb-4">
                <v-icon class="mr-2" color="primary">mdi-account-circle</v-icon>
                <h6 class="text-h6 font-weight-semibold">{{ t('users.details.cards.userInfo') }}</h6>
              </div>
              
              <v-list lines="two" density="comfortable">
                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-identifier</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.userId') }}</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1 font-mono">
                    {{ userStore.viewingUser.id || userStore.viewingUser.userId }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2"></v-divider>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-account</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.username') }}</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.username }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2"></v-divider>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-email</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.email') }}</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.email }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2"></v-divider>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-account-box</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.fullName') }}</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.firstName }} {{ userStore.viewingUser.lastName }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-list-item v-if="userStore.viewingUser.title">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-briefcase</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.title') }}</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.title }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-list-item v-if="userStore.viewingUser.department">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-office-building</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.department') }}</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.department }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-list-item v-if="userStore.viewingUser.phoneNumber">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-phone</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.phone') }}</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.phoneNumber }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2"></v-divider>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-gender-male-female</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.gender') }}</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ 
                      userStore.viewingUser.gender === 1 ? t('users.details.gender.male') : 
                      userStore.viewingUser.gender === 2 ? t('users.details.gender.female') : 
                      t('users.details.gender.notSpecified') 
                    }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2"></v-divider>

                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" :color="userStore.viewingUser.isActive ? 'success' : 'error'">mdi-check-circle</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.status') }}</v-list-item-title>
                  <v-list-item-subtitle>
                    <v-chip :color="userStore.viewingUser.isActive ? 'success' : 'error'" size="small" variant="flat">
                      {{ userStore.viewingUser.isActive ? t('users.status.active') : t('users.status.inactive') }}
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
                <h6 class="text-h6 font-weight-semibold">{{ t('users.details.cards.groupsAndRoles') }}</h6>
              </div>
              
              <v-list lines="two" density="comfortable">
                <v-list-item>
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-account-multiple</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-2">{{ t('users.details.fields.groups') }}</v-list-item-title>
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
                        {{ t('users.groups.none') }}
                      </span>
                    </div>
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2" v-if="userStore.viewingUser.roles && userStore.viewingUser.roles.length > 0"></v-divider>

                <v-list-item v-if="userStore.viewingUser.roles && userStore.viewingUser.roles.length > 0">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-shield-account</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-2">{{ t('users.details.fields.roles') }}</v-list-item-title>
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
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.createdAt') }}</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ formatDate(userStore.viewingUser.createdAt) }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-list-item v-if="userStore.viewingUser.createdBy">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-account-plus</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.createdBy') }}</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ userStore.viewingUser.createdBy }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2" v-if="userStore.viewingUser.lastLoginAt"></v-divider>

                <v-list-item v-if="userStore.viewingUser.lastLoginAt">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-login</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.lastLogin') }}</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ formatDate(userStore.viewingUser.lastLoginAt) }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-divider class="my-2" v-if="userStore.viewingUser.updatedAt"></v-divider>

                <v-list-item v-if="userStore.viewingUser.updatedAt">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-calendar-edit</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.updatedAt') }}</v-list-item-title>
                  <v-list-item-subtitle class="text-body-1">
                    {{ formatDate(userStore.viewingUser.updatedAt) }}
                  </v-list-item-subtitle>
                </v-list-item>

                <v-list-item v-if="userStore.viewingUser.updatedBy">
                  <template v-slot:prepend>
                    <v-icon size="20" class="mr-2" color="primary">mdi-account-edit</v-icon>
                  </template>
                  <v-list-item-title class="text-subtitle-2 font-weight-medium mb-1">{{ t('users.details.fields.updatedBy') }}</v-list-item-title>
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
                  <h6 class="text-h6 font-weight-semibold">{{ t('users.details.cards.photo') }}</h6>
                </div>
                <div class="d-flex justify-center mb-2">
                  <AvatarDisplay
                    v-if="userStore.viewingUser"
                    :user="userStore.viewingUser"
                    :size="200"
                  />
                  <v-avatar v-else size="200" class="mb-2">
                    <v-icon size="100" color="grey-lighten-1">
                      mdi-account-circle
                    </v-icon>
                  </v-avatar>
                </div>
                <div v-if="!userStore.viewingUser?.photoUrl" class="text-caption text-medium-emphasis mt-2">
                  {{ t('users.details.photo.none') }}
                </div>
              </div>
            </v-card>
          </v-col>
        </v-row>

        <!-- Actions -->
        <div class="d-flex justify-end ga-3 mt-6">
          <v-btn color="primary" variant="flat" @click="router.push('/apps/users')">
            {{ t('users.details.buttons.backToList') }}
          </v-btn>
        </div>
      </div>

      <div v-else class="text-center py-8">
        <p class="text-subtitle-1 text-medium-emphasis">{{ t('users.details.notFound') }}</p>
        <v-btn color="primary" @click="router.push('/apps/users')" class="mt-4">
          {{ t('users.details.buttons.backToList') }}
        </v-btn>
      </div>
    </v-card-item>
  </v-card>
</template>

