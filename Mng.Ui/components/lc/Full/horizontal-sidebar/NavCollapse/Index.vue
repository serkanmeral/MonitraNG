<script setup lang="ts">
import { computed } from 'vue';
import { useNuxtApp } from '#app';
import { useLocaleStore } from '@/stores/locale';
import { ChevronDownIcon } from 'vue-tabler-icons';
import Icon from '../../vertical-sidebar/Icon.vue';

interface Props {
  item: {
    icon?: any;
    iconType?: 'mdi' | 'tabler';
    iconName?: string;
    title?: string;
    pageCode?: string; // i18n key için kullanılacak unique identifier
    header?: string;
    subCaption?: string;
    children?: any[];
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
  
  // Try to get the value directly from messages to handle object values correctly
  const i18nGlobal = i18n?.global || i18n;
  const messages = i18nGlobal?.messages || {};
  const localeMessages = messages[currentLocale] || messages.value?.[currentLocale] || {};
  
  let menuValue: any = null;
  
  // Try direct access: menu.apps-automated-forms
  if (localeMessages.menu && localeMessages.menu[props.item.pageCode]) {
    menuValue = localeMessages.menu[props.item.pageCode];
  } else {
    // Fallback to i18n.t() if direct access doesn't work
    menuValue = i18n.t(`menu.${props.item.pageCode}`);
  }
  
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
    <!---Dropdown  -->
    <a class="navItemLink rounded-md cursor-pointer">
        <!---Icon  -->
        <i class="navIcon">
            <Icon 
                :item="item.icon" 
                :iconName="item.iconName || (typeof item.icon === 'string' ? item.icon : null)"
                :iconType="item.iconType || 'tabler'"
                :level="level" 
            />
        </i>
        <!---Title  -->
        <span class="mr-auto">{{ menuTitle }}</span>
        <!---If Caption-->
        <small v-if="item.subCaption" class="text-caption mt-n1 hide-menu">
            {{ menuSubCaption }}
        </small>
        <i class="ddIcon ml-2 d-flex align-center"><ChevronDownIcon size="15" /></i>
    </a>
    <!---Sub Item-->
    <ul :class="`ddMenu ddLevel-${level + 1}`" v-if="item.children && item.children.length > 0">
        <template v-for="(subitem, i) in item.children" :key="`subitem-${i}-${subitem.title || subitem.header || i}`">
            <!-- Nested Header: Eğer subitem bir header ise -->
            <template v-if="subitem.header">
                <!-- Nested header'ın children'larını recursive olarak render et -->
                <template v-if="subitem.children && subitem.children.length > 0">
                    <template v-for="(grandchild, k) in subitem.children" :key="`grandchild-${i}-${k}-${grandchild.title || grandchild.header || k}`">
                        <!-- Deep nested: Eğer grandchild da bir header ise -->
                        <template v-if="grandchild.header">
                            <template v-if="grandchild.children && grandchild.children.length > 0">
                                <template v-for="(greatGrandchild, l) in grandchild.children" :key="`greatGrandchild-${i}-${k}-${l}-${greatGrandchild.title || greatGrandchild.header || l}`">
                                    <li class="navItem">
                                        <LcFullHorizontalSidebarNavCollapse 
                                            v-if="greatGrandchild.children && greatGrandchild.children.length > 0"
                                            :item="greatGrandchild" 
                                            :level="level + 1" 
                                        />
                                        <LcFullHorizontalSidebarNavItem 
                                            v-else
                                            :item="greatGrandchild" 
                                            :level="level + 1" 
                                        />
                                    </li>
                                </template>
                            </template>
                        </template>
                        <!-- Normal Item veya Collapse: Eğer grandchild header değilse -->
                        <template v-else>
                            <li class="navItem">
                                <LcFullHorizontalSidebarNavCollapse 
                                    v-if="grandchild.children && grandchild.children.length > 0"
                                    :item="grandchild" 
                                    :level="level + 1" 
                                />
                                <LcFullHorizontalSidebarNavItem 
                                    v-else
                                    :item="grandchild" 
                                    :level="level + 1" 
                                />
                            </li>
                        </template>
                    </template>
                </template>
            </template>
            <!-- Normal Item veya Collapse: Eğer subitem header değilse -->
            <template v-else>
                <li class="navItem">
                    <LcFullHorizontalSidebarNavCollapse 
                        v-if="subitem.children && subitem.children.length > 0"
                        :item="subitem" 
                        :level="level + 1" 
                    />
                    <LcFullHorizontalSidebarNavItem 
                        v-else
                        :item="subitem" 
                        :level="level + 1" 
                    />
                </li>
            </template>
        </template>
    </ul>
    <!---End Item Sub Header -->
</template>
