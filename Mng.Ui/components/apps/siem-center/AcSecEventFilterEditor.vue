<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type {
  SecEventFilterFieldClause,
  SecEventFilterFieldKey,
  SecEventFilterFieldOp,
  SecEventSavedFilter,
} from '@/types/apps/secEventFilterCatalog';
import {
  SEC_EVENT_SOURCE_PRODUCT_OPTIONS,
  SEC_EVENT_SOURCE_TYPE_OPTIONS,
} from '@/types/apps/secEventFilterCatalog';
import type { SecEventTargetFieldDefinition } from '@/types/apps/secEventParseRules';
import type { SecEventParseRuleManageItem } from '@/types/apps/secEventParseRules';
import {
  buildSecEventFilterFieldSchemasFromCatalog,
  collectParseExtractFieldsForProduct,
  createFallbackSecEventFilterFieldSchemas,
  type SecEventFilterFieldSchema,
} from '@/utils/secEventFilterFieldSchema';
import {
  fetchSecEventParseRulePublished,
  fetchSecEventTargetFields,
} from '@/services/secEventParseRuleCatalogService';
import { fetchEventLogPackageCatalog } from '@/services/eventLogPackageCatalogService';
import { secEventScopeOptions } from '@/services/secEventService';
import { sourceTypeLabelKey } from '@/composables/useSecEventList';

const props = defineProps<{
  modelValue: SecEventSavedFilter;
  hostOptions: string[];
  dirty: boolean;
  selectedFilterId: string | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: SecEventSavedFilter];
  apply: [];
  save: [];
  saveAs: [];
  clear: [];
}>();

const { t } = useAppI18n();

const draft = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v),
});

const targetFields = ref<SecEventTargetFieldDefinition[] | null>(null);
const publishedRules = ref<SecEventParseRuleManageItem[]>([]);
const liveTypes = ref<string[]>([]);
const liveProducts = ref<string[]>([]);
const liveHosts = ref<string[]>([]);
const packageProducts = ref<string[]>([]);
const catalogLoading = ref(false);
const showAdvancedType = ref(false);

const STATIC_TYPES = [...SEC_EVENT_SOURCE_TYPE_OPTIONS];
const STATIC_PRODUCTS = [...SEC_EVENT_SOURCE_PRODUCT_OPTIONS];

function mergeUnique(...lists: string[][]): string[] {
  const set = new Set<string>();
  for (const list of lists) {
    for (const raw of list) {
      const v = String(raw ?? '').trim();
      if (v) set.add(v);
    }
  }
  return Array.from(set).sort((a, b) => a.localeCompare(b));
}

function typeTitle(value: string | null): string {
  if (!value) return t('siemCenter.events.filterCatalog.all');
  const key = sourceTypeLabelKey(value);
  const translated = t(key);
  return translated !== key ? translated : value;
}

function productTitle(value: string | null): string {
  if (!value) return t('siemCenter.events.filterCatalog.all');
  const i18nKey = `siemCenter.events.filterCatalog.products.${value.replace(/-/g, '_')}`;
  const translated = t(i18nKey);
  return translated !== i18nKey ? translated : value;
}

const typeItems = computed(() => {
  const values = mergeUnique(liveTypes.value, STATIC_TYPES, draft.value.scope?.type ? [draft.value.scope.type] : []);
  return values.map((v) => ({ title: typeTitle(v), value: v }));
});

const productItems = computed(() => {
  const values = mergeUnique(
    liveProducts.value,
    packageProducts.value,
    STATIC_PRODUCTS,
    draft.value.scope?.product ? [draft.value.scope.product] : [],
  );
  return values.map((v) => ({ title: productTitle(v), value: v }));
});

const hostItems = computed(() =>
  mergeUnique(props.hostOptions, liveHosts.value, draft.value.scope?.hosts ?? []).map((h) => ({
    title: h,
    value: h,
  })),
);

const allowedFieldsForProduct = computed(() =>
  collectParseExtractFieldsForProduct(publishedRules.value, draft.value.scope?.product),
);

