<!--
  Chart widget — Mevcut durum (8 Mart 2026):
  - Tipler: line, bar, area, pie, donut (monitoring formunda seçilebilir).
  - Multi-series: config.series ile çoklu asset; legend destekli.
  - Zaman aralığı: Widget çark menüsünden (WidgetWithSettings) seçiliyor.
  - Roadmap / sonra devam: docs/content/monitoring_plans/ROADMAP_TODAY.md (§ Chart widget),
    CHART_OPTIONS_NEXT.md, DASHBOARD_WIDGET_PLAN.md §3.4.
-->
<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useTheme } from 'vuetify';
import type { Widget, WidgetDataResponse } from '@/stores/apps/widget';
import { getPrimary, getSecondary } from '@/utils/UpdateColors';

const props = defineProps<{
  widget: Widget;
  data?: WidgetDataResponse | null;
  t?: (key: string) => string;
}>();

const theme = useTheme();

// Chart configuration interface
interface ChartConfig {
  type: 'bar' | 'line' | 'area' | 'pie' | 'donut' | 'radialBar' | 'scatter' | 'bubble';
  height?: number;
  // X-axis configuration
  xAxis?: {
    field?: string; // Field name from data for x-axis
    label?: string; // X-axis label
    categories?: string[]; // Static categories (if not using data field)
  };
  // Y-axis configuration
  yAxis?: {
    field?: string; // Field name from data for y-axis (single value)
    label?: string; // Y-axis label
    min?: number;
    max?: number;
  };
  // Series configuration (for multiple series)
  series?: Array<{
    name: string;
    field: string; // Field name from data
    type?: 'bar' | 'line' | 'area';
    color?: string;
  }>;
  // For grouped/stacked charts
  groupBy?: string; // Field to group by (e.g., "publisher", "author")
  aggregate?: {
    field: string; // Field to aggregate (e.g., "price", "pageCount")
    function: 'sum' | 'avg' | 'count' | 'min' | 'max'; // Aggregation function
  };
  // Chart options
  options?: {
    showLegend?: boolean;
    showDataLabels?: boolean;
    showGrid?: boolean;
    showToolbar?: boolean;
    stacked?: boolean;
    horizontal?: boolean;
    colors?: string[];
    sparkline?: boolean;
  };
}

// Parse config
const chartConfig = computed((): ChartConfig => {
  const config = props.widget.config as any;
  
  return {
    type: config?.type || 'line',
    height: config?.height || 350,
    xAxis: config?.xAxis || {},
    yAxis: config?.yAxis || {},
    series: config?.series || [],
    groupBy: config?.groupBy,
    aggregate: config?.aggregate,
    options: {
      showLegend: config?.options?.showLegend !== false,
      showDataLabels: config?.options?.showDataLabels || false,
      showGrid: config?.options?.showGrid !== false,
      showToolbar: config?.options?.showToolbar || false,
      stacked: config?.options?.stacked || false,
      horizontal: config?.options?.horizontal || false,
      colors: config?.options?.colors || [getPrimary.value, getSecondary.value],
      sparkline: config?.options?.sparkline || false,
    },
  };
});

