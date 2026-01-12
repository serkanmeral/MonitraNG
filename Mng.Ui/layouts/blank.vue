// ===============================|| Blank Layout ||=============================== //
<script setup lang="ts">
import { useCustomizerStore } from '@/stores/customizer';
import { useLocaleStore } from '@/stores/locale';
import { computed, watch } from 'vue';

const customizer = useCustomizerStore();
const localeStore = useLocaleStore();

// RTL support: Use locale store's isRTL instead of customizer.setRTLLayout
const isRTL = computed(() => localeStore.isRTL);

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
  <!-----RTL LAYOUT------->
  <v-locale-provider v-if="isRTL" rtl>
    <v-app :theme="customizer.actTheme">
      <NuxtPage />
    </v-app>
  </v-locale-provider>

  <!-----LTR LAYOUT------->
  <v-locale-provider v-else>
    <v-app :theme="customizer.actTheme">
      <NuxtPage />
    </v-app>
  </v-locale-provider>
</template>


