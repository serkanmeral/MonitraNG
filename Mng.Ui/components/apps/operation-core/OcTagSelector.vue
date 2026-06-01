<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { buildOcSelectMenuProps } from '@/utils/ocDynamicFormField';
import { isTmStatusThemeColor } from '@/utils/taskManagerStatusColor';
import { ocCreateTag, ocListTagsForWorkspace } from '@/services/operationCoreService';
import type { OpTag } from '@/types/apps/operationCore';

const props = withDefaults(
  defineProps<{
    workspaceId?: string | null;
    multiple?: boolean;
    disabled?: boolean;
    label?: string;
    required?: boolean;
    error?: boolean;
    errorMessages?: string[];
    preview?: boolean;
  }>(),
  {
    workspaceId: '',
    multiple: true,
    disabled: false,
    label: '',
    required: false,
    error: false,
    errorMessages: () => [],
    preview: false,
  }
);

const model = defineModel<unknown>({ required: true });

const { t } = useAppI18n();

const tags = ref<OpTag[]>([]);
const loading = ref(false);
const saving = ref(false);
/** Combobox model'i (ad listesi) → modelValue id'lerinden senkronla; döngüyü engellemek için bayrak. */
const syncingFromModel = ref(false);
const comboNames = ref<string[]>([]);

const tagById = computed(() => {
  const map = new Map<string, OpTag>();
  for (const tag of tags.value) map.set(tag.__dataId, tag);
  return map;
});
const tagByLowerName = computed(() => {
  const map = new Map<string, OpTag>();
  for (const tag of tags.value) map.set(tag.name.trim().toLowerCase(), tag);
  return map;
});

const items = computed(() =>
  [...tags.value]
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }))
    .map((tag) => tag.name)
);

const menuProps = computed(() => buildOcSelectMenuProps(props.preview ? 'dialog' : 'default'));

function normalizeIds(value: unknown): string[] {
  if (value === null || value === undefined || value === '') return [];
  if (Array.isArray(value)) return value.flatMap((v) => normalizeIds(v));
  if (typeof value === 'object') {
    const o = value as Record<string, unknown>;
    const id = o.__dataId ?? o.dataId ?? o.id;
    return id ? [String(id).trim()] : [];
  }
  const s = String(value).trim();
  return s ? [s] : [];
}

/** id → görünen ad (yüklü etiketten; yoksa ham id). */
function nameForId(id: string): string {
  return tagById.value.get(id)?.name ?? id;
}

function colorForName(name: string): string | undefined {
  const tag = tagByLowerName.value.get(name.trim().toLowerCase());
  const color = tag?.color?.trim();
  return color && isTmStatusThemeColor(color) ? color : undefined;
}

function colorForId(id: string): string | undefined {
  const color = tagById.value.get(id)?.color?.trim();
  return color && isTmStatusThemeColor(color) ? color : undefined;
}

/** Salt-okunur (readonly) gösterim için seçili etiketler: ad + renk. */
const displayChips = computed(() =>
  normalizeIds(model.value).map((id) => ({ id, name: nameForId(id), color: colorForId(id) }))
);

function syncComboFromModel() {
  syncingFromModel.value = true;
  comboNames.value = normalizeIds(model.value).map((id) => nameForId(id));
  syncingFromModel.value = false;
}

async function loadTags() {
  if (!props.workspaceId) {
    tags.value = [];
    syncComboFromModel();
    return;
  }
  loading.value = true;
  try {
    tags.value = await ocListTagsForWorkspace(props.workspaceId);
  } catch {
    tags.value = [];
  } finally {
    loading.value = false;
    syncComboFromModel();
  }
}

watch(() => props.workspaceId, () => void loadTags(), { immediate: true });
watch(
  () => model.value,
  () => {
    if (!syncingFromModel.value) syncComboFromModel();
  }
);

/** Combobox'tan gelen ad listesini id'lere çevirir; eksik adları o workspace'e yeni etiket olarak yaratır. */
async function onComboUpdate(value: unknown) {
  if (props.disabled) return;
  const rawNames = (props.multiple ? (Array.isArray(value) ? value : []) : value != null ? [value] : []).map(
    (v) => String(v ?? '').trim()
  );

  const ids: string[] = [];
  const seen = new Set<string>();
  saving.value = true;
  try {
    for (const name of rawNames) {
      if (!name) continue;
      const lower = name.toLowerCase();
      let tag = tagByLowerName.value.get(lower);
      if (!tag) {
        // Halihazırda seçili id'lerden biri ham id olarak gelmiş olabilir (ad bulunamayan).
        const existingById = tagById.value.get(name);
        if (existingById) tag = existingById;
      }
      if (!tag && props.workspaceId) {
        const newId = await ocCreateTag({ name, workspaceId: props.workspaceId });
        if (newId) {
          tag = { __dataId: newId, name, color: null, description: null, workspaceId: props.workspaceId };
          tags.value = [...tags.value, tag];
        }
      }
      const id = tag?.__dataId ?? name;
      if (!seen.has(id)) {
        seen.add(id);
        ids.push(id);
      }
    }
  } finally {
    saving.value = false;
  }

  syncingFromModel.value = true;
  if (props.multiple) {
    model.value = ids;
  } else {
    model.value = ids[0] ?? null;
  }
  comboNames.value = ids.map((id) => nameForId(id));
  syncingFromModel.value = false;
}
</script>

<template>
  <!-- Salt-okunur (profil): combobox yerine renkli chip listesi. -->
  <div v-if="disabled" class="oc-tag-readonly">
    <div v-if="label" class="oc-tag-readonly__label">{{ label }}</div>
    <div v-if="displayChips.length" class="d-flex flex-wrap ga-1">
      <v-chip
        v-for="chip in displayChips"
        :key="chip.id"
        size="small"
        variant="tonal"
        :color="chip.color"
        class="text-none"
      >
        {{ chip.name }}
      </v-chip>
    </div>
    <span v-else class="text-medium-emphasis">—</span>
  </div>

  <v-combobox
    v-else
    :model-value="comboNames"
    :items="items"
    :multiple="multiple"
    :chips="multiple"
    :closable-chips="!disabled && multiple"
    :disabled="disabled"
    :loading="loading || saving"
    :menu-props="menuProps"
    :error="error"
    :error-messages="errorMessages"
    :hint="!disabled ? t('operationCore.tags.fieldHint') : undefined"
    persistent-hint
    clearable
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    @update:model-value="onComboUpdate"
  >
    <template #label>
      <span>{{ label }}</span>
      <span v-if="required" class="oc-field-required" aria-hidden="true"> *</span>
    </template>

    <template v-if="multiple" #chip="{ item, props: chipProps }">
      <v-chip
        v-bind="chipProps"
        size="small"
        variant="tonal"
        :color="colorForName(String(item.title ?? item.value ?? ''))"
        class="text-none"
      >
        {{ item.title ?? item.value }}
      </v-chip>
    </template>
  </v-combobox>
</template>

<style scoped>
.oc-field-required {
  color: rgb(var(--v-theme-error));
  font-weight: 600;
}

.oc-tag-readonly__label {
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-surface), 0.6);
  margin-bottom: 4px;
}
</style>
