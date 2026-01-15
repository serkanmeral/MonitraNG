<script setup lang="ts">
import { computed } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';

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
    username: userInfo.username || userInfo.preferred_username || userInfo.sub || '',
    email: userInfo.email || '',
    createdAt: userInfo.created_at || null,
  };
});

// Format date
const formatDate = (date: string | Date | null | undefined): string => {
  if (!date) return '-';
  try {
    const d = typeof date === 'string' ? new Date(date) : date;
    return new Intl.DateTimeFormat('tr-TR', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    }).format(d);
  } catch {
    return '-';
  }
};

// Snackbar helper
const showSnackbar = (message: string, type: 'success' | 'error' = 'success') => {
  if (type === 'error') {
    alert(message);
  }
};
</script>

<template>
  <v-row>
    <!-- Account Information Card -->
    <v-col cols="12" lg="8" md="12">
      <v-card elevation="10" class="mb-4">
        <v-card-item>
          <h5 class="text-h5 mb-4">{{ t('profile.accountSettings.accountInfo.title') }}</h5>
          <v-row>
            <v-col cols="12" md="6">
              <div class="mb-4">
                <span class="text-subtitle-2 text-medium-emphasis">
                  {{ t('profile.accountSettings.accountInfo.username') }}
                </span>
                <p class="text-body-1 mt-1">{{ currentUser?.username || '-' }}</p>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="mb-4">
                <span class="text-subtitle-2 text-medium-emphasis">
                  {{ t('profile.accountSettings.accountInfo.email') }}
                </span>
                <p class="text-body-1 mt-1">{{ currentUser?.email || '-' }}</p>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="mb-4">
                <span class="text-subtitle-2 text-medium-emphasis">
                  {{ t('profile.accountSettings.accountInfo.domain') }}
                </span>
                <p class="text-body-1 mt-1">{{ authStore.domainName || '-' }}</p>
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="mb-4">
                <span class="text-subtitle-2 text-medium-emphasis">
                  {{ t('profile.accountSettings.accountInfo.createdAt') }}
                </span>
                <p class="text-body-1 mt-1">
                  {{ formatDate(currentUser?.createdAt) }}
                </p>
              </div>
            </v-col>
            <v-col cols="12" md="6" v-if="userStore.currentUser?.updatedAt">
              <div class="mb-4">
                <span class="text-subtitle-2 text-medium-emphasis">
                  {{ t('profile.accountSettings.accountInfo.updatedAt') }}
                </span>
                <p class="text-body-1 mt-1">
                  {{ formatDate(userStore.currentUser.updatedAt) }}
                </p>
              </div>
            </v-col>
          </v-row>
        </v-card-item>
      </v-card>
    </v-col>

    <!-- Danger Zone Card -->
    <v-col cols="12" lg="4" md="12">
      <v-card elevation="10" class="border-error">
        <v-card-item>
          <h5 class="text-h5 mb-4 text-error">
            {{ t('profile.accountSettings.dangerZone.title') }}
          </h5>
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ t('profile.accountSettings.dangerZone.description') }}
          </p>
          <v-btn
            color="error"
            variant="outlined"
            block
            disabled
            @click="showSnackbar(t('profile.accountSettings.dangerZone.notImplemented'), 'error')"
          >
            {{ t('profile.accountSettings.dangerZone.deleteAccount') }}
          </v-btn>
        </v-card-item>
      </v-card>
    </v-col>
  </v-row>
</template>
