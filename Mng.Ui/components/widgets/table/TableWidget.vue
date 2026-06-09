<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import type { Widget, WidgetDataResponse } from '@/stores/apps/widget';
import { useDatasetStore } from '@/stores/apps/dataset';
import { formatSeverityLabel } from '@/utils/widgets/widgetFieldMappingBridge';
import { isManifestPlaceholderDataset } from '@/utils/widgets/widgetManifestAdapter';
import {
  alarmStatusChipColor,
  formatAlarmRowSummary,
  formatAlarmStatusLabel,
  formatRelativeTimeSimple,
  formatSeverityChipLabel,
  formatSiemEventAction,
  formatSiemEventOutcome,
  formatWidgetDateTime,
  isRichTableColumnFormat,
  severityChipColor,
  siemEventOutcomeChipColor,
} from '@/utils/widgets/widgetTableFormats';
import { useLocaleStore } from '@/stores/locale';

const props = defineProps<{
  widget: Widget;
  data?: WidgetDataResponse | null;
  t?: (key: string) => string;
  interactions?: import('@/utils/widgets/surfaceInteractions').WidgetInteractions | null;
}>();

const emit = defineEmits<{
  'cross-filter': [row: Record<string, unknown>];
  'row-activate': [row: Record<string, unknown>];
  action: [action: import('@/utils/widgets/surfaceInteractions').WidgetActionConfig, row: Record<string, unknown>];
}>();

const datasetStore = useDatasetStore();
const localeStore = useLocaleStore();

// Table configuration
interface TableColumn {
  key: string;
  title: string;
  sortable?: boolean;
  filterable?: boolean;
  width?: string;
  align?: 'left' | 'center' | 'right';
  format?: 'text' | 'number' | 'currency' | 'date' | 'boolean' | 'severity' | 'severity-chip' | 'status-chip' | 'datetime-relative' | 'alarm-summary' | 'siem-event-action' | 'siem-event-outcome' | 'custom';
  formatOptions?: {
    currency?: string;
    decimalPlaces?: number;
    dateFormat?: string;
  };
  fieldType?: string; // Dataset field type (person, personGroups, etc.)
  isArray?: boolean; // Is field an array
}

interface TableConfig {
  columns: TableColumn[];
  pagination?: {
    enabled: boolean;
    itemsPerPage: number;
    itemsPerPageOptions?: number[];
  };
  sorting?: {
    enabled: boolean;
    defaultSortBy?: string;
    defaultSortOrder?: 'asc' | 'desc';
  };
  search?: {
    enabled: boolean;
    placeholder?: string;
  };
  density?: 'default' | 'comfortable' | 'compact';
  showSelect?: boolean;
  striped?: boolean;
}

// Get dataset schema to determine field types
const datasetSchema = computed(() => {
  const dataset = props.widget.dataSource?.dataset;
  if (!dataset || isManifestPlaceholderDataset(dataset)) return null;
  return datasetStore.getDatasetByName(dataset);
});

// Enrich columns with field type info from dataset schema
const enrichedColumns = computed(() => {
  const config = props.widget.config as Record<string, unknown>;
  const columns = (config?.columns as TableColumn[] | undefined) ?? [];
  const schema = datasetSchema.value;
  
  if (!schema?.fields) return columns;
  
  return columns.map((col: TableColumn) => {
    // Find field in schema by key (handle nested keys like "publisher.name")
    const fieldKey = col.key.split('.')[0]; // Get first part for field lookup
    const field = schema.fields?.find((f: any) => f.name === fieldKey);
    
    if (field) {
      return {
        ...col,
        fieldType: field.fieldType,
        isArray: field.isArray,
      };
    }
    
    return col;
  });
});

