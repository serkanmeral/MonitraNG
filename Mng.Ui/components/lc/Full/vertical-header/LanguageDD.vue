<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useLocaleStore } from '@/stores/locale';
import { useUserPreferencesStore } from '@/stores/apps/userPreferences';
import { useAuthStore } from '@/stores/auth';
import { useCustomizerStore } from '@/stores/customizer';
import { languageDD } from '@/_mockApis/headerData';
import flagTR from '/images/flag/icon-flag-tr.svg';
import flag1 from '/images/flag/icon-flag-en.svg';
import flag2 from '/images/flag/icon-flag-ro.svg';
import flag3 from '/images/flag/icon-flag-zh.svg';
import flag4 from '/images/flag/icon-flag-fr.svg';

const localeStore = useLocaleStore();
const preferencesStore = useUserPreferencesStore();
const authStore = useAuthStore();
const customizerStore = useCustomizerStore();

// Get current i18n locale (mapped: ar -> ro)
const currentI18nLocale = computed(() => {
  return localeStore.locale === 'ar' ? 'ro' : localeStore.locale;
});

// Handle language change
const handleLanguageChange = async (locale: string) => {
  // Map 'ro' back to 'ar' for locale store
  const storeLocale = locale === 'ro' ? 'ar' : locale;
  
  // Update locale store (this will trigger RTL updates)
  localeStore.setLocale(storeLocale as any);
  
  // Update i18n locale
  if (process.client) {
    const nuxtApp = useNuxtApp();
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    
    if (i18n) {
      i18n.locale = locale;
      
      // Also update global.locale if it exists
      if (i18n.global && i18n.global.locale) {
        if (typeof i18n.global.locale === 'object' && 'value' in i18n.global.locale) {
          i18n.global.locale.value = locale;
        } else {
          i18n.global.locale = locale;
        }
      }
    }
  }
  
  // Update HTML dir attribute
  if (process.client) {
    const htmlElement = document.documentElement;
    if (storeLocale === 'ar') {
      htmlElement.setAttribute('dir', 'rtl');
      htmlElement.setAttribute('lang', 'ar');
    } else {
      htmlElement.setAttribute('dir', 'ltr');
      htmlElement.setAttribute('lang', storeLocale);
    }
  }
  
  // Save locale preference to dataset (if user is authenticated)
  if (authStore.isAuthenticated) {
    try {
      await preferencesStore.savePreferences({
        locale: storeLocale as any,
        theme: customizerStore.actTheme, // Preserve current theme
      });
    } catch (error) {
      // Silently handle errors - preference save failure shouldn't block language change
      // Dataset might not exist yet, which is OK
    }
  }
};
</script>
<template>
    <!-- ---------------------------------------------- -->
    <!-- language DD -->
    <!-- ---------------------------------------------- -->
    <v-menu :close-on-content-click="false" location="bottom">
        <template v-slot:activator="{ props }">
            <v-btn icon variant="text" color="primary" v-bind="props">
                <v-avatar size="22">
                    <img v-if="currentI18nLocale === 'tr'" :src="flagTR" :alt="currentI18nLocale" width="22" height="22" class="obj-cover" />
                    <img v-if="currentI18nLocale === 'en'" :src="flag1" :alt="currentI18nLocale" width="22" height="22" class="obj-cover" />
                    <img v-if="currentI18nLocale === 'fr'" :src="flag4" :alt="currentI18nLocale" width="22" height="22" class="obj-cover" />
                    <img v-if="currentI18nLocale === 'ro'" :src="flag2" :alt="currentI18nLocale" width="22" height="22" class="obj-cover" />
                    <img v-if="currentI18nLocale === 'zh'" :src="flag3" :alt="currentI18nLocale" width="22" height="22" class="obj-cover" />
                </v-avatar>
            </v-btn>
        </template>
        <v-sheet rounded="md" width="200" elevation="10">
            <v-list class="theme-list">
                <v-list-item
                    v-for="(item, index) in languageDD"
                    :key="index"
                    color="primary"
                    :active="currentI18nLocale == item.value"
                    class="d-flex align-center"
                    @click="handleLanguageChange(item.value)"
                >
                    <template v-slot:prepend>
                        <v-avatar size="22">
                            <img :src="item.avatar" :alt="item.avatar" width="22" height="22" class="obj-cover" />
                        </v-avatar>
                    </template>
                    <v-list-item-title class="text-subtitle-1 font-weight-regular">
                        {{ item.title }}
                        <span class="text-disabled text-subtitle-1 pl-2">({{ item.subtext }})</span>
                    </v-list-item-title>
                </v-list-item>
            </v-list>
        </v-sheet>
    </v-menu>
</template>