// Process data for chart
const chartSeries = computed(() => {
  if (!props.data?.data || !Array.isArray(props.data.data) || props.data.data.length === 0) {
    return [];
  }

  const data = props.data.data;
  const cfg = chartConfig.value;

  // Pie and Donut charts need special format (array of numbers)
  if (cfg.type === 'pie' || cfg.type === 'donut') {
    // If yAxis field is specified, use it
    if (cfg.yAxis?.field) {
      return data.map((item: any) => {
        const value = getNestedValue(item, cfg.yAxis!.field!);
        return typeof value === 'number' ? value : 0;
      });
    }
    // If series is specified, use first series field
    if (cfg.series && cfg.series.length > 0) {
      return data.map((item: any) => {
        const value = getNestedValue(item, cfg.series![0].field);
        return typeof value === 'number' ? value : 0;
      });
    }
    // Default: use first numeric field
    const firstNumericField = findFirstNumericField(data[0]);
    if (firstNumericField) {
      return data.map((item: any) => {
        const value = getNestedValue(item, firstNumericField);
        return typeof value === 'number' ? value : 0;
      });
    }
    return [];
  }

  // Radial Bar charts need special format (array of numbers, 0-100)
  if (cfg.type === 'radialBar') {
    // If multiple series are specified, extract values from each field
    if (cfg.series && cfg.series.length > 0) {
      // For radial bar, we need array of numbers
      // If data has multiple items, take first item and extract all series values
      if (data.length > 0) {
        const firstItem = data[0];
        const values = cfg.series.map((s) => {
          const value = getNestedValue(firstItem, s.field);
          return typeof value === 'number' ? value : 0;
        });
        // Normalize to 0-100 if needed (optional - can be disabled if values are already percentages)
        const max = Math.max(...values, 1);
        // Only normalize if max > 100, otherwise assume values are already percentages
        if (max > 100) {
          return values.map((v) => Math.round((v / max) * 100));
        }
        return values;
      }
    }
    // If yAxis field is specified, normalize to 0-100
    if (cfg.yAxis?.field) {
      const values = data.map((item: any) => {
        const value = getNestedValue(item, cfg.yAxis!.field!);
        return typeof value === 'number' ? value : 0;
      });
      const max = Math.max(...values, 1);
      return values.map((v) => Math.round((v / max) * 100));
    }
    // Default: use first numeric field, normalize to 0-100
    const firstNumericField = findFirstNumericField(data[0]);
    if (firstNumericField) {
      const values = data.map((item: any) => {
        const value = getNestedValue(item, firstNumericField);
        return typeof value === 'number' ? value : 0;
      });
      const max = Math.max(...values, 1);
      return values.map((v) => Math.round((v / max) * 100));
    }
    return [];
  }

  // Simple single series chart (x-axis field + y-axis field)
  if (cfg.xAxis?.field && cfg.yAxis?.field && !cfg.groupBy && cfg.series?.length === 0) {
    return [
      {
        name: cfg.yAxis.label || cfg.yAxis.field,
        data: data.map((item: any) => getNestedValue(item, cfg.yAxis.field!)),
      },
    ];
  }

  // Multiple series chart
  if (cfg.series && cfg.series.length > 0) {
    return cfg.series.map((s) => ({
      name: s.name,
      data: data.map((item: any) => getNestedValue(item, s.field)),
      type: s.type,
      color: s.color,
    }));
  }

  // Grouped/aggregated chart
  if (cfg.groupBy && cfg.aggregate) {
    const grouped = groupAndAggregate(data, cfg.groupBy, cfg.aggregate);
    return [
      {
        name: cfg.aggregate.field,
        data: Object.values(grouped),
      },
    ];
  }

  // Default: use first numeric field as data
  const firstNumericField = findFirstNumericField(data[0]);
  if (firstNumericField) {
    return [
      {
        name: firstNumericField,
        data: data.map((item: any) => getNestedValue(item, firstNumericField)),
      },
    ];
  }

  return [];
});

// Chart categories (x-axis labels) or labels for pie/radialBar
const chartLabels = computed(() => {
  const cfg = chartConfig.value;
  const data = props.data?.data || [];

  // For pie/donut/radialBar, use labels instead of categories
  if (cfg.type === 'pie' || cfg.type === 'donut' || cfg.type === 'radialBar') {
    // Static categories from xAxis
    if (cfg.xAxis?.categories && cfg.xAxis.categories.length > 0) {
      return cfg.xAxis.categories;
    }
    // Labels from xAxis field
    if (cfg.xAxis?.field && data.length > 0) {
      return data.map((item: any) => {
        const value = getNestedValue(item, cfg.xAxis!.field!);
        if (value === null || value === undefined) return '';
        if (cfg.xAxis!.field === 'timestamp' || looksLikeIsoDate(value)) return formatChartTimestamp(value);
        return String(value);
      });
    }
    // Grouped chart labels
    if (cfg.groupBy && data.length > 0) {
      const grouped = groupData(data, cfg.groupBy);
      return Object.keys(grouped);
    }
    // Default: use index
    return data.map((_, index) => `Item ${index + 1}`);
  }

  // For other chart types, return categories for x-axis
  // Static categories
  if (cfg.xAxis?.categories && cfg.xAxis.categories.length > 0) {
    return cfg.xAxis.categories;
  }

  // Categories from data field
  if (cfg.xAxis?.field && data.length > 0) {
    return data.map((item: any) => {
      const value = getNestedValue(item, cfg.xAxis!.field!);
      if (value === null || value === undefined) return '';
      if (cfg.xAxis!.field === 'timestamp' || looksLikeIsoDate(value)) return formatChartTimestamp(value);
      return String(value);
    });
  }

  // Grouped chart categories
  if (cfg.groupBy && data.length > 0) {
    const grouped = groupData(data, cfg.groupBy);
    return Object.keys(grouped);
  }

  // Default: use index
  return data.map((_, index) => `Item ${index + 1}`);
});

