<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  createEventLogPackage,
  deleteEventLogPackage,
  fetchEventLogChannelDictionary,
  fetchEventLogPackageManageList,
  fetchEventLogPackagePresets,
  publishEventLogPackageCatalog,
  updateEventLogPackage,
} from '@/services/eventLogPackageCatalogService';
import type {
  EventLogChannelDictionary,
  EventLogPackageManageItem,
  EventLogPackageManageListResponse,
  EventLogPackagePreset,
  EventLogSelectionMode,
} from '@/types/apps/eventLogPackageCatalog';

function asPositiveIntIds(values: unknown[]): number[] {
  return [
    ...new Set(
      values
        .map((n) => Number(n))
        .filter((n) => Number.isFinite(n) && n > 0),
    ),
  ].sort((a, b) => a - b);
}

function formatApiError(e: unknown): string {
  if (e && typeof e === 'object') {
    const err = e as {
      data?: { data?: { error?: string }; error?: string; message?: string };
      message?: string;
    };
    const nested =
      err.data?.data?.error || err.data?.error || err.data?.message || err.message;
    if (nested) return String(nested);
  }
  return e instanceof Error ? e.message : String(e);
}

const { t, locale } = useAppI18n();

const loading = ref(true);
const saving = ref(false);
const publishing = ref(false);
const error = ref<string | null>(null);
const flash = ref<string | null>(null);
const managed = ref<EventLogPackageManageListResponse | null>(null);
const channels = ref<EventLogChannelDictionary[]>([]);
const presets = ref<EventLogPackagePreset[]>([]);

const dialogOpen = ref(false);
const editingName = ref<string | null>(null);
/** Snapshot of IDs when edit opened — save fallback if UI select gets cleared. */
const editingOriginalIds = ref<number[]>([]);
const formName = ref('');
const formChannel = ref('System');
const formSelectionMode = ref<EventLogSelectionMode>('selected');
const formIsDefault = ref(true);
/** Canonical Event ID list (include or exclude depending on selectionMode). */
const formEventIds = ref<number[]>([]);
const deleteTarget = ref<EventLogPackageManageItem | null>(null);
const saveError = ref(false);
/** Swallow one empty v-select emit after dialog open (Vuetify mount glitch). */
const swallowEmptySelect = ref(false);

const selectionModeOptions = computed(() => [
  {
    title: t('siemCenter.settings.catalog.modeSelected'),
    value: 'selected' as const,
    subtitle: t('siemCenter.settings.catalog.modeSelectedHint'),
  },
  {
    title: t('siemCenter.settings.catalog.modeAll'),
    value: 'all' as const,
    subtitle: t('siemCenter.settings.catalog.modeAllHint'),
  },
]);

const dateLocale = computed(() => (locale.value === 'tr' ? 'tr-TR' : 'en-GB'));

const channelOptions = computed(() =>
  channels.value.map((c) => ({
    title: c.label && c.label !== c.channel ? `${c.label}` : c.channel,
    value: c.channel,
  })),
);

function normalizeChannelModel(v: unknown): string {
  if (v == null) return '';
  if (typeof v === 'string') return v;
  if (typeof v === 'object' && 'value' in (v as object)) {
    return String((v as { value: unknown }).value ?? '');
  }
  return String(v);
}

function onFormChannelUpdate(v: unknown) {
  formChannel.value = normalizeChannelModel(v);
}

const knownIdsForChannel = computed(() => {
  const ch = String(formChannel.value || '').trim();
  const hit = channels.value.find((c) => c.channel === ch);
  return hit?.knownEventIds ?? [];
});

const knownIdOptions = computed(() =>
  knownIdsForChannel.value.map((k) => ({
    title: `${k.id} — ${k.label}`,
    value: Number(k.id),
  })),
);

const knownIdSet = computed(
  () => new Set(knownIdsForChannel.value.map((k) => Number(k.id))),
);

