<script setup lang="ts">
import { ref, computed } from 'vue';
import type { Widget, WidgetDataResponse } from '@/stores/apps/widget';

const props = defineProps<{
  widget: Widget;
  data?: WidgetDataResponse | null;
  t?: (key: string) => string;
}>();

// Banner configuration
interface BannerConfig {
  type: 'info' | 'warning' | 'success' | 'error' | 'custom';
  variant?: 'tonal' | 'filled' | 'outlined' | 'flat';
  title?: string; // Static title
  titleField?: string; // Data field for title
  content?: string; // Static content
  contentField?: string; // Data field for content
  icon?: string; // Material Design Icon
  image?: string; // Image URL
  showIcon?: boolean;
  showImage?: boolean;
  dismissible?: boolean;
  action?: {
    enabled?: boolean;
    label?: string;
    icon?: string;
    color?: string;
    onClick?: string; // Event handler name
  };
  customColor?: string; // Custom type için
}

// Parse config
const bannerConfig = computed((): BannerConfig => {
  const config = props.widget.config as any;
  
  return {
    type: config?.type || 'info',
    variant: config?.variant || 'tonal',
    title: config?.title,
    titleField: config?.titleField,
    content: config?.content,
    contentField: config?.contentField,
    icon: config?.icon,
    image: config?.image,
    showIcon: config?.showIcon !== false,
    showImage: config?.showImage || false,
    dismissible: config?.dismissible || false,
    action: {
      enabled: config?.action?.enabled || false,
      label: config?.action?.label || 'Action',
      icon: config?.action?.icon,
      color: config?.action?.color || 'primary',
      onClick: config?.action?.onClick,
    },
    customColor: config?.customColor,
  };
});

// Get data item (first item from data array)
const dataItem = computed(() => {
  if (!props.data?.data || !Array.isArray(props.data.data) || props.data.data.length === 0) {
    // Data yoksa null döndür (banner gösterilmez veya static content gösterilir)
    return null;
  }
  return props.data.data[0];
});


// Replace template placeholders with data values
function replaceTemplate(template: string, data: any): string {
  if (!template || !data) {
    return template;
  }
  
  // Match {fieldName} or {field.name} patterns
  let result = template;
  const matches = template.match(/\{([^}]+)\}/g);
  
  if (!matches || matches.length === 0) {
    return template;
  }
  
  matches.forEach((match) => {
    // Extract field path from {fieldName} or {field.name}
    const fieldPath = match.replace(/[{}]/g, '').trim();
    const value = getNestedValue(data, fieldPath);
    
    // If value found, replace ALL occurrences of this placeholder
    if (value !== null && value !== undefined && value !== '') {
      // Replace all occurrences using global regex
      const regex = new RegExp(match.replace(/[{}]/g, '\\$&'), 'g');
      result = result.replace(regex, String(value));
    }
    // If field not found, placeholder remains (for debugging)
  });
  
  return result;
}

// Get content (static or from data with template support)
const content = computed(() => {
  const cfg = bannerConfig.value;
  
  // Priority 1: If contentField is specified and data exists, use that field directly (no template)
  if (cfg.contentField && dataItem.value) {
    const value = getNestedValue(dataItem.value, cfg.contentField);
    if (value !== null && value !== undefined) {
      return String(value);
    }
  }
  
  // Priority 2: Get static content
  const staticContent = cfg.content || props.widget.description || '';
  
  // Priority 3: If static content contains template placeholders and data exists, replace them
  if (staticContent && dataItem.value && staticContent.includes('{')) {
    return replaceTemplate(staticContent, dataItem.value);
  }
  
  return staticContent;
});

// Get title with template support
const title = computed(() => {
  const cfg = bannerConfig.value;
  
  // Priority 1: If titleField is specified and data exists, use that field directly (no template)
  if (cfg.titleField && dataItem.value) {
    const value = getNestedValue(dataItem.value, cfg.titleField);
    if (value !== null && value !== undefined) {
      return String(value);
    }
  }
  
  // Priority 2: Get static title
  const staticTitle = cfg.title || props.widget.title || '';
  
  // Priority 3: If static title contains template placeholders and data exists, replace them
  if (staticTitle && dataItem.value && staticTitle.includes('{')) {
    return replaceTemplate(staticTitle, dataItem.value);
  }
  
  return staticTitle;
});

// Get nested field value (e.g., "publisher.name")
function getNestedValue(item: any, key: string): any {
  if (!item || !key) return null;
  
  const keys = key.split('.');
  let value = item;
  
  for (const k of keys) {
    if (value === null || value === undefined) {
      return null;
    }
    // Try exact key first
    if (value[k] !== undefined) {
      value = value[k];
    } else {
      // Try case variations
      const lowerKey = k.toLowerCase();
      const upperKey = k.charAt(0).toUpperCase() + k.slice(1);
      
      // Check all possible case variations
      const foundKey = Object.keys(value).find(
        (key) => key.toLowerCase() === lowerKey || key === upperKey || key === k
      );
      
      if (foundKey) {
        value = value[foundKey];
      } else {
        return null;
      }
    }
  }
  
  return value;
}

