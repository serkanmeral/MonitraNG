<script setup lang="ts">
import { MailIcon } from "vue-tabler-icons";
import { profileDD } from "@/_mockApis/headerData";
import { useAuthStore } from "@/stores/auth";
import { useUserStore } from "@/stores/apps/user";
import AvatarDisplay from "@/components/apps/profile/AvatarDisplay.vue";
import { computed, onMounted, ref } from "vue";
import { fetchFromMngKeeper, fetchFromDataGateway } from "@/services/apiService";

const authStore = useAuthStore();
const userStore = useUserStore();

// Get i18n instance for legacy mode
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string) => {
  if (i18n && i18n.t) {
    return i18n.t(key);
  }
  if (i18n?.global?.t) {
    return i18n.global.t(key);
  }
  return key;
};

// Get user info from store
const userInfo = computed(() => authStore.userInfo);

// Get user display name
const userDisplayName = computed(() => {
  if (!userInfo.value) return t('header.profileDD.myProfile.title') || 'Kullanıcı';
  
  // Try to get full name from firstName + lastName
  if (userInfo.value.given_name && userInfo.value.family_name) {
    return `${userInfo.value.given_name} ${userInfo.value.family_name}`;
  }
  
  // Try to get name from token (if available)
  const name = userInfo.value.name || userInfo.value.given_name || userInfo.value.preferred_username;
  if (name) return name;
  
  // Fallback to username
  return userInfo.value.username || t('header.profileDD.myProfile.title') || 'Kullanıcı';
});

// Get translated profile menu items
const translatedProfileDD = computed(() => {
  return profileDD.map(item => {
    let title = item.title;
    let subtitle = item.subtitle;
    
    // Map items to i18n keys
    if (item.href === '/apps/profile') {
      title = t('header.profileDD.myProfile.title') || item.title;
      subtitle = t('header.profileDD.myProfile.subtitle') || item.subtitle;
    } else if (item.href === '/apps/notes') {
      title = t('header.profileDD.myNotes.title') || item.title;
      subtitle = t('header.profileDD.myNotes.subtitle') || item.subtitle;
    }
    
    return {
      ...item,
      title,
      subtitle
    };
  });
});

// Get user initials for avatar
const userInitials = computed(() => {
  if (!userInfo.value) return 'U';
  
  // If we have firstName and lastName, use their first letters
  if (userInfo.value.given_name && userInfo.value.family_name) {
    const first = userInfo.value.given_name[0]?.toUpperCase() || '';
    const last = userInfo.value.family_name[0]?.toUpperCase() || '';
    return (first + last) || 'U';
  }
  
  // Try to get name from token
  const name = userInfo.value.name || userInfo.value.given_name || userInfo.value.preferred_username || userInfo.value.username || '';
  
  // If name contains space, get first letters of first and last word
  if (name.includes(' ')) {
    const parts = name.trim().split(' ').filter(p => p.length > 0);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return parts[0][0].toUpperCase();
  }
  
  // If single word, get first 2 letters
  if (name.length >= 2) {
    return name.substring(0, 2).toUpperCase();
  }
  
  return name[0]?.toUpperCase() || 'U';
});

// Get user email
const userEmail = computed(() => {
  return userInfo.value?.email || '';
});

// Get current user for AvatarDisplay component
const currentUser = computed(() => {
  const info = authStore.userInfo;
  if (!info) return null;
  
  const currentKeycloakUserId = info.sub || info.username;
  
  // Only use userStore.currentUser if it matches the current authenticated user
  // Compare using keycloakUserId (if available) or username/email
  if (userStore.currentUser) {
    const storedKeycloakUserId = userStore.currentUser.keycloakUserId;
    const storedUsername = userStore.currentUser.username;
    const storedEmail = userStore.currentUser.email;
    const authUsername = info.username || info.preferred_username;
    const authEmail = info.email;
    
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
    domainId: info.domain_id || '',
    username: info.username || info.preferred_username || info.sub || '',
    email: info.email || '',
    firstName: info.given_name || info.name?.split(' ')[0] || '',
    lastName: info.family_name || info.name?.split(' ').slice(1).join(' ') || '',
    title: null,
    department: null,
    gender: 'NotSpecified' as const,
    phoneNumber: null,
    photoUrl: null, // Will be loaded from backend if available
    isActive: true,
    groups: authStore.userGroups || [],
    roles: [],
  };
});

// Load current user data (for photoUrl) on mount
onMounted(async () => {
  if (authStore.isAuthenticated && !userStore.currentUser && authStore.userInfo) {
    try {
      const searchTerm = authStore.userInfo.username || authStore.userInfo.email || authStore.userInfo.preferred_username || '';
      if (searchTerm) {
        await userStore.fetchUsers({ search: searchTerm, pageSize: 10 });
        const foundUser = userStore.users.find(u => 
          u.username === authStore.userInfo?.username || 
          u.email === authStore.userInfo?.email ||
          u.username === authStore.userInfo?.preferred_username
        );
        if (foundUser) {
          userStore.currentUser = foundUser;
        }
      }
    } catch (error) {
      // User fetch error is not critical, continue without photo
      // Silently handle error - will use authStore.userInfo as fallback
    }
  }
});

