<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useLocaleStore } from '@/stores/locale';
import { useCustomizerStore } from '@/stores/customizer';
import { useUserPreferencesStore } from '@/stores/apps/userPreferences';
import { useAuthStore } from '@/stores/auth';
import type { SupportedLocale } from '@/stores/locale';
import {
  LayoutColumnsIcon,
  LayoutNavbarIcon,
  LayoutDistributeVerticalIcon,
  LayoutDistributeHorizontalIcon,
  LayoutSidebarIcon,
  LayoutSidebarLeftCollapseIcon,
} from 'vue-tabler-icons';

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

const localeStore = useLocaleStore();
const customizerStore = useCustomizerStore();
const preferencesStore = useUserPreferencesStore();
const authStore = useAuthStore();

// Form data
const formData = ref({
  locale: localeStore.locale,
  theme: customizerStore.actTheme,
  horizontalLayout: customizerStore.setHorizontalLayout,
  boxed: customizerStore.boxed,
  miniSidebar: customizerStore.mini_sidebar,
  borderCard: customizerStore.setBorderCard,
});

// Loading state
const isLoading = ref(false);
const saveSuccess = ref(false);

// Get native name for locale
const getLocaleNativeName = (locale: SupportedLocale): string => {
  const names: Record<SupportedLocale, string> = {
    tr: 'Türkçe',
    en: 'English',
    fr: 'Français',
    ar: 'العربية',
    zh: '中文'
  };
  return names[locale];
};

// Theme options
const themeOptions = [
  { value: 'BLUE_THEME', title: 'Blue', color: 'themeBlue' },
  { value: 'AQUA_THEME', title: 'Aqua', color: 'themeAqua' },
  { value: 'PURPLE_THEME', title: 'Purple', color: 'themePurple' },
  { value: 'GREEN_THEME', title: 'Green', color: 'themeGreen' },
  { value: 'CYAN_THEME', title: 'Cyan', color: 'themeCyan' },
  { value: 'ORANGE_THEME', title: 'Orange', color: 'themeOrange' },
  { value: 'DARK_BLUE_THEME', title: 'Dark Blue', color: 'themeDarkBlue' },
  { value: 'DARK_AQUA_THEME', title: 'Dark Aqua', color: 'themeDarkAqua' },
  { value: 'DARK_PURPLE_THEME', title: 'Dark Purple', color: 'themeDarkPurple' },
  { value: 'DARK_GREEN_THEME', title: 'Dark Green', color: 'themeDarkGreen' },
  { value: 'DARK_CYAN_THEME', title: 'Dark Cyan', color: 'themeDarkCyan' },
  { value: 'DARK_ORANGE_THEME', title: 'Dark Orange', color: 'themeDarkOrange' },
];

// Save preferences to dataset
const savePreferences = async () => {
  isLoading.value = true;
  saveSuccess.value = false;
  
  try {
    await preferencesStore.savePreferences({
      locale: formData.value.locale,
      theme: formData.value.theme,
      horizontalLayout: formData.value.horizontalLayout,
      boxed: formData.value.boxed,
      miniSidebar: formData.value.miniSidebar,
      borderCard: formData.value.borderCard,
    });
    
    saveSuccess.value = true;
    setTimeout(() => {
      saveSuccess.value = false;
    }, 3000);
  } catch (error: any) {
    alert(t('profile.preferences.saveError') || 'Tercihler kaydedilirken bir hata oluştu');
  } finally {
    isLoading.value = false;
  }
};

// Handle locale change
const handleLocaleChange = async (locale: SupportedLocale) => {
  localeStore.setLocale(locale, true);
  
  // Update i18n locale
  if (process.client) {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n) {
      // Map 'ar' to 'ro' for i18n (as per existing implementation)
      const i18nLocale = locale === 'ar' ? 'ro' : locale;
      i18n.locale = i18nLocale;
      
      if (i18n.global && i18n.global.locale) {
        if (typeof i18n.global.locale === 'object' && 'value' in i18n.global.locale) {
          i18n.global.locale.value = i18nLocale;
        } else {
          i18n.global.locale = i18nLocale;
        }
      }
    }
    
    // Update HTML dir attribute
    const htmlElement = document.documentElement;
    if (locale === 'ar') {
      htmlElement.setAttribute('dir', 'rtl');
      htmlElement.setAttribute('lang', 'ar');
    } else {
      htmlElement.setAttribute('dir', 'ltr');
      htmlElement.setAttribute('lang', locale);
    }
  }
  
  formData.value.locale = locale;
  
  // Auto-save to dataset
  await savePreferences();
};

// Handle theme change
const handleThemeChange = async (theme: string) => {
  customizerStore.SET_THEME(theme);
  formData.value.theme = theme;
  
  // Auto-save to dataset
  await savePreferences();
};