const resolvedSchemas = computed((): SecEventFilterFieldSchema[] => {
  const fields = targetFields.value?.length
    ? targetFields.value
    : createFallbackSecEventFilterFieldSchemas()
        .filter((s) => s.field !== 'event.actionPrefix' && s.field !== 'search')
        .map((s) => ({
          name: s.field,
          label: s.label,
          group: s.group ?? '',
          valueType: 'keyword',
          extractTypes: [] as string[],
          queryOperators: s.ops as string[],
          queryable: true,
          wizardSelectable: true,
          isCustom: !!s.isCustom,
        }));

  return buildSecEventFilterFieldSchemasFromCatalog(fields, {
    product: draft.value.scope?.product,
    allowedFields: allowedFieldsForProduct.value,
  });
});

const addFieldItems = computed(() =>
  resolvedSchemas.value
    .filter((s) => !draft.value.fields.some((f) => f.field === s.field))
    .map((s) => ({
      title: fieldLabel(s),
      value: s.field,
    })),
);

const badgeLabel = computed(() => {
  if (!props.selectedFilterId) {
    return props.dirty
      ? t('siemCenter.events.filterCatalog.unsavedChanges')
      : t('siemCenter.events.filterCatalog.noFilterSelected');
  }
  if (props.dirty) return t('siemCenter.events.filterCatalog.unsavedChanges');
  return draft.value.name || t('siemCenter.events.filterCatalog.activeFilter');
});

const hasTypeSelected = computed(() => !!draft.value.scope?.type?.trim());

function fieldLabel(schema: SecEventFilterFieldSchema | undefined, fallback?: string): string {
  if (!schema) return fallback ?? '';
  if (schema.labelKey) {
    const translated = t(schema.labelKey);
    if (translated !== schema.labelKey) return translated;
  }
  return schema.label || schema.field;
}

function patchScope(partial: Partial<SecEventSavedFilter['scope']>) {
  draft.value = {
    ...draft.value,
    scope: { ...draft.value.scope, ...partial },
  };
}

function patchField(index: number, partial: Partial<SecEventFilterFieldClause>) {
  const fields = draft.value.fields.map((f, i) => (i === index ? { ...f, ...partial } : f));
  draft.value = { ...draft.value, fields };
}

function removeField(index: number) {
  draft.value = {
    ...draft.value,
    fields: draft.value.fields.filter((_, i) => i !== index),
  };
}

function addField(field: SecEventFilterFieldKey) {
  const schema = resolvedSchemas.value.find((s) => s.field === field);
  if (!schema) return;
  const op: SecEventFilterFieldOp = schema.ops[0] ?? 'eq';
  draft.value = {
    ...draft.value,
    fields: [...draft.value.fields, { field, op, value: '' }],
  };
}

function schemaFor(field: SecEventFilterFieldKey) {
  return resolvedSchemas.value.find((s) => s.field === field);
}

function opItems(field: SecEventFilterFieldKey) {
  const schema = schemaFor(field);
  const ops = schema?.ops ?? ['eq'];
  return ops.map((op) => ({
    title: t(`siemCenter.events.filterCatalog.ops.${op}`),
    value: op,
  }));
}

function coerceComboValue(value: unknown): string | null {
  if (value == null || value === '') return null;
  if (typeof value === 'string') return value.trim() || null;
  if (typeof value === 'object' && value !== null && 'value' in (value as object)) {
    const inner = (value as { value: unknown }).value;
    if (inner == null || inner === '') return null;
    return String(inner).trim() || null;
  }
  return String(value).trim() || null;
}

function onTypeUpdate(value: unknown) {
  patchScope({ type: coerceComboValue(value) });
}

function onProductUpdate(value: unknown) {
  patchScope({ product: coerceComboValue(value) });
}

function onHostsUpdate(value: unknown) {
  const raw = Array.isArray(value) ? value : [];
  const hosts = raw
    .map((x) => coerceComboValue(x))
    .filter((x): x is string => !!x);
  patchScope({ hosts });
}

function onOpUpdate(index: number, value: unknown) {
  if (typeof value === 'string') patchField(index, { op: value as SecEventFilterFieldOp });
}

function onSelectValueUpdate(index: number, value: unknown) {
  if (Array.isArray(value)) {
    patchField(index, { value: value.map(String).join(',') });
  } else {
    patchField(index, { value: value == null ? '' : String(value) });
  }
}

