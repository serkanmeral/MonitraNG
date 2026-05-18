<script setup lang="ts">
import { computed } from 'vue';
import type { Widget, WidgetDataResponse } from '@/stores/apps/widget';

const props = defineProps<{
  widget: Widget;
  data?: WidgetDataResponse | null;
  t?: (key: string) => string;
}>();

// Extract value from data
const value = computed(() => {
  if (!props.data?.data || !Array.isArray(props.data.data) || props.data.data.length === 0) {
    return null;
  }

  // Try to get value from first item
  const firstItem = props.data.data[0];
  
  // Check config for value field mapping
  const config = props.widget.config as any;
  if (config?.valueField) {
    return firstItem[config.valueField];
  }

  // Default: try common field names
  return firstItem.value ?? firstItem.total ?? firstItem.count ?? firstItem.amount ?? firstItem[Object.keys(firstItem)[0]];
});

// Extract secondary value if configured
const secondaryValue = computed(() => {
  if (!cardConfig.value.showSecondaryValue || !props.data?.data || !Array.isArray(props.data.data) || props.data.data.length === 0) {
    return null;
  }

  const firstItem = props.data.data[0];
  const field = cardConfig.value.secondaryValueField;
  return firstItem[field];
});

// Extract trend/change if available
const trend = computed(() => {
  if (!props.data?.data || !Array.isArray(props.data.data) || props.data.data.length < 2) {
    return null;
  }

  const config = props.widget.config as any;
  if (config?.trendField) {
    const items = props.data.data;
    const current = items[0]?.[config.trendField];
    const previous = items[1]?.[config.trendField];
    if (current !== undefined && previous !== undefined) {
      return {
        value: current - previous,
        percentage: previous !== 0 ? ((current - previous) / previous) * 100 : 0,
      };
    }
  }
  return null;
});

// Card configuration
const cardConfig = computed(() => {
  const config = props.widget.config as any;
  return {
    showIcon: config?.showIcon !== false,
    icon: config?.icon || 'mdi-chart-line',
    iconVariant: config?.iconVariant || 'icon', // 'icon', 'avatar', 'button'
    color: config?.color || 'primary',
    trendUpColor: config?.trendUpColor || 'success',
    trendDownColor: config?.trendDownColor || 'error',
    format: config?.format || 'number', // 'number', 'currency', 'percentage', 'boolean', 'text'
    currency: config?.currency || '₺',
    decimalPlaces: config?.decimalPlaces ?? 0,
    subtitle: config?.subtitle || props.widget.description || '',
    showAction: config?.showAction !== false && config?.actionIcon,
    actionIcon: config?.actionIcon,
    actionColor: config?.actionColor || 'default',
    variant: config?.variant || 'outlined', // 'outlined', 'flat', 'elevated'
    elevation: config?.elevation ?? (config?.variant === 'elevated' ? 10 : undefined),
    bgColor: config?.bgColor,
    showSecondaryValue: config?.showSecondaryValue && config?.secondaryValueField,
    secondaryValueField: config?.secondaryValueField,
    secondaryLabel: config?.secondaryLabel || '',
    /** Kart görünümü: default | badge (kompakt, değer vurgulu) */
    cardDisplay: config?.cardDisplay === 'badge' ? 'badge' : 'default',
    /** Boolean format etiketleri (format: 'boolean' iken) */
    booleanTrueLabel: config?.booleanTrueLabel ?? 'Evet',
    booleanFalseLabel: config?.booleanFalseLabel ?? 'Hayır',
    booleanTrueColor: config?.booleanTrueColor ?? 'success',
    booleanFalseColor: config?.booleanFalseColor ?? 'error',
  };
});

// Format value
const formattedValue = computed(() => {
  const val = value.value;
  if (val === null || val === undefined) return '-';

  const cfg = cardConfig.value;

  switch (cfg.format) {
    case 'boolean': {
      const isTrue = val === true || val === 'true' || String(val).toLowerCase() === 'true' || Number(val) === 1;
      return isTrue ? cfg.booleanTrueLabel : cfg.booleanFalseLabel;
    }
    case 'text':
      return String(val);
    case 'currency':
      return new Intl.NumberFormat('tr-TR', {
        style: 'currency',
        currency: 'TRY',
        minimumFractionDigits: cfg.decimalPlaces,
        maximumFractionDigits: cfg.decimalPlaces,
      }).format(Number(val));

    case 'percentage':
      return `${Number(val).toFixed(cfg.decimalPlaces)}%`;

    case 'number':
    default:
      return new Intl.NumberFormat('tr-TR', {
        minimumFractionDigits: cfg.decimalPlaces,
        maximumFractionDigits: cfg.decimalPlaces,
      }).format(Number(val));
  }
});

// Boolean için renk ve ikon (format === 'boolean' iken)
const booleanDisplay = computed(() => {
  if (cardConfig.value.format !== 'boolean') return null;
  const val = value.value;
  const isTrue = val === true || val === 'true' || String(val).toLowerCase() === 'true' || Number(val) === 1;
  const cfg = cardConfig.value;
  return {
    color: isTrue ? cfg.booleanTrueColor : cfg.booleanFalseColor,
    icon: isTrue ? 'mdi-check-circle' : 'mdi-close-circle',
  };
});

