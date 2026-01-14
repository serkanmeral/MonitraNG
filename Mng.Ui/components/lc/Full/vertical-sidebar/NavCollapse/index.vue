<script setup lang="ts">
import { computed } from 'vue';
import Icon from "../Icon.vue";

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

// Always show tooltip for menu items
const needsTooltip = computed(() => {
  return true;
});
</script>

<template>
  <!-- ---------------------------------------------- -->
  <!---Item Childern -->
  <!-- ---------------------------------------------- -->
  <v-list-group no-action>
    <!-- ---------------------------------------------- -->
    <!---Dropdown  -->
    <!-- ---------------------------------------------- -->
    <template v-slot:activator="{ props: activatorProps }">
      <v-tooltip
          location="right"
      >
          <template v-slot:activator="{ props: tooltipProps }">
              <v-list-item
                  v-bind="{ ...activatorProps, ...tooltipProps }"
                  :value="item.title"
                  rounded
                  class="mb-1"
              >
                  <!---Icon  -->
                  <template v-slot:prepend>
                    <Icon 
                      :item="item.icon" 
                      :iconName="item.iconName || (typeof item.icon === 'string' ? item.icon : null)"
                      :iconType="item.iconType || 'tabler'"
                      :level="level" 
                    />
                  </template>
                  <!---Title  -->
                  <v-list-item-title
                    class="mr-auto"
                  >
                    {{ 
                      item.pageCode && $t(`menu.${item.pageCode}`) !== `menu.${item.pageCode}` 
                        ? (() => {
                            const menuValue = $t(`menu.${item.pageCode}`);
                            // If it's an object, get title property, otherwise use the value directly
                            return typeof menuValue === 'object' && menuValue !== null && menuValue.title
                              ? menuValue.title
                              : menuValue;
                          })()
                        : (item.title ? $t(item.title) : '')
                    }}
                  </v-list-item-title>
                  <!---If Caption-->
                  <v-list-item-subtitle
                    v-if="item.subCaption"
                    class="text-caption mt-n1 hide-menu"
                  >
                    {{
                      item.pageCode 
                        ? (() => {
                            // First try to get menu.pageCode value
                            const menuValue = $t(`menu.${item.pageCode}`);
                            // If it's an object with subCaption property, use it
                            if (typeof menuValue === 'object' && menuValue !== null && menuValue.subCaption) {
                              return menuValue.subCaption;
                            }
                            // Otherwise try nested key access
                            const subCaptionValue = $t(`menu.${item.pageCode}.subCaption`);
                            if (subCaptionValue !== `menu.${item.pageCode}.subCaption`) {
                              return subCaptionValue;
                            }
                            // Fallback to item.subCaption
                            return item.subCaption;
                          })()
                        : item.subCaption
                    }}
                  </v-list-item-subtitle>
              </v-list-item>
          </template>
          <div style="white-space: pre-line; text-align: left; max-width: 300px;">
              <div class="font-weight-medium">
                {{ 
                  item.pageCode && $t(`menu.${item.pageCode}`) !== `menu.${item.pageCode}` 
                    ? (() => {
                        const menuValue = $t(`menu.${item.pageCode}`);
                        // If it's an object, get title property, otherwise use the value directly
                        return typeof menuValue === 'object' && menuValue !== null && menuValue.title
                          ? menuValue.title
                          : menuValue;
                      })()
                    : (item.title ? $t(item.title) : '')
                }}
              </div>
              <div v-if="item.subCaption" class="text-caption mt-1" style="opacity: 0.8;">
                {{
                  item.pageCode 
                    ? (() => {
                        // First try to get menu.pageCode value
                        const menuValue = $t(`menu.${item.pageCode}`);
                        // If it's an object with subCaption property, use it
                        if (typeof menuValue === 'object' && menuValue !== null && menuValue.subCaption) {
                          return menuValue.subCaption;
                        }
                        // Otherwise try nested key access
                        const subCaptionValue = $t(`menu.${item.pageCode}.subCaption`);
                        if (subCaptionValue !== `menu.${item.pageCode}.subCaption`) {
                          return subCaptionValue;
                        }
                        // Fallback to item.subCaption
                        return item.subCaption;
                      })()
                    : item.subCaption
                }}
              </div>
          </div>
      </v-tooltip>
    </template>
    <!-- ---------------------------------------------- -->
    <!---Sub Item-->
    <!-- ---------------------------------------------- -->
    <template
      v-for="(subitem, i) in item.children"
      :key="i"
      v-if="item.children"
    >
      <!-- Nested Header: Eğer subitem bir header ise -->
      <template v-if="subitem.header">
        <LcFullVerticalSidebarNavGroup :item="subitem" />
        <!-- Nested header'ın children'larını recursive olarak render et -->
        <template v-if="subitem.children && subitem.children.length > 0">
          <LcFullVerticalSidebarNavCollapse 
            v-for="(grandchild, k) in subitem.children" 
            :key="`grandchild-${i}-${k}`"
            v-if="grandchild.children && grandchild.children.length > 0"
            :item="grandchild" 
            :level="level + 1" 
          />
          <LcFullVerticalSidebarNavItem 
            v-for="(grandchild, k) in subitem.children" 
            :key="`grandchild-item-${i}-${k}`"
            v-else
            :item="grandchild" 
            :level="level + 1" 
          />
        </template>
      </template>
      <!-- Normal Item veya Collapse: Eğer subitem header değilse -->
      <template v-else>
        <LcFullVerticalSidebarNavCollapse :item="subitem" v-if="subitem.children && subitem.children.length > 0" :level="level + 1" />
        <LcFullVerticalSidebarNavItem :item="subitem" :level="level + 1" v-else></LcFullVerticalSidebarNavItem>
      </template>
    </template>
  </v-list-group>

  <!-- ---------------------------------------------- -->
  <!---End Item Sub Header -->
  <!-- ---------------------------------------------- -->
</template>
