<script setup lang="ts">
import { useCustomizerStore } from '@/stores/customizer';
import { useLocaleStore } from '@/stores/locale';
import { computed, watch } from 'vue';
import { pl, zhHans } from 'vuetify/locale'
import ChatbotWidget from '@/components/apps/chatbot/ChatbotWidget.vue'

const route = useRoute();
const customizer = useCustomizerStore();
const localeStore = useLocaleStore();

/** Harita ve sohbet odası: içerik alanı viewport yüksekliğinde (tam panel); diğer sayfalarda normal akış */
const isMapPage = computed(() => route.path.startsWith('/apps/monitoring/map'));
const isPageWrapperFullHeight = computed(
  () => isMapPage.value || route.path === '/apps/chat-room'
);

// RTL support: Use locale store's isRTL instead of customizer.setRTLLayout
const isRTL = computed(() => localeStore.isRTL);

// Sync customizer.setRTLLayout with locale store (for backward compatibility)
watch(() => localeStore.locale, (newLocale) => {
  // Update customizer RTL layout based on locale
  // This ensures other parts of the app that use customizer.setRTLLayout still work
  // But we'll use isRTL computed property in the template for actual RTL control
}, { immediate: true });

const title = ref("Monitra NG");
useHead({
  meta: [{ content: title }],
  titleTemplate: (titleChunk) => {
    return titleChunk
      ? `${titleChunk} - Monitra NG`
      : "Monitra NG";
  },
});

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
        <v-app
            :theme="customizer.actTheme"
            :class="[
                customizer.actTheme,
                customizer.mini_sidebar ? 'mini-sidebar' : '',
                customizer.setHorizontalLayout ? 'horizontalLayout' : 'verticalLayout',
                customizer.setBorderCard ? 'cardBordered' : ''
            ]"
        >

            <!---Customizer location left side--->
            <v-navigation-drawer app temporary elevation="10" location="left" v-model="customizer.Customizer_drawer"
                width="320" class="left-customizer">
                <LcFullCustomizer />
            </v-navigation-drawer>
            <LcFullVerticalHeader v-if="!customizer.setHorizontalLayout" />
            <LcFullVerticalSidebar v-if="!customizer.setHorizontalLayout" />
            <LcFullHorizontalHeader v-if="customizer.setHorizontalLayout" />
            <LcFullHorizontalSidebar v-if="customizer.setHorizontalLayout" />
            <v-main :class="{ 'v-main--viewport-fill': isPageWrapperFullHeight }">
               <v-container fluid :class="['page-wrapper', 'pb-sm-15', 'pb-10', { 'page-wrapper-full-height': isPageWrapperFullHeight }]">
                    <div :class="[customizer.boxed ? 'maxWidth' : '', { 'page-wrapper-inner-full-height': isPageWrapperFullHeight }]">
                        <NuxtPage />
                    </div>
                </v-container>
            </v-main>

            <!-- Chatbot Widget -->
            <ChatbotWidget />
        </v-app>
    </v-locale-provider>


    <!-----LTR LAYOUT------->
    <v-locale-provider v-else>
        <v-app
            :theme="customizer.actTheme"
            :class="[
                customizer.actTheme,
                customizer.mini_sidebar ? 'mini-sidebar' : '',
                customizer.setHorizontalLayout ? 'horizontalLayout' : 'verticalLayout',
                customizer.setBorderCard ? 'cardBordered' : ''
            ]"
        >

            <!---Customizer location right side--->
            <v-navigation-drawer app temporary elevation="10" location="right" v-model="customizer.Customizer_drawer"
                width="320" >
                <LcFullCustomizer />
            </v-navigation-drawer>
            <LcFullVerticalHeader v-if="!customizer.setHorizontalLayout" />
            <LcFullVerticalSidebar v-if="!customizer.setHorizontalLayout" />
            <LcFullHorizontalHeader v-if="customizer.setHorizontalLayout" />
            <LcFullHorizontalSidebar v-if="customizer.setHorizontalLayout" />
            <v-main :class="{ 'v-main--viewport-fill': isPageWrapperFullHeight }">
               <v-container fluid :class="['page-wrapper', 'pb-sm-15', 'pb-10', { 'page-wrapper-full-height': isPageWrapperFullHeight }]">
                    <div :class="[customizer.boxed ? 'maxWidth' : '', { 'page-wrapper-inner-full-height': isPageWrapperFullHeight }]">
                        <NuxtPage />
                    </div>
                </v-container>
            </v-main>

            <!-- Chatbot Widget -->
            <ChatbotWidget />
        </v-app>
    </v-locale-provider>
</template>

<style>
/*
 * Harita ve sohbet odası: viewport yüksekliği + iç içe scroll.
 * _container.scss .page-wrapper { min-height: calc(100vh - 100px) } flex zincirinde taşmayı tetikleyebilir; burada ezilir.
 */
.v-app .v-main.v-main--viewport-fill {
  display: flex;
  flex-direction: column;
  flex: 1 1 auto;
  min-height: 0;
  overflow: hidden;
}

.v-main .page-wrapper.page-wrapper-full-height {
  flex: 1 1 0;
  height: 100%;
  max-height: 100%;
  min-height: 0 !important;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.v-main .page-wrapper.page-wrapper-full-height > div.page-wrapper-inner-full-height {
  flex: 1 1 0;
  min-height: 0;
  max-height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
</style>
