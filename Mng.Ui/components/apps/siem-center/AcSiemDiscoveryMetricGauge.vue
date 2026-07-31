<script setup lang="ts">
import { computed } from 'vue';

const props = withDefaults(
  defineProps<{
    /** 0–100 */
    value: number | null;
    size?: number;
    thickness?: number;
    color?: string;
    trackColor?: string;
    label?: string;
    caption?: string;
  }>(),
  {
    size: 112,
    thickness: 12,
    color: 'rgb(var(--v-theme-primary))',
    trackColor: 'rgba(var(--v-border-color), 0.35)',
    label: '',
    caption: '',
  },
);

/** Semi-circle gauge: 180° arc from left to right. */
const radius = computed(() => {
  const s = props.size;
  const t = props.thickness;
  return (s - t) / 2;
});

const cx = computed(() => props.size / 2);
const cy = computed(() => props.size / 2 + props.thickness / 2);

const circumference = computed(() => Math.PI * radius.value); // half circle

const valueClamped = computed(() => {
  const v = props.value;
  if (v == null || !Number.isFinite(v)) return null;
  return Math.max(0, Math.min(100, v));
});

const dashOffset = computed(() => {
  const pct = valueClamped.value;
  if (pct == null) return circumference.value;
  return circumference.value * (1 - pct / 100);
});

const hasValue = computed(() => valueClamped.value != null);

const displayLabel = computed(() => {
  if (props.label) return props.label;
  if (valueClamped.value == null) return '—';
  const n = valueClamped.value;
  return `${n % 1 === 0 ? n.toFixed(0) : n.toFixed(1)}%`;
});
</script>

<template>
  <div
    class="metric-gauge"
    :style="{ width: `${size}px`, height: `${size / 2 + thickness + 28}px` }"
  >
    <svg
      class="metric-gauge__svg"
      :width="size"
      :height="size / 2 + thickness"
      :viewBox="`0 0 ${size} ${size / 2 + thickness}`"
      aria-hidden="true"
    >
      <path
        :d="`M ${cx - radius} ${cy} A ${radius} ${radius} 0 0 1 ${cx + radius} ${cy}`"
        fill="none"
        :stroke="trackColor"
        :stroke-width="thickness"
        stroke-linecap="round"
      />
      <path
        v-if="hasValue"
        :d="`M ${cx - radius} ${cy} A ${radius} ${radius} 0 0 1 ${cx + radius} ${cy}`"
        fill="none"
        :stroke="color"
        :stroke-width="thickness"
        stroke-linecap="round"
        :stroke-dasharray="circumference"
        :stroke-dashoffset="dashOffset"
        class="metric-gauge__arc"
      />
    </svg>
    <div class="metric-gauge__center">
      <div class="metric-gauge__label font-mono font-weight-bold">{{ displayLabel }}</div>
      <div v-if="caption" class="text-caption text-medium-emphasis">{{ caption }}</div>
    </div>
  </div>
</template>

<style scoped>
.metric-gauge {
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  flex-shrink: 0;
}
.metric-gauge__svg {
  display: block;
}
.metric-gauge__center {
  margin-top: -8px;
  text-align: center;
}
.metric-gauge__label {
  font-size: 1.15rem;
  line-height: 1.2;
}
</style>
