<script setup lang="ts">
import { useLocaleStore } from '@/stores/locale';
import { computed, watch, nextTick, ref } from 'vue';

// Import flag images
import flagTR from '/images/flag/icon-flag-tr.svg';
import flagEN from '/images/flag/icon-flag-en.svg';
import flagFR from '/images/flag/icon-flag-fr.svg';
import flagAR from '/images/flag/icon-flag-ro.svg'; // Arabic uses 'ro' flag
import flagZH from '/images/flag/icon-flag-zh.svg';

definePageMeta({
  layout: "blank",
});

const localeStore = useLocaleStore();

// Initialize locale on mount (for login page, before authentication)
if (process.client) {
  localeStore.initializeLocale();
}

// Locale options for combobox with flags
const localeOptions = [
  { value: 'tr', title: 'Türkçe', flag: flagTR },
  { value: 'en', title: 'English', flag: flagEN },
  { value: 'fr', title: 'Français', flag: flagFR },
  { value: 'ar', title: 'العربية', flag: flagAR },
  { value: 'zh', title: '中文', flag: flagZH }
];

// Get current flag based on locale
const currentFlag = computed(() => {
  const currentI18nLocale = localeStore.locale === 'ar' ? 'ro' : localeStore.locale;
  const option = localeOptions.find(opt => opt.value === localeStore.locale);
  return option?.flag || flagEN;
});

// RTL support for Arabic
const isRTL = computed(() => localeStore.isRTL);

// Force re-render key for locale changes (to ensure translations update)
// Use a ref instead of computed to force updates
const localeKey = ref(localeStore.locale);

// Watch locale changes and update key
watch(() => localeStore.locale, (newLocale) => {
  localeKey.value = newLocale;
}, { immediate: true });

// Handle locale change
const handleLocaleChange = async (locale: string) => {
  localeStore.setLocale(locale as any);
  
  // Manually update i18n locale (for login page, before authentication)
  if (process.client) {
    const nuxtApp = useNuxtApp();
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    
    if (i18n) {
      // IMPORTANT: In messages.ts, Arabic is stored as 'ro', not 'ar'
      // So we need to map 'ar' to 'ro' for i18n
      const i18nLocale = locale === 'ar' ? 'ro' : locale;
      
      // Update i18n locale
      // In legacy mode, locale is a string property
      i18n.locale = i18nLocale;
      
      // Also try to update global.locale if it exists (for composition API mode)
      if (i18n.global && i18n.global.locale) {
        if (typeof i18n.global.locale === 'object' && 'value' in i18n.global.locale) {
          i18n.global.locale.value = i18nLocale;
        } else {
          i18n.global.locale = i18nLocale;
        }
      }
      
      // Force Vue to reactively update by triggering a re-render
      // In legacy mode, we need to manually trigger updates
      await nextTick();
      
      // Force component re-render by updating a reactive key
      // This ensures all $t() calls are re-evaluated
      localeKey.value = locale; // Update key to force re-render
    }
  }
};

// Update HTML dir attribute when locale changes
if (process.client) {
  watch(() => localeStore.locale, (newLocale) => {
    const htmlElement = document.documentElement;
    if (newLocale === 'ar') {
      htmlElement.setAttribute('dir', 'rtl');
      htmlElement.setAttribute('lang', 'ar');
    } else {
      htmlElement.setAttribute('dir', 'ltr');
      htmlElement.setAttribute('lang', newLocale);
    }
  }, { immediate: true });
}
</script>
<template>
  <v-locale-provider :rtl="isRTL">
    <div class="pa-3" :dir="isRTL ? 'rtl' : 'ltr'">
    <v-row class="h-100vh mh-100 auth">
      <v-col
        cols="12"
        lg="7"
        xl="8"
        class="d-lg-flex align-center justify-center authentication position-relative"
      >
        <div class="auth-header pt-lg-6 pt-2 px-sm-6 px-3 pb-lg-6 pb-0">
          <div class="position-relative">
            <LcFullLogoAuthLogo/>
          </div>
        </div>
        <div class="">
          <img
            src="/images/backgrounds/login-bg.svg" height="450"
            class="position-relative d-none d-lg-flex"
            alt="login-background"
          />
        </div>
      </v-col>
      <v-col cols="12" lg="5" xl="4" class="d-flex align-center justify-center bg-surface">
        <div class="mt-xl-0 mt-5 mw-100 position-relative" style="width: 100%; max-width: 400px;">
          <!-- Language Selector -->
          <div class="d-flex mb-4" :class="isRTL ? 'justify-start' : 'justify-end'">
            <v-select
              :model-value="localeStore.locale"
              :items="localeOptions"
              item-title="title"
              item-value="value"
              variant="outlined"
              density="compact"
              hide-details
              style="max-width: 200px;"
              @update:model-value="handleLocaleChange"
            >
              <template #item="{ props: itemProps, item }">
                <v-list-item v-bind="itemProps" :title="item.raw.title">
                  <template #prepend>
                    <v-avatar size="22" :class="isRTL ? 'ml-2' : 'mr-2'">
                      <img :src="item.raw.flag" :alt="item.raw.value" width="22" height="22" class="obj-cover" />
                    </v-avatar>
                  </template>
                </v-list-item>
              </template>
              <template #selection="{ item }">
                <div class="d-flex align-center">
                  <v-avatar size="18" :class="isRTL ? 'ml-2' : 'mr-2'">
                    <img :src="item.raw.flag" :alt="item.raw.value" width="18" height="18" class="obj-cover" />
                  </v-avatar>
                  <span>{{ item.raw.title }}</span>
                </div>
              </template>
            </v-select>
          </div>
          
          <h2 :key="`title-${localeKey}`" class="text-h3 font-weight-semibold mb-2">{{ $t('login.title') }}</h2>
          <div :key="`subtitle-${localeKey}`" class="text-subtitle-1 mb-6">{{ $t('login.subtitle') }}</div>
          <AuthLoginForm :key="`form-${localeKey}`" />
        </div>
      </v-col>
    </v-row>
    </div>
  </v-locale-provider>
</template>