// Handle layout change
const handleHorizontalLayoutChange = async (value: boolean) => {
  customizerStore.SET_LAYOUT(value);
  formData.value.horizontalLayout = value;
  
  // Auto-save to dataset
  await savePreferences();
};

// Handle boxed change
const handleBoxedChange = async (value: boolean) => {
  customizerStore.boxed = value;
  formData.value.boxed = value;
  
  // Auto-save to dataset
  await savePreferences();
};

// Handle mini sidebar change
const handleMiniSidebarChange = async (value: boolean) => {
  customizerStore.SET_MINI_SIDEBAR(value);
  formData.value.miniSidebar = value;
  
  // Auto-save to dataset
  await savePreferences();
};

// Handle border card change
const handleBorderCardChange = async (value: boolean) => {
  customizerStore.SET_CARD_BORDER(value);
  formData.value.borderCard = value;
  
  // Auto-save to dataset
  await savePreferences();
};

onMounted(async () => {
  // Try to load preferences from dataset
  const userId = authStore.userInfo?.sub || authStore.userInfo?.username;
  if (userId) {
    try {
      const prefs = await preferencesStore.loadPreferences(userId);
      if (prefs) {
        // Apply loaded preferences
        preferencesStore.applyPreferences(prefs);
        formData.value = {
          locale: prefs.locale || localeStore.locale,
          theme: prefs.theme || customizerStore.actTheme,
          horizontalLayout: prefs.horizontalLayout ?? customizerStore.setHorizontalLayout,
          boxed: prefs.boxed ?? customizerStore.boxed,
          miniSidebar: prefs.miniSidebar ?? customizerStore.mini_sidebar,
          borderCard: prefs.borderCard ?? customizerStore.setBorderCard,
        };
      } else {
        // No preferences found, use current store values
        formData.value = {
          locale: localeStore.locale,
          theme: customizerStore.actTheme,
          horizontalLayout: customizerStore.setHorizontalLayout,
          boxed: customizerStore.boxed,
          miniSidebar: customizerStore.mini_sidebar,
          borderCard: customizerStore.setBorderCard,
        };
      }
    } catch (error) {
      // Dataset might not exist yet, use current store values
      formData.value = {
        locale: localeStore.locale,
        theme: customizerStore.actTheme,
        horizontalLayout: customizerStore.setHorizontalLayout,
        boxed: customizerStore.boxed,
        miniSidebar: customizerStore.mini_sidebar,
        borderCard: customizerStore.setBorderCard,
      };
    }
  } else {
    // No user ID, use current store values
    formData.value = {
      locale: localeStore.locale,
      theme: customizerStore.actTheme,
      horizontalLayout: customizerStore.setHorizontalLayout,
      boxed: customizerStore.boxed,
      miniSidebar: customizerStore.mini_sidebar,
      borderCard: customizerStore.setBorderCard,
    };
  }
});
</script>

