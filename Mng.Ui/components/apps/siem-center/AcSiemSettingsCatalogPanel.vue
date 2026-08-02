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
} from '@/types/apps/eventLogPackageCatalog';

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
const formName = ref('');
const formChannel = ref('System');
const formIsDefault = ref(true);
const formSelectedIds = ref<number[]>([]);
const formExtraIds = ref('');
const deleteTarget = ref<EventLogPackageManageItem | null>(null);
const skipChannelWatch = ref(false);

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
    value: k.id,
  })),
);

const mergedEventIds = computed(() => {
  const fromSelect = [...formSelectedIds.value];
  const extras = formExtraIds.value
    .split(/[\s,;]+/)
    .map((s) => Number(s.trim()))
    .filter((n) => Number.isFinite(n) && n > 0);
  return [...new Set([...fromSelect, ...extras])].sort((a, b) => a - b);
});

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

function applyEventIdsToForm(channel: string, eventIds: number[]) {
  const known = new Set(
    (channels.value.find((c) => c.channel === channel)?.knownEventIds ?? []).map((k) => k.id),
  );
  formSelectedIds.value = eventIds.filter((id) => known.has(id));
  formExtraIds.value = eventIds.filter((id) => !known.has(id)).join(', ');
}

async function load() {
  loading.value = true;
  error.value = null;
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
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  editingName.value = null;
  formName.value = '';
  formChannel.value = channels.value[0]?.channel || 'System';
  formIsDefault.value = true;
  formSelectedIds.value = [];
  formExtraIds.value = '';
  dialogOpen.value = true;
}

function openEdit(item: EventLogPackageManageItem) {
  editingName.value = item.name;
  formName.value = item.name;
  skipChannelWatch.value = true;
  formChannel.value = item.channel;
  formIsDefault.value = item.isDefault;
  applyEventIdsToForm(item.channel, item.eventIds);
  dialogOpen.value = true;
  queueMicrotask(() => {
    skipChannelWatch.value = false;
  });
}

function applyPreset(preset: EventLogPackagePreset) {
  if (editingName.value) return;
  skipChannelWatch.value = true;
  formName.value = preset.suggestedName || preset.id;
  formChannel.value = preset.channel;
  formIsDefault.value = preset.isDefault;
  applyEventIdsToForm(preset.channel, preset.eventIds);
  queueMicrotask(() => {
    skipChannelWatch.value = false;
  });
}

watch(formChannel, () => {
  if (skipChannelWatch.value) return;
  const allowed = new Set(knownIdsForChannel.value.map((k) => k.id));
  formSelectedIds.value = formSelectedIds.value.filter((id) => allowed.has(id));
});

function selectAllKnown() {
  formSelectedIds.value = knownIdsForChannel.value.map((k) => k.id);
}

function clearKnown() {
  formSelectedIds.value = [];
}

async function saveForm() {
  if (!formName.value.trim()) {
    flash.value = null;
    error.value = t('siemCenter.settings.catalog.nameRequired');
    return;
  }
  if (!String(formChannel.value || '').trim()) {
    error.value = t('siemCenter.settings.catalog.channelRequired');
    return;
  }
  if (mergedEventIds.value.length === 0) {
    error.value = t('siemCenter.settings.catalog.idsRequired');
    return;
  }

  saving.value = true;
  error.value = null;
  flash.value = null;
  const payload = {
    name: formName.value.trim(),
    channel: String(formChannel.value).trim(),
    eventIds: mergedEventIds.value,
    isDefault: formIsDefault.value,
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
    await load();
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
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
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

async function publish() {
  publishing.value = true;
  error.value = null;
  flash.value = null;
  try {
    const res = await publishEventLogPackageCatalog();
    flash.value = t('siemCenter.settings.catalog.published', { version: res.version });
    await load();
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
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
      {{ t('siemCenter.settings.catalog.loadError') }}
      <div class="text-caption mt-1">{{ error }}</div>
    </v-alert>

    <v-skeleton-loader v-if="loading && !managed" type="table" />

    <template v-else-if="managed">
      <v-table density="comfortable" class="mb-2">
        <thead>
          <tr>
            <th>{{ t('siemCenter.settings.catalog.colName') }}</th>
            <th>{{ t('siemCenter.settings.catalog.colChannel') }}</th>
            <th>{{ t('siemCenter.settings.catalog.colEventIds') }}</th>
            <th>{{ t('siemCenter.settings.catalog.colDefault') }}</th>
            <th class="text-end" />
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in managed.items" :key="item.name">
            <td class="font-mono">{{ item.name }}</td>
            <td class="text-body-2">{{ item.channel }}</td>
            <td class="font-mono text-caption">{{ item.eventIds.join(', ') }}</td>
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
          <div class="d-flex flex-wrap ga-2 mb-2">
            <v-btn size="x-small" variant="tonal" :disabled="!knownIdOptions.length" @click="selectAllKnown">
              {{ t('siemCenter.settings.catalog.selectAllIds') }}
            </v-btn>
            <v-btn size="x-small" variant="text" @click="clearKnown">
              {{ t('siemCenter.settings.catalog.clearIds') }}
            </v-btn>
          </div>
          <v-select
            v-model="formSelectedIds"
            :items="knownIdOptions"
            :label="t('siemCenter.settings.catalog.knownIds')"
            :disabled="!knownIdOptions.length"
            multiple
            chips
            closable-chips
            density="comfortable"
            class="mb-2"
          />
          <v-text-field
            v-model="formExtraIds"
            :label="t('siemCenter.settings.catalog.extraIds')"
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

    <v-dialog :model-value="!!deleteTarget" max-width="420" @update:model-value="(v: boolean) => { if (!v) deleteTarget = null; }">
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
