<script setup lang="ts">
import { computed } from 'vue';

const props = withDefaults(
  defineProps<{
    values: number[];
    /** CSS color or currentColor */
    color?: string;
    height?: number;
    /** Soft fill under the line */
    fill?: boolean;
  }>(),
  {
    color: 'rgb(var(--v-theme-primary))',
    height: 40,
    fill: true,
  },
);

const vbW = 100;
const vbH = computed(() => props.height);

const path = computed(() => {
  const vals = props.values.filter((v) => Number.isFinite(v));
  if (vals.length < 2) return '';
  const min = Math.min(...vals);
  const max = Math.max(...vals);
  const span = max - min || 1;
  const h = vbH.value;
  const pad = 2;
  return vals
    .map((v, i) => {
      const x = (i / (vals.length - 1)) * vbW;
      const y = h - pad - ((v - min) / span) * (h - pad * 2);
      return `${i === 0 ? 'M' : 'L'}${x.toFixed(2)},${y.toFixed(2)}`;
    })
    .join(' ');
});

const areaPath = computed(() => {
  if (!props.value || !props.value) return '';
  const h = vbH.value;
  return `${path.value} L${vbW},${h} L0,${h} Z`;
});

const hasSeries = computed(() => props.values.filter((v) => Number.isFinite(v)).length >= 2);
</script>

<template>
  <div class="metric-spark" :style="{ height: `${height}px` }">
    <svg
      v-if="hasSeries"
      class="metric-spark__svg"
      :viewBox="`0 0 ${vbW} ${vbH}`"
      preserveAspectRatio="none"
      aria-hidden="true"
    >
      <path
        v-if="fill && areaPath"
        :d="areaPath"
        :fill="color"
        opacity="0.12"
      />
      <path
        :d="path"
        fill="none"
        :stroke="color"
        stroke-width="1.75"
        stroke-linecap="round"
        stroke-linejoin="round"
        vector-effect="non-scaling-stroke"
      />
    </svg>
    <div v-else class="metric-spark__empty text-caption text-medium-emphasis">—</div>
  </div>
</template>

<style scoped>
.metric-spark {
  width: 100%;
  min-height: 28px;
}
.metric-spark__svg {
  display: block;
  width: 100%;
  height: 100%;
}
.metric-spark__empty {
  display: flex;
  align-items: center;
  height: 100%;
}
</style>
