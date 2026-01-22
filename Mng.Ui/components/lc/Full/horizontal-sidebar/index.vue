<script setup lang="ts">
import { ref, shallowRef, computed, onMounted, watch } from 'vue';
import { useDisplay } from 'vuetify';
import { useCustomizerStore } from '@/stores/customizer';
import { useAuthStore } from '@/stores/auth';
import { useSideMenuStore } from '@/stores/apps/sideMenu';
import { useLocaleStore } from '@/stores/locale';
import type { menu } from '../vertical-sidebar/sidebarItem';
import HorizontalItems from './horizontalItems';

const customizer = useCustomizerStore();
const authStore = useAuthStore();
const menuStore = useSideMenuStore();
const localeStore = useLocaleStore();
const config = useRuntimeConfig();
const nuxtApp = useNuxtApp();
const { mdAndUp } = useDisplay();

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
    sidebarMenu.value = HorizontalItems || [];
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
</script>

<template>
    <template v-if="mdAndUp">
        <div class="horizontalMenu border-bottom bg-surface position-relative">
            <div :class="customizer.boxed ? 'maxWidth' : 'px-6'">
                <ul class="gap-1 horizontal-navbar mx-lg-0 mx-3" v-if="sidebarMenu && sidebarMenu.length > 0">
                    <!---Menu Loop - Recursive rendering -->
                    <template v-for="(item, i) in sidebarMenu" :key="`menu-${i}-${item.title || item.header || i}`">
                        <!---Item Sub Header -->
                        <template v-if="item.header">
                            <!-- Header'lar horizontal menu'de dropdown olarak görünür -->
                            <li v-if="item.children && item.children.length > 0" class="navItem">
                                <LcFullHorizontalSidebarNavCollapse :item="item" :level="0" />
                            </li>
                            <!-- Header'ın children'ı yoksa sadece header gösterilmez (normalde olmaz ama güvenlik için) -->
                        </template>
                        <!---If Has Child (no header) -->
                        <li v-else-if="item.children && item.children.length > 0" class="navItem">
                            <LcFullHorizontalSidebarNavCollapse :item="item" :level="0" />
                        </li>
                        <!---Single Item-->
                        <li v-else class="navItem">
                            <LcFullHorizontalSidebarNavItem :item="item" :level="0" />
                        </li>
                        <!---End Single Item-->
                    </template>
                </ul>
                <div v-else-if="menuLoading" class="pa-4 text-center text-body-2 text-medium-emphasis">
                    Menü yükleniyor...
                </div>
                <!-- Menu boş ve yükleme tamamlandı - hiçbir şey gösterme -->
            </div>    
        </div>
    </template>
    <div v-else class="mobile-menu">
        <LcFullVerticalSidebar />
    </div>
</template>
<style lang="scss"></style>