function onTextValueUpdate(index: number, value: unknown) {
  patchField(index, { value: value == null ? '' : String(value) });
}

function selectModelValue(clause: SecEventFilterFieldClause): string | string[] {
  if (clause.op === 'in') {
    return clause.value.split(',').map((s) => s.trim()).filter(Boolean);
  }
  return clause.value;
}

function fieldSelectOptions(field: SecEventFilterFieldKey): string[] {
  return schemaFor(field)?.options ?? [];
}

function isSelectField(field: SecEventFilterFieldKey): boolean {
  const schema = schemaFor(field);
  return schema?.input === 'select' && !!schema.options?.length;
}

async function loadScopeAndCatalog() {
  catalogLoading.value = true;
  try {
    const [targets, scope, packages, published] = await Promise.all([
      fetchSecEventTargetFields().catch(() => null),
      secEventScopeOptions({ rangeHours: 168 }).catch(() => null),
      fetchEventLogPackageCatalog().catch(() => null),
      fetchSecEventParseRulePublished().catch(() => null),
    ]);

    if (targets?.fields?.length) {
      targetFields.value = targets.fields;
    } else {
      targetFields.value = createFallbackSecEventFilterFieldSchemas()
        .filter((s) => s.field !== 'event.actionPrefix' && s.field !== 'search')
        .map((s) => ({
          name: s.field,
          label: s.label,
          group: s.group ?? '',
          valueType: 'keyword',
          extractTypes: [],
          queryOperators: s.ops,
          queryable: true,
          wizardSelectable: true,
          isCustom: !!s.isCustom,
        }));
    }

    if (scope) {
      liveTypes.value = scope.types;
      liveProducts.value = scope.products;
      liveHosts.value = scope.hosts;
    }

    if (packages) {
      const names = [
        ...(packages.packages ?? []).map((p) => p.name),
        ...(packages.optionalPackages ?? []).map((p) => p.name),
      ];
      packageProducts.value = names.filter(Boolean);
    }

    publishedRules.value = published?.rules ?? [];
  } finally {
    catalogLoading.value = false;
  }
}

onMounted(() => {
  if (hasTypeSelected.value) showAdvancedType.value = true;
  void loadScopeAndCatalog();
});

watch(hasTypeSelected, (v) => {
  if (v) showAdvancedType.value = true;
});
</script>

