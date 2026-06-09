import { ref, watch, type Ref } from 'vue';
import type { OcFormFieldRuntimeDto, OcFormRuntimeContext } from '@/types/apps/operationCore';
import {
  ocListBoardsForWorkspace,
  ocListDataset,
  ocListPriorities,
  ocListPrioritiesForWorkspace,
  ocListStates,
  ocListStatesForWorkspace,
} from '@/services/operationCoreService';
import { useOcPersonPicker, type OcPersonPickerApi } from '@/composables/useOcPersonPicker';
import { useOcDatasetPicker, type OcDatasetPickerApi, type OcDatasetPickerConfig } from '@/composables/useOcDatasetPicker';
import { collectPersonIdsFromValue } from '@/utils/ocPersonPicker';
import {
  isOcPersonsUserPickerField,
  recordToDatasetItems,
  resolveOcDynamicFieldWidget,
  resolveRelationDataset,
} from '@/utils/ocDynamicFormField';
import { resolveOcFormFieldType } from '@/utils/ocFormFieldLabels';
import {
  extractLookupStoredValue,
  collectLookupIdsFromValue,
  lookupStaticItemsToSelectItems,
  parseOcLookupFromFieldOptions,
  resolveLookupDependsOnFilter,
  resolveEffectiveLookupPresentation,
  type OcLookupConfig,
} from '@/utils/ocLookupFieldOptions';

export type OcSelectItem = { title: string; value: string; subtitle?: string };

const PERSONS_LOADING_KEY = '__personUsers__';

function resolveFieldLookup(
  fieldKey: string,
  meta?: OcFormFieldRuntimeDto | null
): OcLookupConfig | null {
  const ft = resolveOcFormFieldType(fieldKey, meta);
  return parseOcLookupFromFieldOptions(meta?.options, ft);
}

/**
 * Form alanları için relation / core select listelerini yükler.
 * `persons` alanları: `useOcPersonPicker` (arama + sayfalama).
 */