// ApexCharts options
const chartOptions = computed(() => {
  const cfg = chartConfig.value;
  const isDark = theme.current.value.dark;
  const isPieOrDonut = cfg.type === 'pie' || cfg.type === 'donut';
  const isRadialBar = cfg.type === 'radialBar';

  const baseOptions: any = {
    chart: {
      type: cfg.type,
      height: cfg.height || 350,
      fontFamily: 'inherit',
      foreColor: isDark ? '#a1aab2' : '#5a6a85',
      toolbar: {
        show: cfg.options?.showToolbar || false,
      },
      sparkline: {
        enabled: cfg.options?.sparkline || false,
      },
    },
    colors: cfg.options?.colors || [getPrimary.value, getSecondary.value],
    dataLabels: {
      enabled: cfg.options?.showDataLabels || false,
    },
    legend: {
      show: cfg.options?.showLegend !== false,
      position: 'bottom',
    },
    tooltip: {
      theme: isDark ? 'dark' : 'light',
    },
  };

  // Pie and Donut specific options
  if (isPieOrDonut) {
    baseOptions.labels = chartLabels.value;
    baseOptions.plotOptions = {
      pie: {
        donut: {
          size: cfg.type === 'donut' ? '70%' : '0%',
        },
      },
    };
    baseOptions.stroke = {
      show: false,
    };
    return baseOptions;
  }

  // Radial Bar specific options
  if (isRadialBar) {
    baseOptions.labels = chartLabels.value;
    baseOptions.plotOptions = {
      radialBar: {
        dataLabels: {
          name: {
            show: true,
            fontSize: '16px',
            fontWeight: 600,
          },
          value: {
            show: true,
            fontSize: '14px',
            formatter: function (val: any) {
              return val + '%';
            },
          },
        },
      },
    };
    return baseOptions;
  }

  // Other chart types (bar, line, area, scatter, etc.)
  baseOptions.plotOptions = {
    bar: {
      horizontal: cfg.options?.horizontal || false,
      columnWidth: cfg.type === 'bar' ? '50%' : undefined,
      borderRadius: cfg.type === 'bar' ? 4 : 0,
      dataLabels: {
        position: cfg.options?.horizontal ? 'center' : 'top',
      },
    },
  };
  baseOptions.stroke = {
    show: cfg.type !== 'bar',
    width: 2,
    colors: cfg.type === 'bar' ? ['transparent'] : undefined,
  };
  baseOptions.xaxis = {
    categories: chartLabels.value,
    title: {
      text: cfg.xAxis?.label || cfg.xAxis?.field || '',
    },
    labels: {
      style: {
        colors: isDark ? '#a1aab2' : '#5a6a85',
      },
    },
  };
  baseOptions.yaxis = {
    title: {
      text: cfg.yAxis?.label || cfg.yAxis?.field || '',
    },
    min: cfg.yAxis?.min,
    max: cfg.yAxis?.max,
    labels: {
      style: {
        colors: isDark ? '#a1aab2' : '#5a6a85',
      },
    },
  };
  baseOptions.grid = {
    show: cfg.options?.showGrid !== false,
    borderColor: isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)',
  };
  baseOptions.fill = {
    opacity: cfg.type === 'area' ? 0.6 : 1,
  };

  return baseOptions;
});

/** ISO / Z formatındaki zaman değerlerini grafik etiketi için okunabilir formata çevirir */
function formatChartTimestamp(value: any): string {
  if (value === null || value === undefined) return '';
  const str = String(value).trim();
  if (!str) return '';
  const date = new Date(str);
  if (Number.isNaN(date.getTime())) return str;
  return new Intl.DateTimeFormat('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: undefined,
  }).format(date);
}

