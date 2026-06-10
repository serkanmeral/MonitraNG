import { computed, ref, watch, type Ref } from 'vue';
import { ocListDataset } from '@/services/operationCoreService';
import type { OpField } from '@/types/apps/operationCore';
import { recordToDatasetItems } from '@/utils/ocDynamicFormField';
import {
  OC_LOOKUP_DEFAULT_LABEL_FIELD,
  OC_LOOKUP_DEFAULT_VALUE_FIELD,
  parseOcLookupFromFieldOptions,
} from '@/utils/ocLookupFieldOptions';

export interface OcBoardFilterColumnRef {
  key: string;
  kind: string;
}

function relationFields(poolFields: OpField[]): OpField[] {
  return poolFields.filter(
    (f) =>
      f.key &&
      (f.fieldType || '').toLowerCase() === 'relation' &&
      !!f.relationDatasetName?.trim()
  );
}

function lookupKeys(field: OpField): { valueKey: string; labelKey: string } {
  const lookup = parseOcLookupFromFieldOptions(field.options, field.fieldType);
  return {
    valueKey: lookup?.valueField ?? OC_LOOKUP_DEFAULT_VALUE_FIELD,
    labelKey: lookup?.labelField ?? OC_LOOKUP_DEFAULT_LABEL_FIELD,
  };
}

function datasetCacheKey(dataset: string, valueKey: string, labelKey: string): string {
  return `${dataset}\0${valueKey}\0${labelKey}`;
}

/**
 * Board liste — pool relation alanları için dataset lookup (op_fields.options.lookup.labelField).
 * Görünen liste sütunları + filtrelenebilir relation sütunları için seçenekleri yükler.
 */
export function useOcBoardRelationLookups(
  poolFields: Ref<OpField[]>,
  listColumnKeys: Ref<string[]>,
  filterableColumns: Ref<OcBoardFilterColumnRef[]>
) {
  const relationOptionsByKey = ref<Record<string, { value: string; title: string }[]>>({});

  const relationPoolKeySet = computed(
    () => new Set(relationFields(poolFields.value).map((f) => f.key))
  );

  const fieldByKey = computed(() => {
    const map = new Map<string, OpField>();
    for (const f of relationFields(poolFields.value)) {
      if (f.key) map.set(f.key, f);
    }
    return map;
  });

  const keysToLoad = computed(() => {
    const keys = new Set<string>();
    for (const k of listColumnKeys.value) {
      if (relationPoolKeySet.value.has(k)) keys.add(k);
    }
    for (const col of filterableColumns.value) {
      if (col.kind === 'relation') keys.add(col.key);
    }
    return [...keys].sort();
  });

  const relationLabelByKey = computed(() => {
    const map = new Map<string, Map<string, string>>();
    for (const [key, items] of Object.entries(relationOptionsByKey.value)) {
      map.set(key, new Map(items.map((i) => [i.value, i.title])));
    }
    return map;
  });

  let loadGeneration = 0;

  async function loadRelationOptions(keys: string[]) {
    const generation = ++loadGeneration;
    const fields = keys
      .map((k) => fieldByKey.value.get(k))
      .filter((f): f is OpField => !!f);

    if (!fields.length) {
      relationOptionsByKey.value = {};
      return;
    }

    const cache = new Map<string, { value: string; title: string }[]>();
    const next: Record<string, { value: string; title: string }[]> = {
      ...relationOptionsByKey.value,
    };

    await Promise.all(
      fields.map(async (f) => {
        const dataset = f.relationDatasetName!.trim();
        const key = f.key as string;
        const { valueKey, labelKey } = lookupKeys(f);
        const cacheKey = datasetCacheKey(dataset, valueKey, labelKey);
        try {
          let items = cache.get(cacheKey);
          if (!items) {
            const rows = await ocListDataset(dataset, { limit: 500 });
            items = recordToDatasetItems(rows, { idKey: valueKey, labelKey });
            cache.set(cacheKey, items);
          }
          if (generation === loadGeneration) {
            next[key] = items;
          }
        } catch {
          if (generation === loadGeneration) {
            next[key] = [];
          }
        }
      })
    );

    if (generation === loadGeneration) {
      relationOptionsByKey.value = next;
    }
  }

  /** Relation pool değerini (id / id[] / nesne) okunabilir etikete çevirir. */
  function resolveRelationValue(key: string, value: unknown): string {
    const field = fieldByKey.value.get(key);
    const labelKey = field ? lookupKeys(field).labelKey : OC_LOOKUP_DEFAULT_LABEL_FIELD;
    const labels = relationLabelByKey.value.get(key);
    const entries = Array.isArray(value) ? value : value == null || value === '' ? [] : [value];
    if (!entries.length) return '—';

    const names = entries
      .map((entry) => {
        if (entry && typeof entry === 'object') {
          const o = entry as Record<string, unknown>;
          const id = String(o.__dataId ?? o.dataId ?? o.id ?? '').trim();
          const inline = o[labelKey] ?? o.name ?? o.title ?? o.label;
          return (inline != null ? String(inline) : '') || (id ? labels?.get(id) : '') || id || '';
        }
        const id = String(entry).trim();
        return labels?.get(id) || id;
      })
      .filter((n) => n && n !== '—');

    return names.length ? names.join(', ') : '—';
  }

  async function ensureRelationOptions() {
    await loadRelationOptions(keysToLoad.value);
  }

  watch(
    [poolFields, keysToLoad],
    ([, keys]) => {
      void loadRelationOptions(keys);
    },
    { immediate: true }
  );

  return {
    relationPoolKeySet,
    relationOptionsByKey,
    resolveRelationValue,
    ensureRelationOptions,
  };
}
