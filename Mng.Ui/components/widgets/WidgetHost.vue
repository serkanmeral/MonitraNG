<script setup lang="ts">
/**
 * WidgetHost — manifest-aware widget runtime (Faz 1)
 * Mevcut WidgetRenderer'ın evrimi; yüzey bağlamı (surfaceContext) destekler.
 */
import WidgetRenderer from './WidgetRenderer.vue';
import type { Widget } from '@/stores/apps/widget';
import type { SurfaceContext } from '@/types/apps/widgetManifest';

defineProps<{
  widgetId?: string;
  widget?: Widget | null;
  configOverrides?: Record<string, unknown>;
  surfaceContext?: SurfaceContext;
  t?: (key: string) => string;
}>();
</script>

<template>
  <WidgetRenderer
    :widget-id="widgetId"
    :widget="widget"
    :config-overrides="configOverrides"
    :surface-context="surfaceContext"
    :t="t"
  />
</template>
