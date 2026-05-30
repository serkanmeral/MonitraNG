<script setup lang="ts">
import { computed } from 'vue';
import type { OcCatalogDisplayItem } from '@/utils/ocCatalogDisplay';
import { catalogIconPresentation, catalogTextPresentation } from '@/utils/ocCatalogDisplay';
import { getTmStatusTablerIconComponent } from '@/utils/tmStatusTablerIcons';

const props = defineProps<{
  item: OcCatalogDisplayItem | null;
  fallback?: string;
}>();

const displayName = computed(() => props.item?.name?.trim() || props.fallback?.trim() || '—');

const textPresentation = computed(() => catalogTextPresentation(props.item?.color));
const iconPresentation = computed(() => catalogIconPresentation(props.item?.color));

// Katalog ikonları Tabler set'inden gelir; düz v-icon (mdi) bunları çözemez.
const tablerIcon = computed(() => getTmStatusTablerIconComponent(props.item?.icon));
</script>

<template>
  <span class="d-inline-flex align-center ga-1 oc-board-catalog-label">
    <span
      v-if="tablerIcon"
      class="d-inline-flex align-center"
      :class="textPresentation.className"
      :style="textPresentation.style"
    >
      <component :is="tablerIcon" :size="16" />
    </span>
    <v-icon
      v-else-if="item?.icon"
      :icon="item.icon"
      size="16"
      :color="iconPresentation.color"
      :style="iconPresentation.style"
    />
    <span :class="textPresentation.className" :style="textPresentation.style">
      {{ displayName }}
    </span>
  </span>
</template>