// Logout handler
const logOut = async function(){
  await authStore.logout();
  return navigateTo('/auth/login');
}

// Get app version from runtime config
const appVersion = computed(() => {
  const config = useRuntimeConfig();
  return config.public.appVersion || '6.0.0';
});

// Version modal state
const showVersionDialog = ref(false);
const loadingVersions = ref(false);
const serviceVersions = ref<Record<string, any>>({});

// Interface for service version
interface ServiceVersion {
  name: string;
  version?: string;
  product?: string;
  buildDate?: string;
  error?: string;
}

// Fetch all service versions
const fetchServiceVersions = async () => {
  loadingVersions.value = true;
  serviceVersions.value = {};
  
  const config = useRuntimeConfig();
  const services: { name: string; url: string; fetchFn: (url: string) => Promise<any> }[] = [];
  
  // Add services based on available configs
  if (config.public.keeperUrl) {
    services.push({
      name: 'MngKeeper',
      url: `${config.public.keeperUrl}/api/version`,
      fetchFn: async (url: string) => {
        try {
          return await fetchFromMngKeeper('version');
        } catch (error) {
          throw error;
        }
      }
    });
  }
  
  if (config.public.reactorUrl) {
    services.push({
      name: 'MngDataGateway',
      url: `${config.public.reactorUrl}/api/v1/version`,
      fetchFn: async (url: string) => {
        try {
          return await fetchFromDataGateway('v1/version');
        } catch (error) {
          throw error;
        }
      }
    });
  }
  
  // Fetch versions from all services in parallel
  const versionPromises = services.map(async (service) => {
    try {
      const versionData = await service.fetchFn(service.url);
      serviceVersions.value[service.name] = {
        name: service.name,
        version: versionData.Version || versionData.version || 'N/A',
        product: versionData.Product || versionData.product || service.name,
        buildDate: versionData.BuildDate || versionData.buildDate,
        environment: versionData.Environment || versionData.environment,
        error: null
      };
    } catch (error: any) {
      serviceVersions.value[service.name] = {
        name: service.name,
        version: null,
        error: error.message || 'Bağlantı hatası'
      };
    }
  });
  
  await Promise.all(versionPromises);
  loadingVersions.value = false;
};

// Open version dialog
const openVersionDialog = async () => {
  showVersionDialog.value = true;
  if (Object.keys(serviceVersions.value).length === 0) {
    await fetchServiceVersions();
  }
};

// Close version dialog
const closeVersionDialog = () => {
  showVersionDialog.value = false;
};

</script>