/** Known-ID multi-select mirrors a subset of formEventIds (never the source of truth). */
const formSelectedIds = computed({
  get: () => formEventIds.value.filter((id) => knownIdSet.value.has(id)),
  set: (values: number[]) => {
    const known = knownIdSet.value;
    const keptUnknown = formEventIds.value.filter((id) => !known.has(id));
    formEventIds.value = asPositiveIntIds([...values, ...keptUnknown]);
  },
});

const formExtraIds = computed({
  get: () =>
    formEventIds.value
      .filter((id) => !knownIdSet.value.has(id))
      .join(', '),
  set: (text: string) => {
    const known = knownIdSet.value;
    const selectedKnown = formEventIds.value.filter((id) => known.has(id));
    const extras = String(text || '')
      .split(/[\s,;]+/)
      .map((s) => s.trim())
      .filter(Boolean);
    formEventIds.value = asPositiveIntIds([...selectedKnown, ...extras]);
  },
});

function formatIdsCell(item: EventLogPackageManageItem): string {
  if (item.selectionMode === 'all') {
    const exclude =
      item.excludedEventIds.length > 0
        ? t('siemCenter.settings.catalog.idsSummaryExclude', {
            ids: item.excludedEventIds.join(', '),
          })
        : '';
    return t('siemCenter.settings.catalog.idsSummaryAll', { exclude });
  }
  return item.eventIds.join(', ');
}