export function useOcDynamicFormLookups(
  workspaceId: Ref<string | undefined>,
  context: Ref<OcFormRuntimeContext | null>,
  formModel?: Ref<Record<string, unknown>>,
  options?: { readonly?: Ref<boolean> }
) {
  const isReadonly = () => options?.readonly?.value === true;
  const personPickers = new Map<string, OcPersonPickerApi>();
  const datasetPickers = new Map<string, OcDatasetPickerApi>();

  const priorityItems = ref<OcSelectItem[]>([]);
  const stateItems = ref<OcSelectItem[]>([]);
  const boardItems = ref<OcSelectItem[]>([]);
  const relationItemsByKey = ref<Record<string, OcSelectItem[]>>({});
  const loadingKeys = ref<Set<string>>(new Set());
  const dependsOnParentByField = ref<Record<string, string | null>>({});

  function fieldKeysNeedingLookups(): string[] {
    const ctx = context.value;
    if (!ctx?.fields) return [];
    return Object.keys(ctx.fields);
  }

  function personFieldKeys(): string[] {
    const ctx = context.value;
    if (!ctx?.fields) return [];
    return Object.keys(ctx.fields).filter((key) =>
      isOcPersonsUserPickerField(key, ctx.fields[key])
    );
  }

  function needsPersonUsers(keys: string[]) {
    return keys.some((key) => isOcPersonsUserPickerField(key, context.value?.fields[key]));
  }

  function needsPriority(keys: string[]) {
    return keys.includes('priorityId');
  }

  function needsState(keys: string[]) {
    return keys.includes('stateId');
  }

  function needsBoard(keys: string[]) {
    return keys.includes('boardId');
  }

  function parentValueForField(fieldKey: string): string | null {
    const lookup = resolveFieldLookup(fieldKey, context.value?.fields[fieldKey]);
    const parentKey = lookup?.dependsOn?.fieldKey;
    if (!parentKey || !formModel?.value) return null;
    return extractLookupStoredValue(formModel.value[parentKey]);
  }

  function isFieldDependsOnBlocked(fieldKey: string): boolean {
    const lookup = resolveFieldLookup(fieldKey, context.value?.fields[fieldKey]);
    if (!lookup?.dependsOn) return false;
    return !parentValueForField(fieldKey);
  }

  async function loadRelationForField(fieldKey: string, dataset: string, lookup: OcLookupConfig) {
    if (lookup.dependsOn && !parentValueForField(fieldKey)) {
      relationItemsByKey.value = { ...relationItemsByKey.value, [fieldKey]: [] };
      return;
    }

    loadingKeys.value = new Set(loadingKeys.value).add(fieldKey);
    try {
      const dependsFilter = lookup.dependsOn
        ? resolveLookupDependsOnFilter(lookup.dependsOn.filterTemplate, formModel?.value?.[lookup.dependsOn.fieldKey])
        : null;
      const filterParts = [lookup.filter, dependsFilter].filter(Boolean);
      const filter = filterParts.length ? filterParts.join(',') : undefined;

      const rows = await ocListDataset(dataset, {
        limit: lookup.pageSize,
        filter,
      });
      relationItemsByKey.value = {
        ...relationItemsByKey.value,
        [fieldKey]: recordToDatasetItems(rows, {
          idKey: lookup.valueField,
          labelKey: lookup.labelField,
        }),
      };
    } catch {
      relationItemsByKey.value = { ...relationItemsByKey.value, [fieldKey]: [] };
    } finally {
      const next = new Set(loadingKeys.value);
      next.delete(fieldKey);
      loadingKeys.value = next;
    }
  }

  function isDatasetPickerField(fieldKey: string, meta?: OcFormFieldRuntimeDto | null): boolean {
    const widget = resolveOcDynamicFieldWidget(fieldKey, meta);
    if (widget !== 'relationSelect' && widget !== 'relationSelectMulti') return false;
    const lookup = resolveFieldLookup(fieldKey, meta);
    return resolveEffectiveLookupPresentation(lookup) === 'picker';
  }

  function buildDatasetPickerConfig(fieldKey: string): OcDatasetPickerConfig {
    const meta = context.value?.fields[fieldKey];
    const lookup =
      resolveFieldLookup(fieldKey, meta) ?? parseOcLookupFromFieldOptions(null, 'relation')!;
    const dataset = resolveRelationDataset(fieldKey, meta) ?? '';
    const dependsFilter = lookup.dependsOn
      ? resolveLookupDependsOnFilter(lookup.dependsOn.filterTemplate, formModel?.value?.[lookup.dependsOn.fieldKey])
      : null;
    return {
      dataset,
      valueField: lookup.valueField,
      labelField: lookup.labelField,
      pageSize: lookup.pageSize,
      baseFilter: lookup.filter,
      dependsOnFilter: dependsFilter,
      searchFields: lookup.searchFields,
    };
  }

  function datasetPickerForField(fieldKey: string): OcDatasetPickerApi {
    let picker = datasetPickers.get(fieldKey);
    if (!picker) {
      picker = useOcDatasetPicker(() => buildDatasetPickerConfig(fieldKey));
      datasetPickers.set(fieldKey, picker);
    }
    return picker;
  }

  function selectedIdsForDatasetField(fieldKey: string): string[] {
    return collectLookupIdsFromValue(formModel?.value?.[fieldKey]);
  }

  async function syncDatasetPickerSelection() {
    await Promise.all(
      fieldKeysNeedingLookups()
        .filter((key) => isDatasetPickerField(key, context.value?.fields[key]))
        .map((key) => datasetPickerForField(key).ensureSelectedLabels(selectedIdsForDatasetField(key)))
    );
  }
  function pickerForField(fieldKey: string): OcPersonPickerApi {
    let picker = personPickers.get(fieldKey);
    if (!picker) {
      picker = useOcPersonPicker();
      personPickers.set(fieldKey, picker);
    }
    return picker;
  }

  function selectedIdsForField(fieldKey: string): string[] {
    return collectPersonIdsFromValue(formModel?.value?.[fieldKey]);
  }

  async function initPersonPicker() {
    loadingKeys.value = new Set(loadingKeys.value).add(PERSONS_LOADING_KEY);
    try {
      await Promise.all(
        personFieldKeys().map(async (key) => {
          const picker = pickerForField(key);
          await picker.resetAndFetch('');
          await picker.ensureSelectedIds(selectedIdsForField(key));
        })
      );
    } finally {
      const next = new Set(loadingKeys.value);
      next.delete(PERSONS_LOADING_KEY);
      loadingKeys.value = next;
    }
  }

  async function syncPersonPickerSelection() {
    await Promise.all(
      personFieldKeys().map((key) =>
        pickerForField(key).ensureSelectedIds(selectedIdsForField(key))
      )
    );
  }

  async function reload() {
    if (isReadonly()) return;
    const ws = workspaceId.value?.trim();
    const ctx = context.value;
    if (!ctx) return;

    const keys = fieldKeysNeedingLookups();
    const tasks: Promise<void>[] = [];

    if (needsPersonUsers(keys)) {
      tasks.push(initPersonPicker());
    }

    if (needsPriority(keys)) {
      tasks.push(
        (ws
          ? ocListPrioritiesForWorkspace(ws, { fallbackAll: true })
          : ocListPriorities()
        ).then((rows) => {
          priorityItems.value = rows.map((p) => ({ title: p.name, value: p.__dataId }));
        })
      );
    }

    if (needsState(keys)) {
      tasks.push(
        (ws ? ocListStatesForWorkspace(ws, { fallbackAll: true }) : ocListStates()).then((rows) => {
          stateItems.value = rows.map((s) => ({ title: s.name, value: s.__dataId }));
        })
      );
    }

    if (needsBoard(keys) && ws) {
      tasks.push(
        ocListBoardsForWorkspace(ws).then((rows) => {
          boardItems.value = rows.map((b) => ({ title: b.name, value: b.__dataId }));
        })
      );
    }

    for (const fieldKey of keys) {
      const meta = ctx.fields[fieldKey];
      const widget = resolveOcDynamicFieldWidget(fieldKey, meta);
      if (widget === 'tags') continue;

      if (widget === 'staticSelect' || widget === 'staticSelectMulti') {
        const lookup = resolveFieldLookup(fieldKey, meta);
        relationItemsByKey.value = {
          ...relationItemsByKey.value,
          [fieldKey]: lookupStaticItemsToSelectItems(lookup?.staticItems ?? []),
        };
        continue;
      }

      const widgetDataset = resolveRelationDataset(fieldKey, meta);
      if (!widgetDataset) continue;
      if (fieldKey === 'priorityId' || fieldKey === 'boardId' || fieldKey === 'stateId') continue;
      if (fieldKey === 'typeId') continue;

      const lookup =
        resolveFieldLookup(fieldKey, meta) ??
        parseOcLookupFromFieldOptions(null, 'relation')!;

      if (resolveEffectiveLookupPresentation(lookup) === 'picker') {
        tasks.push(datasetPickerForField(fieldKey).ensureSelectedLabels(selectedIdsForDatasetField(fieldKey)));
        continue;
      }

      tasks.push(loadRelationForField(fieldKey, widgetDataset, lookup));
    }

    await Promise.all(tasks);
  }

  function selectItemsForField(fieldKey: string): OcSelectItem[] {
    const meta = context.value?.fields[fieldKey];
    if (isOcPersonsUserPickerField(fieldKey, meta)) {
      return pickerForField(fieldKey).items.value;
    }
    if (fieldKey === 'priorityId') return priorityItems.value;
    if (fieldKey === 'boardId') return boardItems.value;
    if (fieldKey === 'stateId') return stateItems.value;
    if (fieldKey === 'typeId') {
      return (context.value?.types ?? []).map((t) => ({ title: t.name, value: t.id }));
    }
    return relationItemsByKey.value[fieldKey] ?? [];
  }

  function isLoadingField(fieldKey: string): boolean {
    const meta = context.value?.fields[fieldKey];
    if (isOcPersonsUserPickerField(fieldKey, meta)) {
      return loadingKeys.value.has(PERSONS_LOADING_KEY) || pickerForField(fieldKey).loading.value;
    }
    if (isDatasetPickerField(fieldKey, meta)) {
      return datasetPickerForField(fieldKey).loading.value;
    }
    return loadingKeys.value.has(fieldKey);
  }

  function isPersonField(fieldKey: string): boolean {
    return isOcPersonsUserPickerField(fieldKey, context.value?.fields[fieldKey]);
  }

  function isDatasetPickerFieldForKey(fieldKey: string): boolean {
    return isDatasetPickerField(fieldKey, context.value?.fields[fieldKey]);
  }

  function isFieldDisabledByDependsOn(fieldKey: string): boolean {
    return isFieldDependsOnBlocked(fieldKey);
  }

  function selectPresentationForField(fieldKey: string): 'dropdown' | 'autocomplete' | 'picker' {
    const lookup = resolveFieldLookup(fieldKey, context.value?.fields[fieldKey]);
    return resolveEffectiveLookupPresentation(lookup);
  }

  watch(
    () => [workspaceId.value, context.value, options?.readonly?.value] as const,
    () => {
      void reload();
    },
    { immediate: true }
  );

  if (formModel) {
    watch(
      formModel,
      () => {
        if (isReadonly()) return;
        if (!personFieldKeys().length) return;
        void syncPersonPickerSelection();
      },
      { deep: true }
    );

    watch(
      formModel,
      () => {
        if (isReadonly()) return;
        const pickerKeys = fieldKeysNeedingLookups().filter((key) =>
          isDatasetPickerField(key, context.value?.fields[key])
        );
        if (!pickerKeys.length) return;
        void syncDatasetPickerSelection();
      },
      { deep: true }
    );

    watch(
      formModel,
      () => {
        if (isReadonly()) return;
        const ctx = context.value;
        if (!ctx?.fields) return;

        for (const fieldKey of Object.keys(ctx.fields)) {
          const lookup = resolveFieldLookup(fieldKey, ctx.fields[fieldKey]);
          if (!lookup?.dependsOn) continue;

          const parentVal = parentValueForField(fieldKey);
          const prev = dependsOnParentByField.value[fieldKey] ?? null;
          if (prev === parentVal) continue;

          dependsOnParentByField.value = {
            ...dependsOnParentByField.value,
            [fieldKey]: parentVal,
          };

          if (!parentVal) {
            const meta = ctx.fields[fieldKey];
            const multi =
              resolveOcDynamicFieldWidget(fieldKey, meta) === 'relationSelectMulti' ||
              resolveOcDynamicFieldWidget(fieldKey, meta) === 'staticSelectMulti';
            if (formModel.value[fieldKey] != null && formModel.value[fieldKey] !== '') {
              formModel.value[fieldKey] = multi ? [] : null;
            }
            relationItemsByKey.value = { ...relationItemsByKey.value, [fieldKey]: [] };
            if (isDatasetPickerField(fieldKey, meta)) {
              void datasetPickerForField(fieldKey).resetAndFetch('');
            }
            continue;
          }

          const dataset = resolveRelationDataset(fieldKey, ctx.fields[fieldKey]);
          if (!dataset) continue;

          if (isDatasetPickerField(fieldKey, ctx.fields[fieldKey])) {
            void datasetPickerForField(fieldKey).resetAndFetch('');
          } else {
            void loadRelationForField(fieldKey, dataset, lookup);
          }
        }
      },
      { deep: true }
    );
  }

  return {
    priorityItems,
    stateItems,
    boardItems,
    relationItemsByKey,
    pickerForField,
    datasetPickerForField,
    selectItemsForField,
    isLoadingField,
    isPersonField,
    isDatasetPickerFieldForKey,
    isFieldDisabledByDependsOn,
    selectPresentationForField,
    reload,
  };
}
