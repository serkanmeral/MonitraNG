<script setup lang="ts">
import { computed } from 'vue';
import Icon from '../Icon.vue';

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

// Always show tooltip for menu items
const needsTooltip = computed(() => {
  return true;
});
</script>

<template>
    <!---Single Item-->
    <v-tooltip
        location="right"
    >
        <template v-slot:activator="{ props: tooltipProps }">
            <v-list-item
                v-bind="tooltipProps"
                :to="item.type === 'external' ? '' : item.to"
                :href="item.type === 'external' ? item.to : ''"
                rounded
                class="mb-1"
                :disabled="item.disabled"
                :target="item.type === 'external' ? '_blank' : ''"
                v-scroll-to="{ el: '#top' }"
            >
                <!---If icon-->
                <template v-slot:prepend>
                    <Icon 
                        :item="item.icon" 
                        :iconName="item.iconName || (typeof item.icon === 'string' ? item.icon : null)"
                        :iconType="item.iconType || 'tabler'"
                        :level="level" 
                    />
                </template>
                <v-list-item-title>
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
                <v-list-item-subtitle v-if="item.subCaption" class="text-caption mt-n1 hide-menu">
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
                <!---If any chip or label-->
                <template v-slot:append v-if="item.chip">
                    <v-chip
                        :color="item.chipColor"
                        :class="'sidebarchip hide-menu bg-' + item.chipBgColor"
                        :size="item.chipIcon ? 'small' : 'small'"
                        :variant="item.chipVariant"
                        :prepend-icon="item.chipIcon"
                    >
                        {{ item.chip }}
                    </v-chip>
                </template>
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