function formatUtc(iso?: string | null): string {
  if (!iso) return '—';
  try {
    return new Intl.DateTimeFormat(dateLocale.value, {
      dateStyle: 'short',
      timeStyle: 'medium',
      timeZone: 'UTC',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

async function load() {
  loading.value = true;
  error.value = null;
  saveError.value = false;
  try {
    const [list, dict, presetList] = await Promise.all([
      fetchEventLogPackageManageList(),
      fetchEventLogChannelDictionary(),
      fetchEventLogPackagePresets().catch(() => [] as EventLogPackagePreset[]),
    ]);
    managed.value = list;
    channels.value = dict;
    presets.value = presetList;
    if (!formChannel.value && dict[0]) formChannel.value = dict[0].channel;
  } catch (e: unknown) {
    managed.value = null;
    saveError.value = false;
    error.value = formatApiError(e);
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingName.value = null;
  editingOriginalIds.value = [];
  formName.value = '';
  formChannel.value = channels.value[0]?.channel || 'System';
  formSelectionMode.value = 'selected';
  formIsDefault.value = true;
  formEventIds.value = [];
  swallowEmptySelect.value = true;
  dialogOpen.value = true;
}

function openEdit(item: EventLogPackageManageItem) {
  editingName.value = item.name;
  formName.value = item.name;
  formChannel.value = item.channel;
  formSelectionMode.value = item.selectionMode === 'all' ? 'all' : 'selected';
  formIsDefault.value = item.isDefault;
  const ids = asPositiveIntIds(
    item.selectionMode === 'all' ? item.excludedEventIds : item.eventIds,
  );
  editingOriginalIds.value = ids;
  formEventIds.value = [...ids];
  swallowEmptySelect.value = true;
  dialogOpen.value = true;
}

function applyPreset(preset: EventLogPackagePreset) {
  if (editingName.value) return;
  formName.value = preset.suggestedName || preset.id;
  formChannel.value = preset.channel;
  formSelectionMode.value = 'selected';
  formIsDefault.value = preset.isDefault;
  formEventIds.value = asPositiveIntIds(preset.eventIds);
}

function onSelectedIdsUpdate(v: unknown) {
  const next = asPositiveIntIds(Array.isArray(v) ? v : []);
  if (next.length === 0 && swallowEmptySelect.value) {
    swallowEmptySelect.value = false;
    return;
  }
  swallowEmptySelect.value = false;
  formSelectedIds.value = next;
}

// Include list must not become exclude list (and vice versa) when scope changes.
watch(formSelectionMode, (mode, prev) => {
  if (!prev || mode === prev) return;
  formEventIds.value = [];
  editingOriginalIds.value = [];
});

function selectAllKnown() {
  const known = asPositiveIntIds(knownIdsForChannel.value.map((k) => k.id));
  const unknown = formEventIds.value.filter((id) => !knownIdSet.value.has(id));
  formEventIds.value = asPositiveIntIds([...known, ...unknown]);
}

function clearKnown() {
  formEventIds.value = formEventIds.value.filter((id) => !knownIdSet.value.has(id));
}

async function saveForm() {
  saveError.value = true;
  if (!formName.value.trim()) {
    flash.value = null;
    error.value = t('siemCenter.settings.catalog.nameRequired');
    return;
  }
  const channel = normalizeChannelModel(formChannel.value).trim();
  if (!channel) {
    error.value = t('siemCenter.settings.catalog.channelRequired');
    return;
  }
  // Force selected when editing existing curated packages unless user explicitly chose all
  // and has the new collector — still always send numeric eventIds for selected.
  const mode: EventLogSelectionMode =
    formSelectionMode.value === 'all' ? 'all' : 'selected';

  let ids = asPositiveIntIds(formEventIds.value);
  // Only for selected: recover wipe of include IDs. Never inject includes as excludes for "all".
  if (mode === 'selected' && ids.length === 0 && editingOriginalIds.value.length > 0) {
    ids = [...editingOriginalIds.value];
    formEventIds.value = [...ids];
  }

  if (mode === 'selected' && ids.length === 0) {
    error.value = t('siemCenter.settings.catalog.idsRequired');
    return;
  }

  saving.value = true;
  error.value = null;
  flash.value = null;
  const payload = {
    name: formName.value.trim(),
    channel,
    selectionMode: mode,
    // Always include eventIds for selected; old collectors only read this field.
    eventIds: mode === 'selected' ? ids : [],
    excludedEventIds: mode === 'all' ? ids : [],
    isDefault: !!formIsDefault.value,
  };
  try {
    if (editingName.value) {
      await updateEventLogPackage(editingName.value, payload);
      flash.value = t('siemCenter.settings.catalog.saved');
    } else {
      await createEventLogPackage(payload);
      flash.value = t('siemCenter.settings.catalog.created');
    }
    dialogOpen.value = false;
    editingOriginalIds.value = [];
    await load();
  } catch (e: unknown) {
    saveError.value = true;
    error.value = formatApiError(e);
  } finally {
    saving.value = false;
  }
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  saving.value = true;
  error.value = null;
  try {
    await deleteEventLogPackage(deleteTarget.value.name);
    flash.value = t('siemCenter.settings.catalog.deleted');
    deleteTarget.value = null;
    await load();
  } catch (e: unknown) {
    saveError.value = true;
    error.value = formatApiError(e);
  } finally {
    saving.value = false;
  }
}

async function publish() {
  publishing.value = true;
  error.value = null;
  saveError.value = true;
  flash.value = null;
  try {
    const res = await publishEventLogPackageCatalog();
    flash.value = t('siemCenter.settings.catalog.published', { version: res.version });
    await load();
  } catch (e: unknown) {
    error.value = formatApiError(e);
  } finally {
    publishing.value = false;
  }
}

onMounted(load);

defineExpose({ refresh: load });
</script>

<template>
  <div class="siem-settings-catalog">
    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      {{ t('siemCenter.settings.catalog.manageHint') }}
    </v-alert>

    <div class="d-flex flex-wrap align-center ga-2 mb-4">
      <v-btn
        size="small"
        variant="tonal"
        color="primary"
        prepend-icon="mdi-refresh"
        :loading="loading"
        @click="load"
      >
        {{ t('siemCenter.settings.catalog.refresh') }}
      </v-btn>
      <v-btn
        size="small"
        color="primary"
        prepend-icon="mdi-plus"
        @click="openCreate"
      >
        {{ t('siemCenter.settings.catalog.create') }}
      </v-btn>
      <v-btn
        size="small"
        color="secondary"
        variant="flat"
        prepend-icon="mdi-publish"
        :loading="publishing"
        :disabled="!managed"
        @click="publish"
      >
        {{ t('siemCenter.settings.catalog.publish') }}
      </v-btn>
      <template v-if="managed">
        <v-chip size="small" variant="tonal">
          {{ t('siemCenter.settings.catalog.version') }}: {{ managed.version || '—' }}
        </v-chip>
        <v-chip
          v-if="managed.hasUnpublishedChanges"
          size="small"
          color="warning"
          variant="tonal"
        >
          {{ t('siemCenter.settings.catalog.unpublished') }}
        </v-chip>
        <span class="text-caption text-medium-emphasis">
          {{ t('siemCenter.settings.catalog.publishedAt') }}:
          {{ formatUtc(managed.publishedUtc) }} UTC
        </span>
      </template>
    </div>

    <v-alert v-if="flash" type="success" variant="tonal" density="compact" class="mb-3" closable>
      {{ flash }}
    </v-alert>
    <v-alert v-if="error" type="error" variant="tonal" class="mb-4">
      <template v-if="!saveError">{{ t('siemCenter.settings.catalog.loadError') }}</template>
      <div :class="saveError ? '' : 'text-caption mt-1'">{{ error }}</div>
    </v-alert>

    <v-skeleton-loader v-if="loading && !managed" type="table" />

    <template v-else-if="managed">
      <v-table density="comfortable" class="mb-2">
        <thead>
          <tr>
            <th>{{ t('siemCenter.settings.catalog.colName') }}</th>
            <th>{{ t('siemCenter.settings.catalog.colChannel') }}</th>
            <th>{{ t('siemCenter.settings.catalog.colMode') }}</th>
            <th>{{ t('siemCenter.settings.catalog.colEventIds') }}</th>
            <th>{{ t('siemCenter.settings.catalog.colDefault') }}</th>
            <th class="text-end" />
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in managed.items" :key="item.name">
            <td class="font-mono">{{ item.name }}</td>
            <td class="text-body-2">{{ item.channel }}</td>
            <td>
              <v-chip
                size="x-small"
                :color="item.selectionMode === 'all' ? 'warning' : 'primary'"
                variant="tonal"
              >
                {{
                  item.selectionMode === 'all'
                    ? t('siemCenter.settings.catalog.modeAll')
                    : t('siemCenter.settings.catalog.modeSelected')
                }}
              </v-chip>
            </td>
            <td class="font-mono text-caption">{{ formatIdsCell(item) }}</td>
            <td>
              <v-chip
                size="x-small"
                :color="item.isDefault ? 'success' : 'default'"
                variant="tonal"
              >
                {{
                  item.isDefault
                    ? t('siemCenter.settings.catalog.defaultYes')
                    : t('siemCenter.settings.catalog.defaultNo')
                }}
              </v-chip>
            </td>
            <td class="text-end text-no-wrap">
              <v-btn
                icon="mdi-pencil"
                size="small"
                variant="text"
                @click="openEdit(item)"
              />
              <v-btn
                icon="mdi-delete"
                size="small"
                variant="text"
                color="error"
                @click="deleteTarget = item"
              />
            </td>
          </tr>
        </tbody>
      </v-table>
      <p v-if="!managed.items.length" class="text-medium-emphasis text-body-2">
        {{ t('siemCenter.settings.catalog.empty') }}
      </p>
    </template>

    <v-dialog v-model="dialogOpen" max-width="680" scrollable>
      <v-card>
        <v-card-title>
          {{
            editingName
              ? t('siemCenter.settings.catalog.editTitle')
              : t('siemCenter.settings.catalog.createTitle')
          }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pt-4">
          <template v-if="!editingName && presets.length">
            <div class="text-subtitle-2 mb-1">
              {{ t('siemCenter.settings.catalog.presetsTitle') }}
            </div>
            <p class="text-caption text-medium-emphasis mb-2">
              {{ t('siemCenter.settings.catalog.presetsHint') }}
            </p>
            <div class="d-flex flex-wrap ga-2 mb-4">
              <v-chip
                v-for="p in presets"
                :key="p.id"
                size="small"
                variant="tonal"
                color="primary"
                class="cursor-pointer"
                :title="p.description"
                @click="applyPreset(p)"
              >
                {{ p.title }}
              </v-chip>
            </div>
          </template>

          <v-text-field
            v-model="formName"
            :label="t('siemCenter.settings.catalog.colName')"
            :disabled="!!editingName"
            density="comfortable"
            class="mb-2"
            hint="system-lifecycle"
            persistent-hint
          />
          <v-combobox
            :model-value="formChannel"
            :items="channelOptions"
            item-title="title"
            item-value="value"
            :label="t('siemCenter.settings.catalog.colChannel')"
            :hint="t('siemCenter.settings.catalog.channelHint')"
            persistent-hint
            density="comfortable"
            class="mb-2"
            clearable
            @update:model-value="onFormChannelUpdate"
          />
          <v-radio-group
            v-model="formSelectionMode"
            :label="t('siemCenter.settings.catalog.colMode')"
            density="compact"
            class="mb-2"
          >
            <v-radio
              v-for="opt in selectionModeOptions"
              :key="opt.value"
              :label="opt.title"
              :value="opt.value"
            >
              <template #label>
                <div>
                  <div class="text-body-2">{{ opt.title }}</div>
                  <div class="text-caption text-medium-emphasis">{{ opt.subtitle }}</div>
                </div>
              </template>
            </v-radio>
          </v-radio-group>
          <div v-if="formSelectionMode === 'selected'" class="d-flex flex-wrap ga-2 mb-2">
            <v-btn size="x-small" variant="tonal" :disabled="!knownIdOptions.length" @click="selectAllKnown">
              {{ t('siemCenter.settings.catalog.selectAllIds') }}
            </v-btn>
            <v-btn size="x-small" variant="text" @click="clearKnown">
              {{ t('siemCenter.settings.catalog.clearIds') }}
            </v-btn>
          </div>
          <div v-else class="d-flex flex-wrap ga-2 mb-2">
            <v-btn size="x-small" variant="text" @click="clearKnown">
              {{ t('siemCenter.settings.catalog.clearIds') }}
            </v-btn>
          </div>
          <v-select
            :model-value="formSelectedIds"
            :items="knownIdOptions"
            item-title="title"
            item-value="value"
            :label="
              formSelectionMode === 'all'
                ? t('siemCenter.settings.catalog.knownExcludeIds')
                : t('siemCenter.settings.catalog.knownIds')
            "
            :disabled="!knownIdOptions.length"
            multiple
            chips
            closable-chips
            density="comfortable"
            class="mb-2"
            @update:model-value="onSelectedIdsUpdate"
          />
          <v-text-field
            v-model="formExtraIds"
            :label="
              formSelectionMode === 'all'
                ? t('siemCenter.settings.catalog.extraExcludeIds')
                : t('siemCenter.settings.catalog.extraIds')
            "
            density="comfortable"
            hint="1000, 1001"
            persistent-hint
            class="mb-2"
          />
          <v-switch
            v-model="formIsDefault"
            :label="t('siemCenter.settings.catalog.defaultSwitch')"
            color="primary"
            density="compact"
            hide-details
          />
          <p class="text-caption text-medium-emphasis mt-3 mb-0">
            {{ t('siemCenter.settings.catalog.parserLater') }}
          </p>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="dialogOpen = false">
            {{ t('siemCenter.settings.catalog.cancel') }}
          </v-btn>
          <v-btn color="primary" :loading="saving" @click="saveForm">
            {{ t('siemCenter.settings.catalog.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="!!deleteTarget" max-width="420" @update:model-value="(v) => { if (!v) deleteTarget = null; }">
      <v-card v-if="deleteTarget">
        <v-card-title>{{ t('siemCenter.settings.catalog.deleteTitle') }}</v-card-title>
        <v-card-text>
          {{ t('siemCenter.settings.catalog.deleteConfirm', { name: deleteTarget.name }) }}
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteTarget = null">
            {{ t('siemCenter.settings.catalog.cancel') }}
          </v-btn>
          <v-btn color="error" :loading="saving" @click="confirmDelete">
            {{ t('siemCenter.settings.catalog.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
