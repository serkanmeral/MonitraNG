<script setup lang="ts">
import { computed } from 'vue';
import { useNuxtApp } from '#app';
import { useLocaleStore } from '@/stores/locale';
import Icon from '../../vertical-sidebar/Icon.vue';

interface Props {
  item: {
    icon?: any;
    iconType?: 'mdi' | 'tabler';
    iconName?: string;
    title?: string;
    pageCode?: string; // i18n key için kullanılacak unique identifier
    to?: string;
    type?: string;
    disabled?: boolean;
    subCaption?: string;
    chip?: string;
    chipColor?: string;
    chipBgColor?: string;
    chipVariant?: string;
    chipIcon?: string;
  };
  level?: number;
}

const props = withDefaults(defineProps<Props>(), {
  level: 0,
});

// Get i18n instance and locale store for reactivity
const nuxtApp = useNuxtApp();
const localeStore = useLocaleStore();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;

// Computed property for menu title - reactive to locale changes
const menuTitle = computed(() => {
  // Access localeStore.locale to make this computed reactive to locale changes
  const currentLocale = localeStore.locale;
  
  if (!props.item.pageCode || !i18n) {
    return props.item.title ? i18n?.t?.(props.item.title) || props.item.title : '';
  }
  
  const translationKey = `menu.${props.item.pageCode}`;
  
  // Try to get the value directly from messages to handle object values correctly
  let menuValue: any = null;
  
  // First, try to access messages directly
  const i18nGlobal = i18n?.global || i18n;
  const messages = i18nGlobal?.messages || {};
  const localeMessages = messages[currentLocale] || messages.value?.[currentLocale] || {};
  
  // Try direct access: menu.apps-automated-forms
  if (localeMessages.menu && localeMessages.menu[props.item.pageCode]) {
    menuValue = localeMessages.menu[props.item.pageCode];
  } else {
    // Fallback to i18n.t() if direct access doesn't work
    menuValue = i18n.t(translationKey);
  }
  
  // If translation not found, return original title
  if (!menuValue || menuValue === translationKey || (typeof menuValue === 'string' && menuValue.startsWith('menu.'))) {
    return props.item.title ? i18n?.t?.(props.item.title) || props.item.title : '';
  }
  
  // If it's an object, get title property, otherwise use the value directly
  if (typeof menuValue === 'object' && menuValue !== null && menuValue.title) {
    return menuValue.title;
  }
  
  return menuValue;
});

// Computed property for menu subCaption - reactive to locale changes
const menuSubCaption = computed(() => {
  const currentLocale = localeStore.locale;
  
  if (!props.item.subCaption || !props.item.pageCode || !i18n) {
    return props.item.subCaption || '';
  }
  
  const menuValue = i18n.t(`menu.${props.item.pageCode}`);
  
  // If it's an object with subCaption property, use it
  if (typeof menuValue === 'object' && menuValue !== null && menuValue.subCaption) {
    return menuValue.subCaption;
  }
  
  // Otherwise try nested key access
  const subCaptionValue = i18n.t(`menu.${props.item.pageCode}.subCaption`);
  if (subCaptionValue !== `menu.${props.item.pageCode}.subCaption`) {
    return subCaptionValue;
  }
  
  // Fallback to item.subCaption
  return props.item.subCaption;
});
</script>

<template>
    <!---Single Item-->
    <router-link 
        :to="item.type === 'external' ? '' : item.to" 
        :href="item.type === 'external' ? item.to : ''"
        class="navItemLink rounded-md" 
        :disabled="item.disabled"
        :target="item.type === 'external' ? '_blank' : ''"
    >
        <!---If icon-->
        <i class="navIcon">
            <Icon 
                :item="item.icon" 
                :iconName="item.iconName || (typeof item.icon === 'string' ? item.icon : null)"
                :iconType="item.iconType || 'tabler'"
                :level="level" 
            />
        </i>
        <span>{{ menuTitle }}</span>
        <!---If Caption-->
        <small v-if="item.subCaption" class="text-caption mt-n1 hide-menu">
            {{ menuSubCaption }}
        </small>
        <!---If any chip or label-->
        <template v-if="item.chip">
            <v-chip
                :color="item.chipColor"
                :class="'sidebarchip hide-menu ml-auto bg-' + (item.chipBgColor || '')"
                :size="item.chipIcon ? 'small' : 'small'"
                :variant="item.chipVariant"
                :prepend-icon="item.chipIcon"
            >
                {{ item.chip }}
            </v-chip>
        </template>
    </router-link>
</template>