<template>
  <!-- ---------------------------------------------- -->
  <!-- notifications DD -->
  <!-- ---------------------------------------------- -->
  <v-menu :close-on-content-click="false">
    <template v-slot:activator="{ props }">
      <v-btn variant="text" v-bind="props" icon>
        <AvatarDisplay :user="currentUser" :size="35" />
      </v-btn>
    </template>
    <v-sheet rounded="md" width="360" elevation="10">
      <div class="px-8 pt-6">
        <h6 class="text-h5 font-weight-medium">{{ t('header.profileDD.myProfile.title') || 'Kullanıcı Profili' }}</h6>
        <div class="d-flex align-center mt-4 pb-6">
          <AvatarDisplay :user="currentUser" :size="80" />
          <div class="ml-3">
            <h6 class="text-h6 mb-n1">{{ userDisplayName }}</h6>
            <span class="text-subtitle-1 font-weight-regular textSecondary" v-if="userInfo?.isAdmin"
              >{{ t('profile.accountSettings.role.admin') || 'Yönetici' }}</span
            >
            <span class="text-subtitle-1 font-weight-regular textSecondary" v-else
              >{{ t('profile.accountSettings.role.user') || 'Kullanıcı' }}</span
            >
            <div class="d-flex align-center mt-1" v-if="userEmail">
              <MailIcon size="18" stroke-width="1.5" />
              <span
                class="text-subtitle-1 font-weight-regular textSecondary ml-2"
                >{{ userEmail }}</span
              >
            </div>
            <div class="d-flex align-center mt-1" v-if="userInfo?.domain_name">
              <span class="text-caption text-medium-emphasis">Domain: {{ userInfo.domain_name }}</span>
            </div>
          </div>
        </div>
        <v-divider></v-divider>
      </div>
      <perfect-scrollbar style="height: calc(100vh - 240px); max-height: 240px">
        <v-list class="py-0 theme-list" lines="two">
          <v-list-item
            v-for="item in translatedProfileDD"
            :key="item.title"
            class="py-4 px-8 custom-text-primary"
            :to="item.href"
          >
            <template v-slot:prepend>
              <v-avatar
                size="48"
                color="lightprimary"
                rounded="md"
              >
                <img
                  :src="item.avatar"
                  width="24"
                  height="24"
                  :alt="item.avatar"
                />
              </v-avatar>
            </template>
            <div>
              <h6 class="text-subtitle-1 font-weight-semibold mb-2 custom-title">
                {{ item.title }}
              </h6>
            </div>
            <p class="text-subtitle-1 font-weight-regular textSecondary">
              {{ item.subtitle }}
            </p>
          </v-list-item>
        </v-list>
      </perfect-scrollbar>
      <div class="pt-4 pb-2 px-8 text-center">
        <v-btn color="primary" variant="outlined" block @click="logOut"
          >{{ t('header.profileDD.logout') || 'Çıkış Yap' }}</v-btn
        >
      </div>
      <div class="pb-4 px-8 text-center">
        <v-chip 
          v-if="authStore.isManager || authStore.isAdmin"
          variant="text" 
          color="primary" 
          size="x-small" 
          class="text-caption cursor-pointer"
          @click="openVersionDialog"
          style="cursor: pointer;"
        >
          MonitraNG v{{ appVersion }}
        </v-chip>
        <v-chip 
          v-else
          variant="text" 
          color="primary" 
          size="x-small" 
          class="text-caption"
        >
          MonitraNG v{{ appVersion }}
        </v-chip>
      </div>
    </v-sheet>
  </v-menu>

  <!-- Version Information Dialog -->
  <v-dialog v-model="showVersionDialog" max-width="600" @update:model-value="!showVersionDialog && closeVersionDialog()">
    <v-card>
      <v-card-title class="pa-4 bg-primary text-white">
        <span class="text-h6">Versiyon Bilgileri</span>
      </v-card-title>
      <v-card-text class="pa-6">
        <!-- Frontend Version -->
        <div class="mb-6">
          <h6 class="text-subtitle-1 font-weight-semibold mb-2">MonitraNG UI</h6>
          <v-divider class="mb-2"></v-divider>
          <div class="d-flex justify-space-between align-center">
            <span class="text-body-2">Versiyon:</span>
            <v-chip variant="tonal" color="primary" size="small">{{ appVersion }}</v-chip>
          </div>
        </div>

        <!-- Loading state -->
        <div v-if="loadingVersions" class="text-center py-4">
          <v-progress-circular indeterminate color="primary" size="32"></v-progress-circular>
          <p class="text-subtitle-2 mt-2">Servis versiyonları yükleniyor...</p>
        </div>

        <!-- Service versions -->
        <div v-else>
          <div v-for="(service, serviceName) in serviceVersions" :key="serviceName" class="mb-6">
            <h6 class="text-subtitle-1 font-weight-semibold mb-2">{{ service.name }}</h6>
            <v-divider class="mb-2"></v-divider>
            
            <!-- Error state -->
            <v-alert
              v-if="service.error"
              type="error"
              variant="tonal"
              density="compact"
              class="mb-2"
            >
              {{ service.error }}
            </v-alert>
            
            <!-- Version info -->
            <div v-else class="d-flex flex-column gap-2">
              <div class="d-flex justify-space-between align-center">
                <span class="text-body-2">Versiyon:</span>
                <v-chip variant="tonal" color="primary" size="small">{{ service.version || 'N/A' }}</v-chip>
              </div>
              <div v-if="service.product" class="d-flex justify-space-between align-center">
                <span class="text-body-2">Ürün:</span>
                <span class="text-body-2 text-medium-emphasis">{{ service.product }}</span>
              </div>
              <div v-if="service.buildDate" class="d-flex justify-space-between align-center">
                <span class="text-body-2">Build Tarihi:</span>
                <span class="text-body-2 text-medium-emphasis">{{ new Date(service.buildDate).toLocaleDateString('tr-TR') }}</span>
              </div>
              <div v-if="service.environment" class="d-flex justify-space-between align-center">
                <span class="text-body-2">Ortam:</span>
                <v-chip 
                  :color="service.environment.toLowerCase() === 'production' ? 'success' : 'warning'" 
                  variant="tonal" 
                  size="x-small"
                >
                  {{ service.environment }}
                </v-chip>
              </div>
            </div>
          </div>

          <!-- Empty state -->
          <div v-if="Object.keys(serviceVersions).length === 0" class="text-center py-4">
            <p class="text-subtitle-2 text-medium-emphasis">Servis versiyon bilgisi bulunamadı</p>
          </div>
        </div>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer></v-spacer>
        <v-btn color="primary" variant="outlined" @click="closeVersionDialog">
          Kapat
        </v-btn>
        <v-btn 
          color="primary" 
          variant="flat" 
          @click="fetchServiceVersions"
          :loading="loadingVersions"
        >
          Yenile
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