// Parse config
const tableConfig = computed((): TableConfig => {
  const config = props.widget.config as Record<string, unknown>;

  const configuredColumns = enrichedColumns.value.length
    ? enrichedColumns.value
    : (config?.columns as TableColumn[] | undefined) ?? [];

  let columns = configuredColumns;
  if (!columns.length && tableItems.value.length > 0) {
    const sample = tableItems.value[0] as Record<string, unknown>;
    columns = Object.keys(sample)
      .filter((key) => !key.startsWith('_'))
      .slice(0, 10)
      .map((key) => ({
        key,
        title: key,
        sortable: true,
      }));
  }

  const pageSize =
    (config?.pagination as { itemsPerPage?: number } | undefined)?.itemsPerPage ??
    (typeof config?.pageSize === 'number' ? config.pageSize : undefined) ??
    20;

  return {
    columns,
    pagination: {
      enabled: config?.pagination?.enabled !== false,
      itemsPerPage: pageSize,
      itemsPerPageOptions:
        (config?.pagination as { itemsPerPageOptions?: number[] } | undefined)?.itemsPerPageOptions ||
        [10, 20, 50, 100],
    },
    sorting: {
      enabled: config?.sorting?.enabled !== false,
      defaultSortBy: config?.sorting?.defaultSortBy,
      defaultSortOrder: config?.sorting?.defaultSortOrder || 'asc',
    },
    search: {
      enabled: config?.search?.enabled !== false,
      placeholder: config?.search?.placeholder || 'Ara...',
    },
    density: config?.density || (presentationStyle.value === 'inbox' ? 'comfortable' : 'comfortable'),
    showSelect: config?.showSelect || false,
    striped: config?.striped || false,
  };
});

// Table data
const tableItems = computed(() => {
  if (!props.data?.data || !Array.isArray(props.data.data)) {
    return [];
  }
  return props.data.data;
});

// Total items for pagination
const totalItems = computed(() => {
  return props.data?.total ?? tableItems.value.length;
});

const itemValueKey = computed(() => {
  const config = props.widget.config as Record<string, unknown>;
  if (typeof config.itemValue === 'string' && config.itemValue) {
    return config.itemValue;
  }
  const first = tableItems.value[0] as Record<string, unknown> | undefined;
  if (first?.__dataId != null) return '__dataId';
  if (first?.id != null) return 'id';
  return 'id';
});

const presentationStyle = computed(() => {
  const config = props.widget.config as Record<string, unknown>;
  return typeof config.presentationStyle === 'string' ? config.presentationStyle : 'default';
});

const tableHover = computed(() => {
  const config = props.widget.config as Record<string, unknown>;
  return config.hover !== false && (presentationStyle.value === 'inbox' || config.hover === true);
});

// Vuetify table headers
const headers = computed(() => {
  return tableConfig.value.columns.map((col) => ({
    title: col.title,
    key: col.key,
    sortable: col.sortable !== false && tableConfig.value.sorting?.enabled,
    align: col.align || 'left',
    width: col.width,
  }));
});

// Table options (pagination, sorting)
const tableOptions = ref({
  page: 1,
  itemsPerPage: tableConfig.value.pagination?.itemsPerPage || 20,
  sortBy: tableConfig.value.sorting?.defaultSortBy
    ? [{ key: tableConfig.value.sorting.defaultSortBy, order: tableConfig.value.sorting.defaultSortOrder || 'asc' }]
    : [],
});

// Search
const search = ref('');

// Selected items
const selectedItems = ref([]);

// Format person field value
function formatPersonValue(value: any): string {
  if (!value || typeof value !== 'object') return '-';
  
  // Try DisplayName first
  if (value.DisplayName !== undefined && value.DisplayName !== null && value.DisplayName !== '') {
    return String(value.DisplayName);
  }
  if (value.displayName !== undefined && value.displayName !== null && value.displayName !== '') {
    return String(value.displayName);
  }
  
  // Build from firstName + lastName
  const firstName = value.firstName || value.FirstName || '';
  const lastName = value.lastName || value.LastName || '';
  if (firstName || lastName) {
    return `${firstName} ${lastName}`.trim();
  }
  
  // Fallback to username, email, or __dataId
  return value.username || value.userName || value.Username || value.email || value.Email || value.__dataId || '-';
}

