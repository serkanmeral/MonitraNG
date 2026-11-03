<script setup lang="ts">
import { useLocaleStore } from '@/stores/locale';
import { useCustomizerStore } from '@/stores/customizer';
import { computed, watch, nextTick, ref, onMounted } from 'vue';
import { isDarkAppTheme, resolveThemedDomainLogoSrc } from '@/composables/useDomain';
import { currentLoginHostDomain, fetchDomainBranding, fetchSoleDomainBranding, type DomainBranding } from '@/utils/loginHostDomain';

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
const customizer = useCustomizerStore();
const hostDomain = currentLoginHostDomain();
const soleBranding = await fetchSoleDomainBranding();
const lockedDomain = soleBranding?.name || hostDomain || null;
const branding = ref<DomainBranding | null>(soleBranding?.name ? soleBranding : null);

const brandTitle = computed(() => branding.value?.displayName || lockedDomain || '');
const logoSrc = computed(() => resolveThemedDomainLogoSrc(branding.value, customizer.actTheme));
const logoSurface = computed(() => (isDarkAppTheme(customizer.actTheme) ? '#2a3447' : '#ffffff'));

onMounted(async () => {
  if (!lockedDomain || branding.value) return;
  branding.value = await fetchDomainBranding(lockedDomain);
});

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

const currentLocaleOption = computed(
  () => localeOptions.find((opt) => opt.value === localeStore.locale) ?? localeOptions[1]
);

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
      <v-col cols="12" lg="5" xl="4" class="login-panel d-flex align-center justify-center bg-surface">
        <v-menu location="bottom end" :close-on-content-click="true">
          <template #activator="{ props: menuProps }">
            <v-btn
              v-bind="menuProps"
              class="login-locale"
              variant="text"
              size="small"
              rounded="pill"
            >
              <v-avatar size="18" :class="isRTL ? 'ml-2' : 'mr-2'">
                <img :src="currentLocaleOption.flag" :alt="currentLocaleOption.value" width="18" height="18" />
              </v-avatar>
              {{ currentLocaleOption.title }}
              <v-icon end size="16">mdi-chevron-down</v-icon>
            </v-btn>
          </template>
          <v-list density="compact" min-width="180" nav>
            <v-list-item
              v-for="option in localeOptions"
              :key="option.value"
              :active="option.value === localeStore.locale"
              rounded="lg"
              @click="handleLocaleChange(option.value)"
            >
              <template #prepend>
                <v-avatar size="20" :class="isRTL ? 'ml-3' : 'mr-3'">
                  <img :src="option.flag" :alt="option.value" width="20" height="20" />
                </v-avatar>
              </template>
              <v-list-item-title>{{ option.title }}</v-list-item-title>
            </v-list-item>
          </v-list>
        </v-menu>

        <div class="login-panel-body mt-xl-0 mt-5">
          <div v-if="lockedDomain" class="login-brand">
            <div class="login-domain-logo" :style="{ backgroundColor: logoSurface }">
              <img v-if="logoSrc" :src="logoSrc" :alt="brandTitle" />
            </div>
            <div class="login-brand-name">{{ brandTitle }}</div>
            <div class="login-brand-rule" />
          </div>
          <h2 :key="`title-${localeKey}`" class="text-h3 font-weight-semibold mb-2">{{ $t('login.title') }}</h2>
          <div :key="`subtitle-${localeKey}`" class="text-subtitle-1 mb-6">{{ $t('login.subtitle') }}</div>
          <AuthLoginForm :key="`form-${localeKey}`" :locked-domain="lockedDomain" />
        </div>
      </v-col>
    </v-row>
    </div>
  </v-locale-provider>
</template>

<style scoped>
.login-panel {
  position: relative;
  min-height: 100vh;
}

.login-locale {
  position: absolute;
  top: 16px;
  inset-inline-end: 16px;
  letter-spacing: 0;
  text-transform: none;
}

.login-panel-body {
  width: 100%;
  max-width: 400px;
}

.login-brand {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  margin-bottom: 28px;
}

.login-domain-logo {
  width: min(100%, 280px);
  min-height: 96px;
  border-radius: 18px;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 18px 22px;
  box-shadow: 0 10px 30px rgba(15, 23, 42, 0.06);
}

.login-domain-logo img {
  max-width: 100%;
  max-height: 72px;
  object-fit: contain;
}

.login-brand-name {
  margin-top: 16px;
  font-size: 1.05rem;
  font-weight: 600;
  letter-spacing: 0.01em;
  line-height: 1.35;
}

.login-brand-rule {
  width: 42px;
  height: 3px;
  margin-top: 14px;
  border-radius: 999px;
  background: rgb(var(--v-theme-primary));
}
</style>