/** Değerin ISO tarih string'i olup olmadığını kontrol eder */
function looksLikeIsoDate(value: any): boolean {
  if (value === null || value === undefined) return false;
  const str = String(value).trim();
  return /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(str) || /^\d{4}-\d{2}-\d{2}/.test(str);
}

// Helper: Get nested value
function getNestedValue(item: any, key: string): any {
  if (!item || !key) return null;
  
  const keys = key.split('.');
  let value = item;
  
  for (const k of keys) {
    if (value === null || value === undefined) {
      return null;
    }
    if (value[k] !== undefined) {
      value = value[k];
    } else {
      const lowerKey = k.toLowerCase();
      const foundKey = Object.keys(value).find(
        (key) => key.toLowerCase() === lowerKey
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

// Helper: Find first numeric field
function findFirstNumericField(item: any): string | null {
  if (!item) return null;
  
  for (const key in item) {
    if (typeof item[key] === 'number' && !key.startsWith('__')) {
      return key;
    }
  }
  
  return null;
}

// Helper: Group data
function groupData(data: any[], groupBy: string): Record<string, any[]> {
  const grouped: Record<string, any[]> = {};
  
  data.forEach((item) => {
    const key = getNestedValue(item, groupBy);
    const groupKey = key !== null && key !== undefined ? String(key) : 'Unknown';
    
    if (!grouped[groupKey]) {
      grouped[groupKey] = [];
    }
    grouped[groupKey].push(item);
  });
  
  return grouped;
}

// Helper: Group and aggregate
function groupAndAggregate(
  data: any[],
  groupBy: string,
  aggregate: { field: string; function: 'sum' | 'avg' | 'count' | 'min' | 'max' }
): Record<string, number> {
  const grouped = groupData(data, groupBy);
  const result: Record<string, number> = {};
  
  Object.keys(grouped).forEach((key) => {
    const items = grouped[key];
    const values = items
      .map((item) => getNestedValue(item, aggregate.field))
      .filter((v) => typeof v === 'number');
    
    if (values.length === 0) {
      result[key] = 0;
      return;
    }
    
    switch (aggregate.function) {
      case 'sum':
        result[key] = values.reduce((a, b) => a + b, 0);
        break;
      case 'avg':
        result[key] = values.reduce((a, b) => a + b, 0) / values.length;
        break;
      case 'count':
        result[key] = values.length;
        break;
      case 'min':
        result[key] = Math.min(...values);
        break;
      case 'max':
        result[key] = Math.max(...values);
        break;
      default:
        result[key] = values[0] || 0;
    }
  });
  
  return result;
}

const lbl = (key: string) => props.t?.(`widgets.chart.${key}`) || key;
</script>

<template>
  <div class="chart-widget">
    <v-card elevation="2" class="h-100">
      <v-card-item v-if="widget.title" class="pb-2">
        <div class="text-h6">{{ widget.title }}</div>
        <div v-if="widget.description" class="text-body-2 text-medium-emphasis mt-1">
          {{ widget.description }}
        </div>
      </v-card-item>

      <v-card-text class="pt-0">
        <!-- Loading state -->
        <div v-if="!data" class="d-flex justify-center align-center" :style="{ height: `${chartConfig.height || 350}px` }">
          <v-progress-circular indeterminate color="primary" size="32" />
        </div>

        <!-- No data state -->
        <div
          v-else-if="!data.data || !Array.isArray(data.data) || data.data.length === 0"
          class="d-flex flex-column justify-center align-center text-medium-emphasis"
          :style="{ height: `${chartConfig.height || 350}px` }"
        >
          <v-icon size="48" color="grey">mdi-chart-line</v-icon>
          <div class="text-body-1 mt-2">{{ lbl('noData') }}</div>
        </div>

        <!-- Chart: key ensures re-mount when type changes (line/bar/area vs pie/donut use different series shape) -->
        <apexchart
          v-else
          :key="`${chartConfig.type}-${widget.dataId || widget.__dataId || '0'}`"
          :type="chartConfig.type"
          :height="chartConfig.height || 350"
          :options="chartOptions"
          :series="chartSeries"
        />
      </v-card-text>
    </v-card>
  </div>
</template>

<style scoped>
.chart-widget {
  width: 100%;
  height: 100%;
}
</style>