// Format personGroups field value
function formatPersonGroupValue(value: any): string {
  if (!value || typeof value !== 'object') return '-';
  
  // Try Name first (case-sensitive, then case-insensitive)
  if (value.Name !== undefined && value.Name !== null) {
    return String(value.Name);
  }
  if (value.name !== undefined && value.name !== null) {
    return String(value.name);
  }
  
  // Try groupName
  if (value.groupName !== undefined && value.groupName !== null) {
    return String(value.groupName);
  }
  if (value.GroupName !== undefined && value.GroupName !== null) {
    return String(value.GroupName);
  }
  
  // Fallback to other common fields
  const commonFields = ['title', 'label'];
  for (const field of commonFields) {
    if (value[field] !== undefined && value[field] !== null) {
      return String(value[field]);
    }
  }
  
  // Last fallback: __dataId
  return value.__dataId || '-';
}

function cellRawValue(item: Record<string, unknown>, column: TableColumn): unknown {
  return getNestedValue(item, column.key);
}

function formatSiemEventActionCell(item: Record<string, unknown>, column: TableColumn): string {
  return formatSiemEventAction(cellRawValue(item, column), item);
}

// Format value based on column config
function formatValue(value: any, column: TableColumn): string {
  if (value === null || value === undefined) return '-';

  // Handle arrays (isArray fields)
  if (Array.isArray(value) && column.isArray) {
    if (value.length === 0) return '-';
    
    // Format each item based on field type
    return value.map((item) => {
      if (column.fieldType === 'person' || column.fieldType === 'persons') {
        return formatPersonValue(item);
      } else if (column.fieldType === 'personGroups' || column.fieldType === 'personGroup') {
        return formatPersonGroupValue(item);
      } else if (typeof item === 'object' && item !== null) {
        // For other object arrays, try to get a display value
        return item.name || item.title || item.label || JSON.stringify(item);
      }
      return String(item);
    }).join(', ');
  }

  // Handle person field type
  if ((column.fieldType === 'person' || column.fieldType === 'persons') && typeof value === 'object' && value !== null) {
    return formatPersonValue(value);
  }

  // Handle personGroups field type
  if ((column.fieldType === 'personGroups' || column.fieldType === 'personGroup') && typeof value === 'object' && value !== null) {
    return formatPersonGroupValue(value);
  }

  // Handle regular formats
  switch (column.format) {
    case 'number':
      const num = Number(value);
      if (isNaN(num)) return String(value);
      const decimals = column.formatOptions?.decimalPlaces ?? 0;
      return new Intl.NumberFormat('tr-TR', {
        minimumFractionDigits: decimals,
        maximumFractionDigits: decimals,
      }).format(num);

    case 'currency':
      const currencyValue = Number(value);
      if (isNaN(currencyValue)) return String(value);
      const currency = column.formatOptions?.currency || '₺';
      const currencyDecimals = column.formatOptions?.decimalPlaces ?? 2;
      return new Intl.NumberFormat('tr-TR', {
        style: 'currency',
        currency: 'TRY',
        minimumFractionDigits: currencyDecimals,
        maximumFractionDigits: currencyDecimals,
      }).format(currencyValue).replace('TRY', currency);

    case 'date':
      try {
        const date = new Date(value);
        if (isNaN(date.getTime())) return String(value);
        const format = column.formatOptions?.dateFormat || 'dd.MM.yyyy HH:mm';
        const day = String(date.getDate()).padStart(2, '0');
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const year = date.getFullYear();
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');

        if (format.includes('HH:mm')) {
          return `${day}.${month}.${year} ${hours}:${minutes}`;
        }
        return `${day}.${month}.${year}`;
      } catch {
        return String(value);
      }

    case 'severity':
      return formatSeverityLabel(value);

    case 'boolean':
      return value ? 'Evet' : 'Hayır';

    case 'text':
    default:
      return String(value);
  }
}

