<script setup lang="ts">
import { computed } from 'vue';
import { MailIcon, UserCircleIcon, SettingsIcon, LockIcon, BellIcon } from 'vue-tabler-icons';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';
import AvatarDisplay from '@/components/apps/profile/AvatarDisplay.vue';

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

const authStore = useAuthStore();
const userStore = useUserStore();

// Get current user
const currentUser = computed(() => {
  const userInfo = authStore.userInfo;
  if (!userInfo) return null;
  
  const currentKeycloakUserId = userInfo.sub || userInfo.username;
  
  // Only use userStore.currentUser if it matches the current authenticated user
  // Compare using keycloakUserId (if available) or username/email
  if (userStore.currentUser) {
    const storedKeycloakUserId = userStore.currentUser.keycloakUserId;
    const storedUsername = userStore.currentUser.username;
    const storedEmail = userStore.currentUser.email;
    const authUsername = userInfo.username || userInfo.preferred_username;
    const authEmail = userInfo.email;
    
    // Match if keycloakUserId matches, or username/email matches
    const isMatch = 
      (storedKeycloakUserId && storedKeycloakUserId === currentKeycloakUserId) ||
      (storedUsername && storedUsername === authUsername) ||
      (storedEmail && storedEmail === authEmail);
    
    if (isMatch) {
      return userStore.currentUser;
    } else {
      // Mismatch - clear the stored user (it's from a different user)
      userStore.currentUser = null;
    }
  }
  
  // Fallback to authStore.userInfo
  return {
    id: currentKeycloakUserId || '',
    userId: currentKeycloakUserId,
    keycloakUserId: currentKeycloakUserId,
    domainId: userInfo.domain_id || '',
    username: userInfo.username || userInfo.preferred_username || userInfo.sub || '',
    email: userInfo.email || '',
    firstName: userInfo.given_name || userInfo.name?.split(' ')[0] || '',
    lastName: userInfo.family_name || userInfo.name?.split(' ').slice(1).join(' ') || '',
    title: null,
    department: null,
    gender: 'NotSpecified' as const,
    phoneNumber: null,
    photoUrl: null,
    isActive: true,
    groups: authStore.userGroups || [],
    roles: [],
  };
});

// User display name
const userDisplayName = computed(() => {
  const user = currentUser.value;
  if (!user) return t('profile.user');
  
  if (user.firstName && user.lastName) {
    return `${user.firstName} ${user.lastName}`;
  }
  
  return user.username || t('profile.user');
});

// User title and department
const userTitle = computed(() => {
  return currentUser.value?.title || '';
});

const userDepartment = computed(() => {
  return currentUser.value?.department || '';
});

// User email
const userEmail = computed(() => {
  return currentUser.value?.email || '';
});

// User role badge
const userRole = computed(() => {
  if (authStore.isAdmin) {
    return t('profile.roles.admin');
  }
  if (authStore.isManager) {
    return t('profile.roles.manager');
  }
  return t('profile.roles.user');
});
</script>

<template>
  <v-card elevation="10" class="overflow-hidden">
    <v-card-item class="pa-0">
      <!-- Banner Background -->
      <div class="profile-banner" style="height: 200px; background: linear-gradient(135deg, rgb(80, 178, 252) 0%, rgb(244, 76, 102) 100%);"></div>
      
      <!-- Profile Info Section -->
      <div class="profile-info-section">
        <v-row class="mt-n12">
          <v-col cols="12" class="d-flex justify-center">
            <div class="text-center">
              <!-- Avatar with border -->
              <div class="avatar-border mb-4">
                <AvatarDisplay 
                  :user="currentUser"
                  :size="120"
                />
              </div>
              
              <!-- User Name -->
              <h4 class="text-h4 mb-1">{{ userDisplayName }}</h4>
              
              <!-- Title and Department -->
              <div v-if="userTitle || userDepartment" class="mb-2">
                <span v-if="userTitle" class="text-h6 font-weight-regular">{{ userTitle }}</span>
                <span v-if="userTitle && userDepartment" class="text-h6 font-weight-regular"> • </span>
                <span v-if="userDepartment" class="text-h6 font-weight-regular">{{ userDepartment }}</span>
              </div>
              
              <!-- Email -->
              <div v-if="userEmail" class="d-flex align-center justify-center mb-2">
                <MailIcon size="18" stroke-width="1.5" class="mr-2" />
                <span class="text-subtitle-1">{{ userEmail }}</span>
              </div>
              
              <!-- Role Badge -->
              <v-chip 
                :color="authStore.isAdmin ? 'error' : authStore.isManager ? 'warning' : 'primary'"
                size="small"
                variant="flat"
              >
                {{ userRole }}
              </v-chip>
            </div>
          </v-col>
        </v-row>
      </div>
    </v-card-item>
  </v-card>
</template>

<style lang="scss" scoped>
.profile-banner {
  width: 100%;
  position: relative;
}

.profile-info-section {
  padding: 0 24px 24px;
}

.avatar-border {
  background-image: linear-gradient(rgb(80, 178, 252), rgb(244, 76, 102));
  border-radius: 50%;
  width: 130px;
  height: 130px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto;
  
  :deep(.v-avatar) {
    border: 4px solid rgb(255, 255, 255);
  }
}
</style>
