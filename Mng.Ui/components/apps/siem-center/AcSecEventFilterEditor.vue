<script setup lang="ts">
import { computed } from 'vue';
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
import { listSecEventFilterFieldSchemas } from '@/utils/secEventFilterFieldSchema';
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

const typeItems = computed(() => [
  { title: t('siemCenter.events.filterCatalog.all'), value: null },
  ...SEC_EVENT_SOURCE_TYPE_OPTIONS.map((v) => ({
    title: t(sourceTypeLabelKey(v)),
    value: v,
  })),
]);

const productItems = computed(() => [
  { title: t('siemCenter.events.filterCatalog.all'), value: null },
  ...SEC_EVENT_SOURCE_PRODUCT_OPTIONS.map((v) => ({
    title: t(`siemCenter.events.filterCatalog.products.${v.replace(/-/g, '_')}`),
    value: v,
  })),
]);

const hostItems = computed(() =>
  props.hostOptions.map((h) => ({ title: h, value: h })),
);

const fieldSchemas = computed(() =>
  listSecEventFilterFieldSchemas({
    type: draft.value.scope?.type,
    product: draft.value.scope?.product,
  }),
);

const addFieldItems = computed(() =>
  fieldSchemas.value
    .filter((s) => !draft.value.fields.some((f) => f.field === s.field))
    .map((s) => ({ title: t(s.labelKey), value: s.field })),
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
  const schema = fieldSchemas.value.find((s) => s.field === field);
  if (!schema) return;
  const op: SecEventFilterFieldOp = schema.ops[0] ?? 'eq';
  draft.value = {
    ...draft.value,
    fields: [...draft.value.fields, { field, op, value: '' }],
  };
}

function schemaFor(field: SecEventFilterFieldKey) {
  return fieldSchemas.value.find((s) => s.field === field);
}

function opItems(field: SecEventFilterFieldKey) {
  const schema = schemaFor(field);
  const ops = schema?.ops ?? ['eq'];
  return ops.map((op) => ({
    title: t(`siemCenter.events.filterCatalog.ops.${op}`),
    value: op,
  }));
}

function onTypeUpdate(value: unknown) {
  patchScope({ type: typeof value === 'string' ? value : null });
}

function onProductUpdate(value: unknown) {
  patchScope({ product: typeof value === 'string' ? value : null });
}

function onHostsUpdate(value: unknown) {
  patchScope({ hosts: Array.isArray(value) ? (value as string[]) : [] });
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
    </div>
    <v-row dense class="mb-3">
      <v-col cols="12" md="4">
        <v-select
          :model-value="draft.scope.type ?? null"
          :items="typeItems"
          item-title="title"
          item-value="value"
          :label="t('siemCenter.events.filterCatalog.type')"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          @update:model-value="onTypeUpdate"
        />
      </v-col>
      <v-col cols="12" md="4">
        <v-select
          :model-value="draft.scope.product ?? null"
          :items="productItems"
          item-title="title"
          item-value="value"
          :label="t('siemCenter.events.filterCatalog.product')"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          @update:model-value="onProductUpdate"
        />
      </v-col>
      <v-col cols="12" md="4">
        <v-autocomplete
          :model-value="draft.scope.hosts ?? []"
          :items="hostItems"
          item-title="title"
          item-value="value"
          :label="t('siemCenter.events.filterCatalog.host')"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          multiple
          chips
          closable-chips
          @update:model-value="onHostsUpdate"
        />
      </v-col>
    </v-row>

    <div class="d-flex align-center mb-1">
      <span class="text-caption text-medium-emphasis">
        {{ t('siemCenter.events.filterCatalog.fieldFilters') }}
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
        <v-list density="compact">
          <v-list-item
            v-for="item in addFieldItems"
            :key="item.value"
            :title="item.title"
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
      <v-chip size="small" variant="outlined">{{ t(schemaFor(clause.field)?.labelKey ?? clause.field) }}</v-chip>
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
