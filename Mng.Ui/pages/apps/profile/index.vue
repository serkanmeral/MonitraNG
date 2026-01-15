<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { UserCircleIcon, SettingsIcon, LockIcon, BellIcon } from 'vue-tabler-icons';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import ProfileHeader from '@/components/apps/profile/ProfileHeader.vue';
import ProfileGeneralTab from '@/components/apps/profile/ProfileGeneralTab.vue';

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

// Current tab
const currentTab = ref('general');

// Page title and breadcrumbs
const page = computed(() => ({ title: t('profile.title') }));
const breadcrumbs = computed(() => [
  {
    text: t('profile.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('profile.title'),
    disabled: true,
    href: '#',
  },
]);

// Load current user data
// Note: We need to fetch the user from backend to get the real userId
// authStore.userInfo.sub is Keycloak user ID, not backend user ID
onMounted(async () => {
  // Try to find current user in the users list first
  // We need the real backend userId for update operations
  if (authStore.userInfo?.username || authStore.userInfo?.email) {
    try {
      // Try to fetch user by username or email
      // fetchUsers returns void, but updates userStore.users
      const searchTerm = authStore.userInfo.username || authStore.userInfo.email || '';
      await userStore.fetchUsers({ search: searchTerm, pageSize: 10 });
      
      // Find current user in the fetched users list
      const foundUser = userStore.users.find(u => 
        u.username === authStore.userInfo?.username || 
        u.email === authStore.userInfo?.email ||
        u.username === authStore.userInfo?.preferred_username
      );
      
      if (foundUser) {
        // User found in backend, use it - this has the real userId
        userStore.currentUser = foundUser;
      } else {
        // If not found, try to fetch by Keycloak user ID (might work if it matches)
        // But this usually fails, so we'll create a fallback user from authStore
        console.warn('User not found in backend by username/email, using authStore data');
      }
    } catch (error) {
      console.warn('Could not fetch user from backend, using authStore data:', error);
      // Continue with authStore.userInfo - this is fine for display, but update will fail
    }
  }
});
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-row>
    <v-col cols="12">
      <ProfileHeader />
      
      <v-card elevation="10" class="mt-4">
        <v-tabs v-model="currentTab" color="primary">
          <v-tab value="general">
            <UserCircleIcon size="18" class="mr-2" />
            {{ t('profile.tabs.general') }}
          </v-tab>
          <v-tab value="account" disabled>
            <SettingsIcon size="18" class="mr-2" />
            {{ t('profile.tabs.account') }}
          </v-tab>
          <v-tab value="security" disabled>
            <LockIcon size="18" class="mr-2" />
            {{ t('profile.tabs.security') }}
          </v-tab>
          <v-tab value="preferences" disabled>
            <BellIcon size="18" class="mr-2" />
            {{ t('profile.tabs.preferences') }}
          </v-tab>
        </v-tabs>
        
        <v-card-text>
          <v-window v-model="currentTab">
            <v-window-item value="general">
              <ProfileGeneralTab />
            </v-window-item>
            <v-window-item value="account">
              <div class="text-center py-8">
                <p class="text-subtitle-1">{{ t('profile.comingSoon') }}</p>
              </div>
            </v-window-item>
            <v-window-item value="security">
              <div class="text-center py-8">
                <p class="text-subtitle-1">{{ t('profile.comingSoon') }}</p>
              </div>
            </v-window-item>
            <v-window-item value="preferences">
              <div class="text-center py-8">
                <p class="text-subtitle-1">{{ t('profile.comingSoon') }}</p>
              </div>
            </v-window-item>
          </v-window>
        </v-card-text>
      </v-card>
    </v-col>
  </v-row>
</template>