// Get nested field value (e.g., "publisher.name")
function getNestedValue(item: any, key: string): any {
  const keys = key.split('.');
  let value = item;
  for (const k of keys) {
    if (value === null || value === undefined) return null;
    value = value[k];
  }
  return value;
}

// Filtered items (client-side search)
const filteredItems = computed(() => {
  if (!search.value || !tableConfig.value.search?.enabled) {
    return tableItems.value;
  }

  const searchLower = search.value.toLowerCase();
  return tableItems.value.filter((item) => {
    return tableConfig.value.columns.some((col) => {
      const value = getNestedValue(item, col.key);
      return String(value || '').toLowerCase().includes(searchLower);
    });
  });
});

// Loading state
const loading = computed(() => {
  // Widget data loading is handled by WidgetRenderer
  return false;
});

// Load dataset schema on mount
onMounted(async () => {
  const dataset = props.widget.dataSource?.dataset;
  if (!dataset || isManifestPlaceholderDataset(dataset) || datasetSchema.value) return;
  try {
    await datasetStore.fetchDatasetByName(dataset);
  } catch {
    if (import.meta.env.DEV) {
      console.warn('Dataset schema not found:', dataset);
    }
  }
});

function onRowClick(_event: Event, row: { item: Record<string, unknown> }) {
  emit('row-activate', row.item);
}

const lbl = (key: string) => {
  const i18nKey = `widgets.table.${key}`;
  const raw = props.t?.(i18nKey);
  if (typeof raw === 'string' && raw !== i18nKey) return raw;
  const fallbacks: Record<string, string> = {
    items: 'kayıt',
    noData: 'Veri bulunamadı',
    loading: 'Yükleniyor...',
  };
  return fallbacks[key] ?? key;
};
</script>

