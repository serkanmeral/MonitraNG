<script setup lang="ts">
import { computed, onMounted, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useOcPersonPicker, type OcPersonPickerApi } from '@/composables/useOcPersonPicker';
import { buildOcSelectMenuProps, type OcSelectMenuContext } from '@/utils/ocDynamicFormField';
import { collectPersonIdsFromValue, OC_PERSON_PICKER_LOAD_MORE_VALUE } from '@/utils/ocPersonPicker';

const props = withDefaults(
  defineProps<{
    multiple?: boolean;
    disabled?: boolean;
    density?: 'default' | 'comfortable' | 'compact';
    variant?: 'outlined' | 'filled' | 'plain' | 'underlined' | 'solo';
    hideDetails?: boolean | 'auto';
    placeholder?: string;
    menuContext?: OcSelectMenuContext;
    /** Form genelinde tek picker (OcDynamicForm); yoksa bileşen kendi picker'ını kullanır. */
    externalPicker?: OcPersonPickerApi;
    label?: string;
    showRequiredMark?: boolean;
    error?: boolean;
    errorMessages?: string | string[];
    fieldClass?: string;
  }>(),
  {
    multiple: false,
    disabled: false,
    density: 'comfortable',
    variant: 'outlined',
    hideDetails: 'auto',
    menuContext: 'default',
  }
);

const model = defineModel<unknown>();

const { t } = useAppI18n();
const internalPicker = useOcPersonPicker();
const picker = computed(() => props.externalPicker ?? internalPicker);

const menuProps = computed(() => buildOcSelectMenuProps(props.menuContext));

const displayItems = computed(() =>
  picker.value.itemsWithLoadMoreRow(
    t('operationCore.formUi.personsLoadMore'),
    t('operationCore.formUi.personsLoadMoreHint')
  )
);

const selectModelValue = computed(() => {
  if (props.multiple) {
    if (Array.isArray(model.value)) return model.value;
    if (model.value != null && model.value !== '') return [model.value];
    return [];
  }
  return model.value ?? null;
});

const loading = computed(() => picker.value.loading.value);

const noDataText = computed(() => {
  if (loading.value) return t('operationCore.formUi.personsLoading');
  if (!picker.value.items.value.length) return t('operationCore.formUi.personsFieldHint');
  return undefined;
});

function isLoadMoreSelection(value: unknown): boolean {
  const p = picker.value;
  if (p.isLoadMoreValue(value)) return true;
  if (Array.isArray(value)) return value.some((v) => p.isLoadMoreValue(v));
  return false;
}

async function syncSelectionFromModel() {
  const ids = collectPersonIdsFromValue(model.value);
  if (ids.length) await picker.value.ensureSelectedIds(ids);
}

async function initPicker() {
  await syncSelectionFromModel();
  if (!props.externalPicker) {
    await picker.value.resetAndFetch('');
  }
}

onMounted(() => {
  void initPicker();
});

watch(
  () => model.value,
  () => {
    void syncSelectionFromModel();
  }
);

async function onUpdate(value: unknown) {
  if (props.disabled) return;
  if (isLoadMoreSelection(value)) {
    void picker.value.loadMore();
    return;
  }
  model.value = value;
  await picker.value.onSelectionChanged(value);
}

function onSearch(query: string | null) {
  picker.value.onSearchUpdate(query);
}
</script>

<template>
  <v-autocomplete
    :model-value="selectModelValue"
    :items="displayItems"
    item-title="title"
    item-value="value"
    :disabled="disabled"
    :loading="loading"
    :menu-props="menuProps"
    :custom-filter="() => true"
    :multiple="multiple"
    :chips="multiple"
    :closable-chips="!disabled && multiple"
    :no-data-text="noDataText"
    :placeholder="placeholder ?? t('operationCore.formUi.personsSearchHint')"
    :density="density"
    :variant="variant"
    :hide-details="hideDetails"
    :error="error"
    :error-messages="errorMessages"
    clearable
    :class="fieldClass"
    @update:model-value="onUpdate"
    @update:search="onSearch"
  >
    <template v-if="label || showRequiredMark" #label>
      <span v-if="label">{{ label }}</span>
      <span v-if="showRequiredMark" class="oc-field-required" aria-hidden="true"> *</span>
    </template>
    <template #item="{ props: itemProps, item }">
      <v-list-item
        v-if="item.raw.value === OC_PERSON_PICKER_LOAD_MORE_VALUE"
        :title="item.raw.title"
        :subtitle="item.raw.subtitle || undefined"
        class="oc-person-picker__load-more"
        :disabled="picker.loadingMore.value"
        @click.prevent="void picker.loadMore()"
      />
      <v-list-item
        v-else
        v-bind="itemProps"
        :title="item.raw.title"
        :subtitle="item.raw.subtitle || undefined"
      />
    </template>
  </v-autocomplete>
</template>

<style scoped>
.oc-field-required {
  color: rgb(var(--v-theme-error));
  font-weight: 600;
}

.oc-person-picker__load-more {
  font-weight: 600;
  color: rgb(var(--v-theme-primary));
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
