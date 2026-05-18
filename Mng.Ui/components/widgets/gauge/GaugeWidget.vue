<script setup lang="ts">
import { computed } from 'vue';
import { useTheme } from 'vuetify';
import { getPrimary } from '@/utils/UpdateColors';
import type { Widget, WidgetDataResponse } from '@/stores/apps/widget';

const props = defineProps<{
  widget: Widget;
  data?: WidgetDataResponse | null;
  t?: (key: string) => string;
}>();

const theme = useTheme();
const config = computed(() => (props.widget.config || {}) as Record<string, any>);

/** Tema rengini çözümle (success/warning/error/primary) — SVG stroke için hex gerekli, light/dark uyumlu */
const themeColors = computed(() => {
  const c = theme.current.value?.colors || {};
  return {
    primary: c.primary ?? getPrimary.value ?? '#1e88e5',
    success: c.success ?? '#13DEB9',
    warning: c.warning ?? '#FFAE1F',
    error: c.error ?? '#FA896B',
  };
});

const value = computed(() => {
  if (!props.data?.data || !Array.isArray(props.data.data) || props.data.data.length === 0) return null;
  const first = props.data.data[0];
  const field = config.value.valueField ?? 'value';
  const v = first[field];
  if (v === null || v === undefined) return null;
  const num = Number(v);
  return Number.isNaN(num) ? null : num;
});

const min = computed(() => {
  const v = config.value.min;
  return typeof v === 'number' && !Number.isNaN(v) ? v : 0;
});

const max = computed(() => {
  const v = config.value.max;
  return typeof v === 'number' && !Number.isNaN(v) ? v : 100;
});

const unit = computed(() => config.value.unit ?? '');

/** 0-100 arası doluluk (min-max'e göre) */
const percent = computed(() => {
  const val = value.value;
  if (val === null) return 0;
  const range = max.value - min.value;
  if (range <= 0) return 0;
  const p = ((val - min.value) / range) * 100;
  return Math.max(0, Math.min(100, p));
});

/** Thresholds: [{ from, to, color }] — değere göre renk (color: 'success'|'warning'|'error' veya hex) */
const thresholds = computed(() => {
  const t = config.value.thresholds;
  if (!Array.isArray(t) || t.length === 0) return [];
  return t
    .filter((x: any) => x && typeof x.from === 'number' && typeof x.to === 'number' && x.color)
    .map((x: any) => ({ from: x.from, to: x.to, color: x.color }));
});

const gaugeColor = computed(() => {
  const val = value.value;
  const colors = themeColors.value;
  const resolve = (color: string): string => {
    if (!color) return colors.primary;
    if (color === 'success' || color === 'warning' || color === 'error' || color === 'primary') return colors[color];
    const m = String(color).match(/var\(--v-theme-(\w+)\)/);
    if (m && (m[1] === 'success' || m[1] === 'warning' || m[1] === 'error' || m[1] === 'primary')) return colors[m[1]];
    return color;
  };
  if (val === null) return colors.primary;
  for (const t of thresholds.value) {
    if (val >= t.from && val <= t.to) return resolve(t.color);
  }
  return resolve(config.value.color) || colors.primary;
});

/** SVG yarım daire: merkez (1,1), alt yarım. Arc 0% = sol, 100% = sağ. */
const viewBox = '0 0 2 1.5';
const cx = 1;
const cy = 1;
const rOuter = 0.9;
const rInner = 0.72;
const bandThickness = rOuter - rInner;

function pointOnCircle(radius: number, angleRad: number): [number, number] {
  return [cx + radius * Math.cos(angleRad), cy + radius * Math.sin(angleRad)];
}

/** Arka plan: tek stroked yay (dış yarıçap) */
function describeBackgroundArc(): string {
  const start = pointOnCircle(rOuter, Math.PI);
  const end = pointOnCircle(rOuter, 0);
  return `M ${start[0]} ${start[1]} A ${rOuter} ${rOuter} 0 0 0 ${end[0]} ${end[1]}`;
}

/** Değer: dolu bant (iç yay → dış yay kapalı path), stroke yok — tek parça, cap yok */
function describeValueBand(percentVal: number): string {
  if (percentVal <= 0) return '';
  const p = Math.min(100, percentVal) / 100;
  const endAngle = Math.PI * (1 - p);
  const startAngle = Math.PI;
  const innerStart = pointOnCircle(rInner, startAngle);
  const innerEnd = pointOnCircle(rInner, endAngle);
  const outerEnd = pointOnCircle(rOuter, endAngle);
  const outerStart = pointOnCircle(rOuter, startAngle);
  const large = p >= 0.5 ? 1 : 0;
  return `M ${innerStart[0]} ${innerStart[1]} A ${rInner} ${rInner} 0 ${large} 0 ${innerEnd[0]} ${innerEnd[1]} L ${outerEnd[0]} ${outerEnd[1]} A ${rOuter} ${rOuter} 0 ${large} 1 ${outerStart[0]} ${outerStart[1]} Z`;
}

const pathBackground = describeBackgroundArc();
const pathValueBand = computed(() => describeValueBand(percent.value));

const displayValue = computed(() => {
  const val = value.value;
  if (val === null) return '–';
  const decimals = config.value.decimalPlaces ?? 1;
  return Number(val).toFixed(decimals);
});
</script>

<template>
  <div class="gauge-widget">
    <v-card variant="outlined" class="gauge-card">
      <v-card-text class="pa-3">
        <div class="text-caption text-medium-emphasis mb-1">{{ widget.title }}</div>
        <div class="gauge-svg-wrap">
          <svg :viewBox="viewBox" class="gauge-svg" preserveAspectRatio="xMidYMid meet">
            <path
              class="gauge-bg"
              :d="pathBackground"
              fill="none"
              stroke="currentColor"
              stroke-width="0.12"
              stroke-linecap="round"
            />
            <path
              v-if="pathValueBand"
              class="gauge-value-band"
              :d="pathValueBand"
              :fill="gaugeColor"
            />
          </svg>
          <div class="gauge-center">
            <span class="gauge-number">{{ displayValue }}</span>
            <span v-if="unit" class="gauge-unit">{{ unit }}</span>
          </div>
        </div>
        <div class="d-flex justify-space-between text-caption text-medium-emphasis mt-1 px-1">
          <span>{{ min }}</span>
          <span>{{ max }}</span>
        </div>
      </v-card-text>
    </v-card>
  </div>
</template>

<style scoped>
.gauge-widget {
  width: 100%;
  height: 100%;
}

.gauge-card {
  height: 100%;
}

.gauge-svg-wrap {
  position: relative;
  width: 100%;
  max-width: 240px;
  margin: 0 auto;
  aspect-ratio: 2 / 1.5;
}

.gauge-svg {
  width: 100%;
  height: 100%;
  /* Arka plan yayı: light'ta koyu gri, dark'ta açık gri */
  color: rgba(0, 0, 0, 0.15);
}

.v-theme--dark .gauge-svg {
  color: rgba(255, 255, 255, 0.18);
}

.gauge-value-band {
  transition: d 0.3s ease, fill 0.25s ease;
}

.gauge-center {
  position: absolute;
  left: 50%;
  bottom: 8%;
  transform: translateX(-50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  pointer-events: none;
}

.gauge-number {
  font-size: 1.5rem;
  font-weight: 700;
  line-height: 1.2;
}

.gauge-unit {
  font-size: 0.75rem;
  font-weight: 500;
  opacity: 0.85;
}
</style>
