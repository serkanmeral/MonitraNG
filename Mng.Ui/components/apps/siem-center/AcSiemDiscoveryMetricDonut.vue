<script setup lang="ts">
import { computed } from 'vue';

const props = withDefaults(
  defineProps<{
    /** Used portion 0–100 (or any units as long as used+free conceptually) */
    usedPercent: number | null;
    size?: number;
    thickness?: number;
    usedColor?: string;
    freeColor?: string;
    centerLabel?: string;
    centerCaption?: string;
  }>(),
  {
    size: 112,
    thickness: 12,
    usedColor: 'rgb(var(--v-theme-info))',
    freeColor: 'rgba(var(--v-border-color), 0.35)',
    centerLabel: '',
    centerCaption: '',
  },
);

const radius = computed(() => {
  const s = props.size;
  const t = props.thickness;
  return (s - t) / 2;
});

const circumference = computed(() => 2 * Math.PI * radius.value);

const usedClamped = computed(() => {
  const v = props.usedPercent;
  if (v == null || !Number.isFinite(v)) return null;
  return Math.max(0, Math.min(100, v));
});

const dashOffset = computed(() => {
  const pct = usedClamped.value;
  if (pct == null) return circumference.value;
  return circumference.value * (1 - pct / 100);
});

const hasValue = computed(() => usedClamped.value != null);
</script>

<template>
  <div class="metric-donut" :style="{ width: `${size}px`, height: `${size}px` }">
    <svg
      class="metric-donut__svg"
      :width="size"
      :height="size"
      :viewBox="`0 0 ${size} ${size}`"
      aria-hidden="true"
    >
      <circle
        class="metric-donut__track"
        :cx="size / 2"
        :cy="size / 2"
        :r="radius"
        fill="none"
        :stroke="freeColor"
        :stroke-width="thickness"
      />
      <circle
        v-if="hasValue"
        class="metric-donut__arc"
        :cx="size / 2"
        :cy="size / 2"
        :r="radius"
        fill="none"
        :stroke="usedColor"
        :stroke-width="thickness"
        stroke-linecap="round"
        :stroke-dasharray="circumference"
        :stroke-dashoffset="dashOffset"
        :transform="`rotate(-90 ${size / 2} ${size / 2})`"
      />
    </svg>
    <div class="metric-donut__center">
      <div class="metric-donut__label font-mono font-weight-bold">
        {{ centerLabel || (hasValue ? `${Math.round(usedClamped!)}%` : '—') }}
      </div>
      <div v-if="centerCaption" class="metric-donut__caption text-caption text-medium-emphasis">
        {{ centerCaption }}
      </div>
    </div>
  </div>
</template>

<style scoped>
.metric-donut {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}
.metric-donut__svg {
  display: block;
}
.metric-donut__center {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 8px;
  pointer-events: none;
}
.metric-donut__label {
  font-size: 1.05rem;
  line-height: 1.2;
}
.metric-donut__caption {
  line-height: 1.2;
  max-width: 72px;
}
</style>
