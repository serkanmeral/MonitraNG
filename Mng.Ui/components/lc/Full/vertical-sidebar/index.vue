<script setup lang="ts">
import { ref, shallowRef, computed, onMounted, watch } from 'vue';
import { useCustomizerStore } from '@/stores/customizer';
import { useAuthStore } from '@/stores/auth';
import { useUserStore } from '@/stores/apps/user';
import { useSideMenuStore } from '@/stores/apps/sideMenu';
import { useLocaleStore } from '@/stores/locale';
import { PowerIcon } from 'vue-tabler-icons';
import AvatarDisplay from '@/components/apps/profile/AvatarDisplay.vue';
import type { SideMenuItem } from '@/stores/apps/sideMenu';
import type { menu } from './sidebarItem';
import { resolveDomainLogoSrc } from '@/composables/useDomain';

const customizer = useCustomizerStore();
const authStore = useAuthStore();
const userStore = useUserStore();
const menuStore = useSideMenuStore();
const localeStore = useLocaleStore();
const config = useRuntimeConfig();
const nuxtApp = useNuxtApp();

// Check if fallback menu is enabled (default: false - disabled)
const enableFallbackMenu = computed(() => config.public.enableFallbackMenu === true);

// Try to load menu items from API, fallback to hard-coded (if enabled)
const sidebarMenu = shallowRef<menu[]>([]);
const menuLoading = ref(true); // Track loading state

// Load menu from store (API) or fallback to hard-coded
const loadMenu = async (forceRefresh: boolean = false) => {
  menuLoading.value = true;
  
  try {
    // Try to load from API
    await menuStore.loadMenuItems(forceRefresh);
    
    if (menuStore.visibleMenuItems && menuStore.visibleMenuItems.length > 0) {
      // Convert store items to menu format
      sidebarMenu.value = menuStore.visibleMenuItems.map(item => menuStore.convertToMenuFormat(item));
      menuLoading.value = false;
      return;
    } else {
      if (!enableFallbackMenu.value) {
        sidebarMenu.value = [];
        menuLoading.value = false;
        return;
      }
    }
  } catch (error) {
    if (!enableFallbackMenu.value) {
      sidebarMenu.value = [];
      menuLoading.value = false;
      return;
    }
  }
  
  // Fallback: Hard-coded menu (only if enabled)
  if (!enableFallbackMenu.value) {
    sidebarMenu.value = [];
    menuLoading.value = false;
    return;
  }
  
  try {
    const sidebarItemsModule = await import('./sidebarItem');
    sidebarMenu.value = sidebarItemsModule.default || [];
  } catch (error) {
    sidebarMenu.value = [];
  } finally {
    menuLoading.value = false;
  }
};

// Watch for auth changes to reload menu
watch(() => authStore.isAuthenticated, async (isAuth) => {
  if (isAuth) {
    await loadMenu();
    
    // SignalR bağlantısını başlat
    try {
      await menuStore.connectToHub();
    } catch (error) {
      // SignalR bağlantı hatası kritik değil, sessizce devam et
    }
  } else {
    // Logout durumunda SignalR bağlantısını kapat
    try {
      await menuStore.disconnectFromHub();
    } catch (error) {
      // Hata önemli değil
    }
  }
});

// Watch for menu store changes
watch(() => menuStore.visibleMenuItems, () => {
  if (menuStore.visibleMenuItems && menuStore.visibleMenuItems.length > 0) {
    sidebarMenu.value = menuStore.visibleMenuItems.map(item => menuStore.convertToMenuFormat(item));
    menuLoading.value = false;
  } else if (menuStore.loading === false) {
    // Menu yükleme tamamlandı ama boş
    menuLoading.value = false;
  }
}, { deep: true });

// Watch locale changes and update i18n locale (for Arabic support)
watch(() => localeStore.locale, (newLocale) => {
  // Get i18n instance
  const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
  
  if (i18n) {
    // IMPORTANT: In messages.ts, Arabic is stored as 'ro', not 'ar'
    // So we need to map 'ar' to 'ro' for i18n
    const i18nLocale = newLocale === 'ar' ? 'ro' : newLocale;
    i18n.locale = i18nLocale;
    
    // Also try to update global.locale if it exists (for composition API mode)
    if (i18n.global && i18n.global.locale) {
      if (typeof i18n.global.locale === 'object' && 'value' in i18n.global.locale) {
        i18n.global.locale.value = i18nLocale;
      } else {
        i18n.global.locale = i18nLocale;
      }
    }
  }
}, { immediate: true });

