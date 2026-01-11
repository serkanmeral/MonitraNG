<script setup lang="ts">
import { DotsIcon } from 'vue-tabler-icons';

interface Props {
  item: {
    header?: string;
    pageCode?: string; // i18n key için kullanılacak unique identifier
  };
}

const props = defineProps<Props>();
</script>

<template>
        <v-list-subheader color="darkText" class="smallCap text-uppercase text-subtitle-2 mt-5 font-weight-semibold">
            <span class="mini-icon"><DotsIcon size="16" stroke-width="1.5" class="iconClass" /></span>
            <span class="mini-text">
              {{ 
                props.item.pageCode && $t(`menu.headers.${props.item.pageCode}`) !== `menu.headers.${props.item.pageCode}` 
                  ? $t(`menu.headers.${props.item.pageCode}`) 
                  : (props.item.header ? $t(props.item.header) : '')
              }}
            </span>
        </v-list-subheader>
</template>