// Trend display
const trendDisplay = computed(() => {
  const t = trend.value;
  if (!t) return null;

  const isPositive = t.value >= 0;
  const color = isPositive ? cardConfig.value.trendUpColor : cardConfig.value.trendDownColor;
  const icon = isPositive ? 'mdi-trending-up' : 'mdi-trending-down';
  const sign = isPositive ? '+' : '';

  return {
    value: t.value,
    percentage: t.percentage,
    color,
    icon,
    sign,
  };
});
</script>

<template>
  <v-card
    :variant="cardConfig.variant"
    :elevation="cardConfig.elevation"
    :class="[
      'stat-card',
      cardConfig.cardDisplay === 'badge' ? 'stat-card--badge' : '',
      `stat-card-${cardConfig.format === 'boolean' && booleanDisplay ? booleanDisplay.color : cardConfig.color}`,
      cardConfig.bgColor
    ]"
  >
    <v-card-text :class="cardConfig.cardDisplay === 'badge' ? 'pa-3' : 'pa-4'">
      <div class="d-flex align-start justify-space-between">
        <div class="d-flex align-start ga-3" style="flex: 1;">
          <!-- Boolean: durum ikonu -->
          <template v-if="cardConfig.format === 'boolean' && booleanDisplay">
            <v-icon
              :color="booleanDisplay.color"
              size="40"
              class="stat-icon stat-icon--boolean"
            >
              {{ booleanDisplay.icon }}
            </v-icon>
          </template>
          <!-- Icon Variants (non-boolean) -->
          <v-avatar
            v-else-if="cardConfig.showIcon && cardConfig.iconVariant === 'avatar'"
            :color="cardConfig.color"
            :size="cardConfig.cardDisplay === 'badge' ? 44 : 56"
            variant="flat"
            class="stat-icon-avatar"
          >
            <v-icon :color="cardConfig.color" :size="cardConfig.cardDisplay === 'badge' ? 22 : 28">
              {{ cardConfig.icon }}
            </v-icon>
          </v-avatar>
          <v-btn
            v-else-if="cardConfig.showIcon && cardConfig.iconVariant === 'button'"
            :color="cardConfig.color"
            icon
            flat
            rounded="pill"
            :size="cardConfig.cardDisplay === 'badge' ? 'small' : undefined"
            class="stat-icon-button"
          >
            <v-icon :size="cardConfig.cardDisplay === 'badge' ? 20 : 24">
              {{ cardConfig.icon }}
            </v-icon>
          </v-btn>
          <v-icon
            v-else-if="cardConfig.showIcon"
            :color="cardConfig.color"
            :size="cardConfig.cardDisplay === 'badge' ? 28 : 32"
            class="stat-icon"
          >
            {{ cardConfig.icon }}
          </v-icon>

          <div style="flex: 1;" :class="cardConfig.cardDisplay === 'badge' ? 'stat-card-badge-content' : ''">
            <div class="text-caption text-medium-emphasis mb-1" :class="{ 'text-caption': cardConfig.cardDisplay === 'badge' }">
              {{ widget.title }}
            </div>
            <div v-if="cardConfig.subtitle && cardConfig.cardDisplay !== 'badge'" class="text-caption text-medium-emphasis mb-2">
              {{ cardConfig.subtitle }}
            </div>
            <div :class="cardConfig.cardDisplay === 'badge' ? 'text-h4 font-weight-bold' : 'text-h5 font-weight-bold'">
              {{ formattedValue }}
            </div>
            <div v-if="secondaryValue !== null" class="text-body-2 text-medium-emphasis mt-1">
              <span v-if="cardConfig.secondaryLabel">{{ cardConfig.secondaryLabel }}: </span>
              {{ secondaryValue }}
            </div>
            <div v-if="trendDisplay" class="d-flex align-center ga-1 mt-1">
              <v-icon
                :color="trendDisplay.color"
                size="16"
              >
                {{ trendDisplay.icon }}
              </v-icon>
              <span
                class="text-caption font-weight-medium"
                :class="`text-${trendDisplay.color}`"
              >
                {{ trendDisplay.sign }}{{ trendDisplay.percentage.toFixed(1) }}%
              </span>
            </div>
          </div>
        </div>

        <!-- Action Button -->
        <v-btn
          v-if="cardConfig.showAction"
          :color="cardConfig.actionColor"
          icon
          flat
          rounded="pill"
          size="small"
          class="ml-2"
        >
          <v-icon size="20">
            {{ cardConfig.actionIcon }}
          </v-icon>
        </v-btn>
      </div>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.stat-card {
  height: 100%;
  transition: all 0.2s;
}

.stat-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.stat-card--badge .stat-card-badge-content {
  min-width: 0;
}

.stat-card--badge .stat-icon--boolean {
  flex-shrink: 0;
}
</style>
