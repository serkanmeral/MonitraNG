<script setup lang="ts">
import Icon from '../Icon.vue';

interface Props {
  item: {
    icon?: any;
    iconType?: 'mdi' | 'tabler';
    iconName?: string;
    title?: string;
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
</script>

<template>
    <!---Single Item-->
    <v-list-item
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
        <v-list-item-title>{{ item.title ? $t(item.title) : '' }}</v-list-item-title>
        <!---If Caption-->
        <v-list-item-subtitle v-if="item.subCaption" class="text-caption mt-n1 hide-menu">
            {{ item.subCaption }}
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