// Load menu on mount (force refresh to bypass cache on initial load)
onMounted(async () => {
  if (authStore.isAuthenticated) {
    // İlk yüklemede cache'i bypass et, sonraki yüklemelerde cache kullan
    await loadMenu(true);
    
    // SignalR bağlantısını başlat (real-time menu updates için)
    try {
      await menuStore.connectToHub();
    } catch (error) {
      // SignalR bağlantı hatası kritik değil, menu yine çalışır
    }
    
    // Load current user data (for photoUrl) if not already loaded
    if (!userStore.currentUser && authStore.userInfo) {
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
  }
  
  // Ensure i18n locale is set correctly on mount (for Arabic support)
  const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
  if (i18n) {
    const i18nLocale = localeStore.locale === 'ar' ? 'ro' : localeStore.locale;
    i18n.locale = i18nLocale;
    
    if (i18n.global && i18n.global.locale) {
      if (typeof i18n.global.locale === 'object' && 'value' in i18n.global.locale) {
        i18n.global.locale.value = i18nLocale;
      } else {
        i18n.global.locale = i18nLocale;
      }
    }
  }
});

// Get user display name
const userDisplayName = computed(() => {
  if (!authStore.userInfo) return 'Kullanıcı';
  
  // Try to get full name from firstName + lastName
  if (authStore.userInfo.given_name && authStore.userInfo.family_name) {
    return `${authStore.userInfo.given_name} ${authStore.userInfo.family_name}`;
  }
  
  // Try to get name from token (if available)
  const name = authStore.userInfo.name || authStore.userInfo.given_name || authStore.userInfo.preferred_username;
  if (name) return name;
  
  // Fallback to username
  return authStore.userInfo.username || 'Kullanıcı';
});

// Get user initials for avatar
const userInitials = computed(() => {
  if (!authStore.userInfo) return 'U';
  
  // If we have firstName and lastName, use their first letters
  if (authStore.userInfo.given_name && authStore.userInfo.family_name) {
    const first = authStore.userInfo.given_name[0]?.toUpperCase() || '';
    const last = authStore.userInfo.family_name[0]?.toUpperCase() || '';
    return (first + last) || 'U';
  }
  
  // Try to get name from token
  const name = authStore.userInfo.name || authStore.userInfo.given_name || authStore.userInfo.preferred_username || authStore.userInfo.username || '';
  
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

// Get domain logo for background — uploaded `logo` wins over stale `logoUrl`
const domainLogoStyle = computed(() => {
  const logoSrc = resolveDomainLogoSrc(authStore.domainInfo);
  if (logoSrc) {
    return {
      backgroundImage: `url("${logoSrc}")`,
      backgroundSize: 'contain',
      backgroundPosition: 'center',
      backgroundRepeat: 'no-repeat',
    };
  }

  return {
    backgroundImage: 'url("/images/backgrounds/user-info.jpg")',
    backgroundSize: 'cover',
    backgroundPosition: 'center',
    backgroundRepeat: 'no-repeat',
  };
});

// Logout handler
const handleLogout = async () => {
  // SignalR bağlantısını kapat
  try {
    await menuStore.disconnectFromHub();
  } catch (error) {
    // Hata önemli değil
  }
  
  await authStore.logout();
  navigateTo('/auth/login');
};
</script>

<template>
    <v-navigation-drawer
        left
        v-model="customizer.Sidebar_drawer"
        elevation="0"
        rail-width="75"
        app
        class="leftSidebar"
        :rail="customizer.mini_sidebar"
        expand-on-hover width="270"
    >
        <!-- ---------------------------------------------- -->
        <!---Navigation -->
        <!-- ---------------------------------------------- -->
        <perfect-scrollbar class="scrollnavbar">
            <div class="profile" :style="domainLogoStyle">
                <div class="profile-pic profile-pic py-7 px-3">
                    <AvatarDisplay :user="currentUser" :size="45" />
                </div>
                <div class="profile-name d-flex align-center px-3">
                    <h5 class="text-white font-weight-medium">{{ userDisplayName }}</h5>
                    <div class="ml-auto profile-logout">
                        <v-btn 
                            variant="text" 
                            icon 
                            rounded="md" 
                            color="white" 
                            @click="handleLogout"
                        >
                            <PowerIcon size="22"/>
                            <v-tooltip activator="parent" location="top">Çıkış Yap</v-tooltip>
                        </v-btn>
                    </div>
                </div>
            </div>
            <v-list class="py-5 px-4 bg-muted" density="compact" v-if="sidebarMenu && sidebarMenu.length > 0">
                <!---Menu Loop - Recursive rendering -->
                <template v-for="(item, i) in sidebarMenu" :key="`menu-${i}-${item.title || item.header || i}`">
                    <!---Item Sub Header -->
                    <template v-if="item.header">
                        <LcFullVerticalSidebarNavGroup :item="item" />
                        <!-- Header'ın children'larını recursive olarak render et -->
                        <template v-if="item.children && item.children.length > 0">
                            <template v-for="(child, j) in item.children" :key="`child-${i}-${j}-${child.title || child.header || j}`">
                                <!-- Nested Header: Eğer child bir header ise -->
                                <template v-if="child.header">
                                    <LcFullVerticalSidebarNavGroup :item="child" />
                                    <!-- Nested header'ın children'larını recursive olarak render et -->
                                    <template v-if="child.children && child.children.length > 0">
                                        <template v-for="(grandchild, k) in child.children" :key="`grandchild-${i}-${j}-${k}-${grandchild.title || grandchild.header || k}`">
                                            <!-- Deep nested: Eğer grandchild da bir header ise -->
                                            <template v-if="grandchild.header">
                                                <LcFullVerticalSidebarNavGroup :item="grandchild" />
                                                <template v-if="grandchild.children && grandchild.children.length > 0">
                                                    <template v-for="(greatGrandchild, l) in grandchild.children" :key="`greatGrandchild-${i}-${j}-${k}-${l}-${greatGrandchild.title || greatGrandchild.header || l}`">
                                                        <LcFullVerticalSidebarNavCollapse 
                                                            v-if="greatGrandchild.children && greatGrandchild.children.length > 0"
                                                            :item="greatGrandchild" 
                                                            :level="0" 
                                                            class="leftPadding" 
                                                        />
                                                        <LcFullVerticalSidebarNavItem 
                                                            v-else
                                                            :item="greatGrandchild" 
                                                            :level="0" 
                                                            class="leftPadding" 
                                                        />
                                                    </template>
                                                </template>
                                            </template>
                                            <!-- Normal Item veya Collapse: Eğer grandchild header değilse -->
                                            <template v-else>
                                                <LcFullVerticalSidebarNavCollapse 
                                                    v-if="grandchild.children && grandchild.children.length > 0"
                                                    :item="grandchild" 
                                                    :level="0" 
                                                    class="leftPadding" 
                                                />
                                                <LcFullVerticalSidebarNavItem 
                                                    v-else
                                                    :item="grandchild" 
                                                    :level="0" 
                                                    class="leftPadding" 
                                                />
                                            </template>
                                        </template>
                                    </template>
                                </template>
                                <!-- Normal Item veya Collapse: Eğer child header değilse -->
                                <template v-else>
                                    <LcFullVerticalSidebarNavCollapse 
                                        v-if="child.children && child.children.length > 0"
                                        :item="child" 
                                        :level="0" 
                                        class="leftPadding" 
                                    />
                                    <LcFullVerticalSidebarNavItem 
                                        v-else
                                        :item="child" 
                                        :level="0" 
                                        class="leftPadding" 
                                    />
                                </template>
                            </template>
                        </template>
                    </template>
                    <!---If Has Child (no header) -->
                    <LcFullVerticalSidebarNavCollapse class="leftPadding" :item="item" :level="0" v-else-if="item.children && item.children.length > 0" />
                    <!---Single Item-->
                    <LcFullVerticalSidebarNavItem :item="item" v-else class="leftPadding" />
                    <!---End Single Item-->
                </template>
            </v-list>
            <div v-else-if="menuLoading" class="pa-4 text-center text-body-2 text-medium-emphasis">
                Menü yükleniyor...
            </div>
            <!-- Menu boş ve yükleme tamamlandı - hiçbir şey gösterme -->
        </perfect-scrollbar>
    </v-navigation-drawer>
</template>