<template>
  <v-card
    variant="outlined"
    class="table-widget h-100"
    :class="{ 'table-widget--inbox': presentationStyle === 'inbox' }"
  >
    <v-card-title v-if="widget.title" class="pa-4 pb-2">
      <div class="d-flex align-center justify-space-between">
        <span class="text-h6">{{ widget.title }}</span>
        <v-chip v-if="tableConfig.search?.enabled" size="small" variant="tonal" color="primary">
          {{ filteredItems.length }} {{ lbl('items') || 'kayıt' }}
        </v-chip>
      </div>
    </v-card-title>

    <v-card-text class="pa-0">
      <!-- Search Bar -->
      <div v-if="tableConfig.search?.enabled" class="pa-4 pb-2">
        <v-text-field
          v-model="search"
          :placeholder="tableConfig.search.placeholder"
          prepend-inner-icon="mdi-magnify"
          variant="outlined"
          density="compact"
          hide-details
          clearable
        />
      </div>

      <!-- Data Table -->
      <v-data-table
        v-model="selectedItems"
        v-model:options="tableOptions"
        :headers="headers"
        :items="filteredItems"
        :loading="loading"
        :server-items-length="totalItems"
        :items-per-page="tableConfig.pagination?.itemsPerPage || 20"
        :items-per-page-options="tableConfig.pagination?.itemsPerPageOptions || [10, 20, 50, 100]"
        :density="tableConfig.density"
        :show-select="tableConfig.showSelect"
        :striped="tableConfig.striped"
        :hover="tableHover"
        :item-value="itemValueKey"
        class="border rounded-lg"
        :class="{
          'table-cross-filter': !!interactions?.crossFilter || !!interactions?.rowClick,
          'ac-style-table': presentationStyle === 'inbox',
        }"
        :hide-default-footer="!tableConfig.pagination?.enabled"
        @click:row="onRowClick"
      >
        <!-- Dynamic Column Slots -->
        <template
          v-for="column in tableConfig.columns"
          :key="column.key"
          #[`item.${column.key}`]="{ item }"
        >
          <div :class="column.align === 'right' ? 'text-right' : column.align === 'center' ? 'text-center' : ''">
            <template v-if="isRichTableColumnFormat(column.format)">
              <template v-if="column.format === 'severity-chip'">
                <v-chip size="small" :color="severityChipColor(cellRawValue(item, column))" variant="flat">
                  {{ formatSeverityChipLabel(cellRawValue(item, column)) }}
                </v-chip>
              </template>
              <template v-else-if="column.format === 'status-chip'">
                <v-chip size="small" :color="alarmStatusChipColor(cellRawValue(item, column))" variant="tonal">
                  {{ formatAlarmStatusLabel(cellRawValue(item, column)) }}
                </v-chip>
              </template>
              <template v-else-if="column.format === 'datetime-relative'">
                <div>
                  <div class="text-body-2">
                    {{ formatWidgetDateTime(cellRawValue(item, column), column.formatOptions) }}
                  </div>
                  <div class="text-caption text-medium-emphasis">
                    {{ formatRelativeTimeSimple(cellRawValue(item, column), localeStore.locale) }}
                  </div>
                </div>
              </template>
              <template v-else-if="column.format === 'alarm-summary'">
                <span class="text-body-2">{{ formatAlarmRowSummary(item) }}</span>
              </template>
              <template v-else-if="column.format === 'siem-event-action'">
                <v-chip size="small" color="primary" variant="tonal">
                  {{ formatSiemEventActionCell(item, column) }}
                </v-chip>
              </template>
              <template v-else-if="column.format === 'siem-event-outcome'">
                <v-chip size="small" :color="siemEventOutcomeChipColor(cellRawValue(item, column))" variant="tonal">
                  {{ formatSiemEventOutcome(cellRawValue(item, column)) }}
                </v-chip>
              </template>
            </template>
            <!-- Array fields with chips -->
            <template v-else-if="column.isArray && Array.isArray(getNestedValue(item, column.key))">
              <div class="d-flex flex-wrap ga-1">
                <v-chip
                  v-for="(arrayItem, idx) in getNestedValue(item, column.key)"
                  :key="idx"
                  size="small"
                  variant="tonal"
                  color="primary"
                >
                  {{
                    column.fieldType === 'person' || column.fieldType === 'persons'
                      ? formatPersonValue(arrayItem)
                      : column.fieldType === 'personGroups' || column.fieldType === 'personGroup'
                      ? formatPersonGroupValue(arrayItem)
                      : typeof arrayItem === 'object' && arrayItem !== null
                      ? (arrayItem.name || arrayItem.title || arrayItem.label || JSON.stringify(arrayItem))
                      : String(arrayItem)
                  }}
                </v-chip>
              </div>
            </template>
            <!-- Single value or non-array -->
            <template v-else>
              <span>{{ formatValue(getNestedValue(item, column.key), column) }}</span>
            </template>
          </div>
        </template>

        <!-- Empty State -->
        <template #no-data>
          <div class="text-center pa-8">
            <v-icon
              :icon="presentationStyle === 'inbox' ? 'mdi-bell-off-outline' : 'mdi-database-off'"
              size="48"
              color="primary"
              class="mb-2"
              :class="{ 'opacity-60': presentationStyle === 'inbox' }"
            />
            <div class="text-body-1 text-medium-emphasis">
              {{ lbl('noData') || 'Veri bulunamadı' }}
            </div>
          </div>
        </template>

        <!-- Loading State -->
        <template #loading>
          <div class="text-center pa-8">
            <v-progress-circular indeterminate color="primary" size="32" />
            <div class="text-body-2 text-medium-emphasis mt-2">
              {{ lbl('loading') || 'Yükleniyor...' }}
            </div>
          </div>
        </template>
      </v-data-table>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.table-widget {
  width: 100%;
  height: 100%;
}

.table-widget :deep(.v-data-table) {
  border: none;
}

.table-widget :deep(.table-cross-filter tbody tr) {
  cursor: pointer;
}

.table-widget--inbox :deep(.ac-style-table) {
  border-radius: 12px;
  overflow: hidden;
}

.table-widget--inbox :deep(.ac-style-table tbody tr:hover) {
  background: rgba(var(--v-theme-on-surface), 0.04);
}
</style>