// Dismissible state
const isDismissed = ref(false);

// Handle dismiss
function handleDismiss() {
  isDismissed.value = true;
}

// Handle action click
function handleActionClick() {
  const onClick = bannerConfig.value.action?.onClick;
  if (onClick) {
    // Emit event to parent (dashboard can handle it)
    // For now, just log - can be extended with event system
    console.log('Banner action clicked:', onClick, dataItem.value);
  }
}

// Banner color based on type
const bannerColor = computed(() => {
  const cfg = bannerConfig.value;
  if (cfg.type === 'custom' && cfg.customColor) {
    return cfg.customColor;
  }
  return cfg.type;
});

// Default icon based on type
const defaultIcon = computed(() => {
  const cfg = bannerConfig.value;
  if (cfg.icon) return cfg.icon;
  
  const icons: Record<string, string> = {
    info: 'mdi-information',
    warning: 'mdi-alert',
    success: 'mdi-check-circle',
    error: 'mdi-alert-circle',
    custom: 'mdi-bell',
  };
  return icons[cfg.type] || 'mdi-information';
});

const lbl = (key: string) => props.t?.(`widgets.banner.${key}`) || key;
</script>

<template>
  <div v-if="!isDismissed" class="banner-widget">
    <!-- Show banner even if no data (for static banners) -->
    <!-- v-alert based banner (for info, warning, success, error) -->
    <v-alert
      v-if="bannerConfig.type !== 'custom' || !bannerConfig.showImage"
      :type="bannerConfig.type"
      :variant="bannerConfig.variant"
      :color="bannerColor"
      :closable="bannerConfig.dismissible"
      @click:close="handleDismiss"
      class="mb-0"
    >
      <template v-if="bannerConfig.showIcon" #prepend>
        <v-icon>{{ defaultIcon }}</v-icon>
      </template>

      <div>
        <div v-if="title" class="text-h6 mb-2">{{ title }}</div>
        <div v-if="content" class="text-body-2">{{ content }}</div>
      </div>

      <template v-if="bannerConfig.action?.enabled" #append>
        <v-btn
          :color="bannerConfig.action.color"
          variant="text"
          size="small"
          @click="handleActionClick"
        >
          <v-icon v-if="bannerConfig.action.icon" start size="18">
            {{ bannerConfig.action.icon }}
          </v-icon>
          {{ bannerConfig.action.label }}
        </v-btn>
      </template>
    </v-alert>

    <!-- Custom card-based banner (for image banners) -->
    <v-card
      v-else
      :variant="bannerConfig.variant || 'flat'"
      :color="bannerColor"
      :class="[
        'banner-card',
        bannerConfig.variant === 'tonal' ? `bg-${bannerColor}-lighten-5` : '',
        bannerConfig.variant === 'filled' ? `bg-${bannerColor}` : '',
      ]"
      elevation="0"
      rounded="md"
    >
      <v-card-item class="py-0">
        <v-row class="d-flex align-center">
          <!-- Content Column -->
          <v-col cols="12" :sm="bannerConfig.showImage ? 7 : 12" class="pa-6">
            <div v-if="bannerConfig.showIcon && !bannerConfig.showImage" class="mb-3">
              <v-icon :color="bannerColor" size="32">{{ defaultIcon }}</v-icon>
            </div>
            <h5 v-if="title" class="text-h5 pt-3">{{ title }}</h5>
            <h6 v-if="content" class="text-subtitle-1 text-13 py-4 text-medium-emphasis">
              {{ content }}
            </h6>
            <v-btn
              v-if="bannerConfig.action?.enabled"
              :color="bannerConfig.action.color || bannerColor"
              variant="flat"
              @click="handleActionClick"
            >
              <v-icon v-if="bannerConfig.action.icon" start size="18">
                {{ bannerConfig.action.icon }}
              </v-icon>
              {{ bannerConfig.action.label }}
            </v-btn>
            <v-btn
              v-if="bannerConfig.dismissible"
              icon
              variant="text"
              size="small"
              class="ml-2"
              @click="handleDismiss"
            >
              <v-icon>mdi-close</v-icon>
            </v-btn>
          </v-col>

          <!-- Image Column -->
          <v-col v-if="bannerConfig.showImage && bannerConfig.image" cols="12" sm="5">
            <div class="text-center pa-4">
              <img
                :src="bannerConfig.image"
                :alt="title || 'Banner image'"
                class="banner-image"
                style="max-width: 100%; height: auto;"
              />
            </div>
          </v-col>
        </v-row>
      </v-card-item>
    </v-card>
  </div>
</template>

<style scoped>
.banner-widget {
  width: 100%;
}

.banner-card {
  border: 1px solid rgba(var(--v-border-opacity), var(--v-border-opacity));
}

.banner-image {
  max-width: 100%;
  height: auto;
  object-fit: contain;
}
</style>
