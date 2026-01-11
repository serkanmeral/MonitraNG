<script setup lang="ts">
import { computed } from 'vue';
import { getIcon, type IconType } from '@/utils/icons/iconUtils';

interface Props {
  item: any; // Icon component (for Tabler) or string (for icon name + type)
  level?: number;
  iconType?: IconType; // 'mdi' or 'tabler'
  iconName?: string; // Icon name (if item is not a component)
}

const props = withDefaults(defineProps<Props>(), {
  level: 0,
  iconType: 'tabler',
});

/**
 * Get icon to render
 * Supports both old format (component) and new format (iconName + iconType)
 */
const iconToRender = computed(() => {
  // If item is already a component (old format - backward compatibility)
  if (props.item && typeof props.item === 'object' && 'render' in props.item) {
    return props.item;
  }

  // New format: iconName + iconType
  const iconName = props.iconName || (typeof props.item === 'string' ? props.item : null);
  
  if (!iconName) {
    return null;
  }

  const icon = getIcon(iconName, props.iconType);
  return icon;
});

const iconSize = computed(() => props.level > 0 ? 14 : 20);
</script>

<template>
  <!-- Tabler Icon (component) -->
  <component
    v-if="iconToRender && typeof iconToRender !== 'string'"
    :is="iconToRender"
    :size="iconSize"
    stroke-width="1.5"
    class="iconClass"
  />
  
  <!-- MDI Icon (class name) -->
  <i
    v-else-if="iconToRender && typeof iconToRender === 'string'"
    :class="iconToRender"
    :style="{ fontSize: `${iconSize}px` }"
    class="iconClass"
  />
  
  <!-- Fallback: No icon -->
  <span v-else class="iconClass" :style="{ width: `${iconSize}px`, height: `${iconSize}px`, display: 'inline-block' }"></span>
</template>
