import { ref, watch, type Ref } from 'vue';
import type { OcFormRuntimeContext } from '@/types/apps/operationCore';
import {
  ocListBoardsForWorkspace,
  ocListDataset,
  ocListPriorities,
  ocListPrioritiesForWorkspace,
  ocListStates,
  ocListStatesForWorkspace,
} from '@/services/operationCoreService';
import { useOcPersonPicker } from '@/composables/useOcPersonPicker';
import {
  collectPersonIdsFromFormModel,
  type OcPersonPickerItem,
} from '@/utils/ocPersonPicker';
import {
  isOcPersonsUserPickerField,
  recordToDatasetItems,
  resolveRelationDataset,
} from '@/utils/ocDynamicFormField';

export type OcSelectItem = { title: string; value: string; subtitle?: string };

const PERSONS_LOADING_KEY = '__personUsers__';

/**
 * Form alanları için relation / core select listelerini yükler.
 * `persons` alanları: `useOcPersonPicker` (arama + sayfalama).
 */
export function useOcDynamicFormLookups(
  workspaceId: Ref<string | undefined>,
  context: Ref<OcFormRuntimeContext | null>,
  formModel?: Ref<Record<string, unknown>>
) {
  const personPicker = useOcPersonPicker();

  const priorityItems = ref<OcSelectItem[]>([]);
  const stateItems = ref<OcSelectItem[]>([]);
  const boardItems = ref<OcSelectItem[]>([]);
  const relationItemsByKey = ref<Record<string, OcSelectItem[]>>({});
  const loadingKeys = ref<Set<string>>(new Set());

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

  function needsPersonUsers(keys: string[]): boolean {
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

  async function loadRelationForField(fieldKey: string, dataset: string) {
    loadingKeys.value = new Set(loadingKeys.value).add(fieldKey);
    try {
      const rows = await ocListDataset(dataset, { limit: 500 });
      relationItemsByKey.value = {
        ...relationItemsByKey.value,
        [fieldKey]: recordToDatasetItems(rows),
      };
    } catch {
      relationItemsByKey.value = { ...relationItemsByKey.value, [fieldKey]: [] };
    } finally {
      const next = new Set(loadingKeys.value);
      next.delete(fieldKey);
      loadingKeys.value = next;
    }
  }

  async function initPersonPicker() {
    loadingKeys.value = new Set(loadingKeys.value).add(PERSONS_LOADING_KEY);
    try {
      await personPicker.resetAndFetch('');
      const ids = collectPersonIdsFromFormModel(formModel?.value, personFieldKeys());
      await personPicker.ensureSelectedIds(ids);
    } finally {
      const next = new Set(loadingKeys.value);
      next.delete(PERSONS_LOADING_KEY);
      loadingKeys.value = next;
    }
  }

  async function syncPersonPickerSelection() {
    const ids = collectPersonIdsFromFormModel(formModel?.value, personFieldKeys());
    await personPicker.ensureSelectedIds(ids);
  }

  async function reload() {
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

    const loadedDatasets = new Set<string>();
    for (const fieldKey of keys) {
      const meta = ctx.fields[fieldKey];
      const widgetDataset = resolveRelationDataset(fieldKey, meta);
      if (!widgetDataset) continue;
      if (fieldKey === 'priorityId' || fieldKey === 'boardId' || fieldKey === 'stateId') continue;
      if (fieldKey === 'typeId') continue;
      if (loadedDatasets.has(`${fieldKey}:${widgetDataset}`)) continue;
      loadedDatasets.add(`${fieldKey}:${widgetDataset}`);
      tasks.push(loadRelationForField(fieldKey, widgetDataset));
    }

    await Promise.all(tasks);
  }

  function selectItemsForField(fieldKey: string): OcSelectItem[] {
    const meta = context.value?.fields[fieldKey];
    if (isOcPersonsUserPickerField(fieldKey, meta)) {
      return personPicker.items.value;
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
      return loadingKeys.value.has(PERSONS_LOADING_KEY) || personPicker.loading.value;
    }
    return loadingKeys.value.has(fieldKey);
  }

  function isPersonField(fieldKey: string): boolean {
    return isOcPersonsUserPickerField(fieldKey, context.value?.fields[fieldKey]);
  }

  watch(
    () => [workspaceId.value, context.value] as const,
    () => {
      void reload();
    },
    { immediate: true }
  );

  if (formModel) {
    watch(
      formModel,
      () => {
        if (!personFieldKeys().length) return;
        void syncPersonPickerSelection();
      },
      { deep: true }
    );
  }

  return {
    priorityItems,
    stateItems,
    boardItems,
    relationItemsByKey,
    personPicker,
    selectItemsForField,
    isLoadingField,
    isPersonField,
    reload,
  };
}

export type { OcPersonPickerItem };