<template>
  <div class="sec-filter-editor">
    <div class="d-flex align-center flex-wrap ga-2 mb-3">
      <v-chip size="small" variant="tonal" color="primary">{{ badgeLabel }}</v-chip>
      <v-spacer />
      <v-btn size="small" color="primary" class="text-none" @click="emit('apply')">
        {{ t('siemCenter.events.filterCatalog.apply') }}
      </v-btn>
      <v-btn size="small" variant="tonal" class="text-none" @click="emit('save')">
        {{ t('siemCenter.events.filterCatalog.save') }}
      </v-btn>
      <v-btn size="small" variant="text" class="text-none" @click="emit('saveAs')">
        {{ t('siemCenter.events.filterCatalog.saveAs') }}
      </v-btn>
      <v-btn size="small" variant="text" class="text-none" @click="emit('clear')">
        {{ t('siemCenter.events.filterCatalog.clear') }}
      </v-btn>
    </div>

    <div class="text-caption text-medium-emphasis mb-1">
      {{ t('siemCenter.events.filterCatalog.scope') }}
      <span class="ms-1">{{ t('siemCenter.events.filterCatalog.scopeHint') }}</span>
    </div>
    <v-row dense class="mb-2">
      <v-col cols="12" md="6">
        <v-combobox
          :model-value="draft.scope.product ?? null"
          :items="productItems"
          item-title="title"
          item-value="value"
          :label="t('siemCenter.events.filterCatalog.product')"
          :hint="t('siemCenter.events.filterCatalog.productHint')"
          persistent-hint
          density="compact"
          variant="outlined"
          clearable
          :loading="catalogLoading"
          @update:model-value="onProductUpdate"
        />
      </v-col>
      <v-col cols="12" md="6">
        <v-combobox
          :model-value="draft.scope.hosts ?? []"
          :items="hostItems"
          item-title="title"
          item-value="value"
          :label="t('siemCenter.events.filterCatalog.host')"
          :hint="t('siemCenter.events.filterCatalog.hostHint')"
          persistent-hint
          density="compact"
          variant="outlined"
          clearable
          multiple
          chips
          closable-chips
          :loading="catalogLoading"
          @update:model-value="onHostsUpdate"
        />
      </v-col>
    </v-row>

    <div class="d-flex align-center flex-wrap ga-2 mb-2">
      <v-btn
        size="x-small"
        variant="text"
        class="text-none px-1"
        :prepend-icon="showAdvancedType ? 'mdi-chevron-up' : 'mdi-chevron-down'"
        @click="showAdvancedType = !showAdvancedType"
      >
        {{ t('siemCenter.events.filterCatalog.advancedScope') }}
      </v-btn>
      <v-chip v-if="hasTypeSelected" size="x-small" variant="tonal" closable @click:close="onTypeUpdate(null)">
        {{ typeTitle(draft.scope.type ?? null) }}
      </v-chip>
    </div>
    <v-expand-transition>
      <div v-show="showAdvancedType" class="mb-3">
        <v-combobox
          :model-value="draft.scope.type ?? null"
          :items="typeItems"
          item-title="title"
          item-value="value"
          :label="t('siemCenter.events.filterCatalog.type')"
          :hint="t('siemCenter.events.filterCatalog.typeHint')"
          persistent-hint
          density="compact"
          variant="outlined"
          clearable
          @update:model-value="onTypeUpdate"
        />
      </div>
    </v-expand-transition>

    <div class="d-flex align-center mb-1">
      <span class="text-caption text-medium-emphasis">
        {{ t('siemCenter.events.filterCatalog.fieldFilters') }}
        <span v-if="catalogLoading" class="ms-1">(…)</span>
      </span>
      <v-spacer />
      <v-menu v-if="addFieldItems.length">
        <template #activator="{ props: menuProps }">
          <v-btn
            v-bind="menuProps"
            size="x-small"
            variant="tonal"
            class="text-none"
            prepend-icon="mdi-plus"
          >
            {{ t('siemCenter.events.filterCatalog.addField') }}
          </v-btn>
        </template>
        <v-list density="compact" max-height="320">
          <v-list-item
            v-for="item in addFieldItems"
            :key="item.value"
            :title="item.title"
            :subtitle="item.value"
            @click="addField(item.value)"
          />
        </v-list>
      </v-menu>
    </div>

    <div v-if="!draft.fields.length" class="text-caption text-medium-emphasis mb-2">
      {{ t('siemCenter.events.filterCatalog.noFieldFilters') }}
    </div>

    <div
      v-for="(clause, index) in draft.fields"
      :key="`${clause.field}-${index}`"
      class="d-flex flex-wrap align-center ga-2 mb-2"
    >
      <v-chip size="small" variant="outlined" :title="clause.field">
        {{ fieldLabel(schemaFor(clause.field), clause.field) }}
      </v-chip>
      <v-select
        :model-value="clause.op"
        :items="opItems(clause.field)"
        item-title="title"
        item-value="value"
        density="compact"
        variant="outlined"
        hide-details
        style="max-width: 8rem"
        @update:model-value="onOpUpdate(index, $event)"
      />
      <v-select
        v-if="isSelectField(clause.field)"
        :model-value="selectModelValue(clause)"
        :items="fieldSelectOptions(clause.field)"
        density="compact"
        variant="outlined"
        hide-details
        :multiple="clause.op === 'in'"
        chips
        style="min-width: 10rem; flex: 1"
        @update:model-value="onSelectValueUpdate(index, $event)"
      />
      <v-text-field
        v-else
        :model-value="clause.value"
        density="compact"
        variant="outlined"
        hide-details
        style="min-width: 10rem; flex: 1"
        @update:model-value="onTextValueUpdate(index, $event)"
      />
      <v-btn icon size="x-small" variant="text" @click="removeField(index)">
        <v-icon icon="mdi-close" size="16" />
      </v-btn>
    </div>
  </div>
</template>