<template>
  <v-row>
    <!-- Language Preference Card -->
    <v-col cols="12" lg="6" md="12">
      <v-card elevation="10" class="mb-4">
        <v-card-item>
          <h5 class="text-h5 mb-4">{{ t('profile.preferences.language.title') }}</h5>
          <v-select
            v-model="formData.locale"
            :items="localeStore.availableLocales.map(locale => ({
              value: locale,
              title: getLocaleNativeName(locale)
            }))"
            item-title="title"
            item-value="value"
            :label="t('profile.preferences.language.label')"
            variant="outlined"
            density="comfortable"
            @update:model-value="handleLocaleChange"
          >
            <template v-slot:item="{ props, item }">
              <v-list-item v-bind="props">
                <template v-slot:prepend>
                  <v-icon v-if="item.raw?.value === localeStore.locale || item.raw === localeStore.locale" color="primary">mdi-check</v-icon>
                </template>
              </v-list-item>
            </template>
          </v-select>
          <p class="text-caption text-medium-emphasis mt-2">
            {{ t('profile.preferences.language.description') }}
          </p>
        </v-card-item>
      </v-card>
    </v-col>

    <!-- Theme Preference Card -->
    <v-col cols="12" lg="6" md="12">
      <v-card elevation="10" class="mb-4">
        <v-card-item>
          <h5 class="text-h5 mb-4">{{ t('profile.preferences.theme.title') }}</h5>
          <v-row>
            <v-col 
              cols="4" 
              v-for="theme in themeOptions" 
              :key="theme.value"
              class="pa-2"
            >
              <v-sheet
                rounded="md"
                class="border cursor-pointer d-block text-center px-3 py-4 hover-btns"
                :class="{ 'border-primary': formData.theme === theme.value }"
                elevation="9"
                @click="handleThemeChange(theme.value)"
              >
                <v-avatar :class="theme.color" size="25" class="mb-2">
                  <v-icon 
                    v-if="formData.theme === theme.value" 
                    color="white" 
                    size="18"
                  >
                    mdi-check
                  </v-icon>
                </v-avatar>
                <p class="text-caption mt-2 mb-0">{{ theme.title }}</p>
              </v-sheet>
            </v-col>
          </v-row>
          <p class="text-caption text-medium-emphasis mt-4">
            {{ t('profile.preferences.theme.description') }}
          </p>
        </v-card-item>
      </v-card>
    </v-col>

    <!-- Layout Preferences -->
    <v-col cols="12" lg="6" md="12">
      <v-card elevation="10" class="mb-4">
        <v-card-item>
          <h5 class="text-h5 mb-4">{{ t('profile.preferences.layout.title') }}</h5>
          
          <!-- Sidebar Layout -->
          <div class="mb-6">
            <h6 class="text-subtitle-1 mb-3">{{ t('profile.preferences.layout.sidebarLayout') }}</h6>
            <v-btn-toggle 
              v-model="formData.horizontalLayout" 
              color="primary" 
              class="btn-group-custom gap-3" 
              rounded="0" 
              group
              @update:model-value="handleHorizontalLayoutChange"
            >
              <v-btn :value="false" variant="text" elevation="9" class="rounded-md">
                <LayoutColumnsIcon stroke-width="1.5" size="21" class="mr-2 icon" />
                {{ t('profile.preferences.layout.vertical') }}
              </v-btn>
              <v-btn :value="true" variant="text" elevation="9" class="rounded-md">
                <LayoutNavbarIcon stroke-width="1.5" size="21" class="mr-2 icon" />
                {{ t('profile.preferences.layout.horizontal') }}
              </v-btn>
            </v-btn-toggle>
          </div>

          <!-- Container Option -->
          <div class="mb-6">
            <h6 class="text-subtitle-1 mb-3">{{ t('profile.preferences.layout.containerOption') }}</h6>
            <v-btn-toggle 
              v-model="formData.boxed" 
              color="primary" 
              class="btn-group-custom gap-3" 
              rounded="0" 
              group
              @update:model-value="handleBoxedChange"
            >
              <v-btn :value="true" variant="text" elevation="9" class="rounded-md">
                <LayoutDistributeVerticalIcon stroke-width="1.5" size="21" class="mr-2 icon" />
                {{ t('profile.preferences.layout.boxed') }}
              </v-btn>
              <v-btn :value="false" variant="text" elevation="9" class="rounded-md">
                <LayoutDistributeHorizontalIcon stroke-width="1.5" size="21" class="mr-2 icon" />
                {{ t('profile.preferences.layout.full') }}
              </v-btn>
            </v-btn-toggle>
          </div>

          <!-- Sidebar Type (only for vertical layout) -->
          <div class="mb-6" v-if="!formData.horizontalLayout">
            <h6 class="text-subtitle-1 mb-3">{{ t('profile.preferences.layout.sidebarType') }}</h6>
            <v-btn-toggle 
              v-model="formData.miniSidebar" 
              color="primary" 
              class="btn-group-custom gap-3" 
              rounded="0" 
              group
              @update:model-value="handleMiniSidebarChange"
            >
              <v-btn :value="false" variant="text" elevation="9" class="rounded-md">
                <LayoutSidebarIcon stroke-width="1.5" size="21" class="mr-2 icon" />
                {{ t('profile.preferences.layout.full') }}
              </v-btn>
              <v-btn :value="true" variant="text" elevation="9" class="rounded-md">
                <LayoutSidebarLeftCollapseIcon stroke-width="1.5" size="21" class="mr-2 icon" />
                {{ t('profile.preferences.layout.collapse') }}
              </v-btn>
            </v-btn-toggle>
          </div>

          <!-- Card Style -->
          <div>
            <h6 class="text-subtitle-1 mb-3">{{ t('profile.preferences.layout.cardStyle') }}</h6>
            <v-btn-toggle 
              v-model="formData.borderCard" 
              color="primary" 
              class="btn-group-custom gap-3" 
              rounded="0" 
              group
              @update:model-value="handleBorderCardChange"
            >
              <v-btn :value="false" variant="text" elevation="9" class="rounded-md">
                <LayoutSidebarLeftCollapseIcon stroke-width="1.5" size="21" class="mr-2 icon" />
                {{ t('profile.preferences.layout.shadow') }}
              </v-btn>
              <v-btn :value="true" variant="text" elevation="9" class="rounded-md">
                <LayoutSidebarIcon stroke-width="1.5" size="21" class="mr-2 icon" />
                {{ t('profile.preferences.layout.border') }}
              </v-btn>
            </v-btn-toggle>
          </div>
        </v-card-item>
      </v-card>
    </v-col>
  </v-row>
</template>

<style scoped>
.hover-btns:hover {
  transform: scale(1.05);
  transition: transform 0.2s;
}
</style>
