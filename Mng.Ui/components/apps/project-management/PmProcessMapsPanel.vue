<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import DiDrawioEditorDialog from '@/components/apps/document-intelligence/DiDrawioEditorDialog.vue';
import DiFilePreviewDialog from '@/components/apps/document-intelligence/DiFilePreviewDialog.vue';
import PmPickDocumentDialog from '@/components/apps/project-management/PmPickDocumentDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  diDownloadResource,
  diGetById,
} from '@/services/documentIntelligenceService';
import {
  pmCreateProcessMap,
  pmDeleteProcessMap,
  pmUpdateProcessMap,
} from '@/services/projectManagementService';
import type { DiResource } from '@/types/apps/documentIntelligence';
import type {
  PmProcessMap,
  PmProcessMapKind,
  PmProcessMapStatus,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { diCreateBlankDrawio } from '@/utils/diDrawio';
import { diPageResourceLabel } from '@/utils/diPageResource';
import { ensureProjectUploadsFolder } from '@/utils/pmProjectLibrary';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  projectCode: string;
  hubFolderId?: string | null;
  items: PmProcessMap[];
  wbs: PmWbsItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
  hubReady: [id: string];
  openInLibrary: [resourceId: string];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const statusFilter = ref<'all' | 'open' | 'incomplete' | 'current'>('all');
const dialog = ref(false);
const pickOpen = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmProcessMap | null>(null);
const deleting = ref(false);
const promotingId = ref<string | null>(null);
const drawingId = ref<string | null>(null);
const editorOpen = ref(false);
const editorResourceId = ref<string | null>(null);
const editorTitle = ref('');
const previewOpen = ref(false);
const previewResource = ref<DiResource | null>(null);
const resourceNames = ref<Record<string, string>>({});

const KIND_META: Record<PmProcessMapKind, { icon: string; color: string }> = {
  procedure: { icon: 'mdi-file-document-outline', color: 'info' },
  workflow: { icon: 'mdi-sitemap', color: 'primary' },
  org: { icon: 'mdi-account-group-outline', color: 'secondary' },
  other: { icon: 'mdi-vector-polyline', color: 'default' },
};

const STATUS_META: Record<PmProcessMapStatus, { icon: string; color: string }> = {
  draft: { icon: 'mdi-pencil-outline', color: 'info' },
  current: { icon: 'mdi-check-decagram-outline', color: 'success' },
  superseded: { icon: 'mdi-archive-outline', color: 'warning' },
};

const form = ref({
  name: '',
  kind: 'procedure' as PmProcessMapKind,
  resourceId: '',
  resourceName: '',
  wbsId: '',
  status: 'draft' as PmProcessMapStatus,
  note: '',
});

const kindChoices = computed(() =>
  (['procedure', 'workflow', 'org', 'other'] as PmProcessMapKind[]).map((value) => ({
    value,
    title: t(`projectManagement.processMap.kind.${value}`),
    hint: t(`projectManagement.processMap.dialog.kindHint.${value}`),
    icon: KIND_META[value].icon,
    color: KIND_META[value].color,
  })),
);

const statusChoices = computed(() =>
  (['draft', 'current', 'superseded'] as PmProcessMapStatus[]).map((value) => ({
    value,
    title: t(`projectManagement.processMap.status.${value}`),
    hint: t(`projectManagement.processMap.dialog.statusHint.${value}`),
    icon: STATUS_META[value].icon,
    color: STATUS_META[value].color,
  })),
);

const statusMeta = computed(() => STATUS_META[form.value.status] || STATUS_META.draft);

const wbsItems = computed(() => [
  { title: t('projectManagement.processMap.projectLevel'), value: '' },
  ...props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
]);

const openCount = computed(() => props.items.filter((row) => row.open).length);
const incompleteCount = computed(() => props.items.filter((row) => row.incomplete).length);
const currentCount = computed(() => props.items.filter((row) => row.current).length);

const rows = computed(() => {
  if (statusFilter.value === 'open') return props.items.filter((row) => row.open);
  if (statusFilter.value === 'incomplete') return props.items.filter((row) => row.incomplete);
  if (statusFilter.value === 'current') return props.items.filter((row) => row.current);
  return props.items;
});

const headers = computed(() => [
  { title: t('projectManagement.fields.name'), key: 'name', minWidth: 180 },
  { title: t('projectManagement.fields.kind'), key: 'kind', width: 120 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 140 },
  { title: t('projectManagement.processMap.resource'), key: 'resourceId', minWidth: 140 },
  { title: t('projectManagement.actions'), key: 'actions', width: 280, sortable: false, align: 'end' as const },
]);

const canSave = computed(() => {
  if (!form.value.name.trim()) return false;
  if (form.value.status === 'current' && !form.value.resourceId.trim()) return false;
  if (form.value.status === 'superseded' && !form.value.note.trim()) return false;
  return true;
});

function wbsName(id?: string | null) {
  if (!id) return t('projectManagement.processMap.projectLevel');
  const row = props.wbs.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function statusLabel(status?: string | null) {
  const key = `projectManagement.processMap.status.${status || 'draft'}`;
  const label = t(key);
  return label === key ? status || 'draft' : label;
}

function kindLabel(kind?: string | null) {
  const key = `projectManagement.processMap.kind.${kind || 'procedure'}`;
  const label = t(key);
  return label === key ? kind || 'procedure' : label;
}

function statusColor(row: PmProcessMap) {
  if (row.status === 'superseded') return 'warning';
  if (row.incomplete) return 'warning';
  if (row.current) return 'success';
  return 'info';
}

function documentLabel(id?: string | null) {
  if (!id) return '—';
  return resourceNames.value[id] || id.slice(0, 8);
}

async function openPreview(id: string) {
  try {
    previewResource.value = await diGetById(id);
    previewOpen.value = true;
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.preview');
  }
}

function onPreviewOpenInLibrary(resource: DiResource) {
  previewOpen.value = false;
  emit('openInLibrary', resource.id);
}

async function downloadPreview(resource: DiResource) {
  try {
    const { blob, fileName } = await diDownloadResource(resource.id, resource.fileName || resource.name);
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName || resource.fileName || resource.name || 'dosya';
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  } catch (error) {
    panelError(error, 'documentIntelligence.errors.download');
  }
}

function openCreate() {
  editingId.value = null;
  form.value = {
    name: '',
    kind: 'procedure',
    resourceId: '',
    resourceName: '',
    wbsId: '',
    status: 'draft',
    note: '',
  };
  dialog.value = true;
}

function openEdit(row: PmProcessMap) {
  editingId.value = row.id;
  form.value = {
    name: row.name,
    kind: (row.kind as PmProcessMapKind) || 'procedure',
    resourceId: row.resourceId || '',
    resourceName: row.resourceId ? documentLabel(row.resourceId) : '',
    wbsId: row.wbsId || '',
    status: (row.status as PmProcessMapStatus) || 'draft',
    note: row.note || '',
  };
  dialog.value = true;
  if (row.resourceId) void resolveFormDocumentName(row.id, row.resourceId);
}

async function resolveFormDocumentName(rowId: string, resourceId: string) {
  try {
    const resource = await diGetById(resourceId);
    if (editingId.value !== rowId) return;
    const name = resource.fileName || diPageResourceLabel(resource) || resource.name || resourceId.slice(0, 8);
    form.value.resourceName = name;
    rememberResourceName(resourceId, name);
  } catch {
    /* keep fallback label */
  }
}

function selectKind(kind: PmProcessMapKind) {
  form.value.kind = kind;
}

function selectStatus(status: PmProcessMapStatus) {
  form.value.status = status;
}

function rememberResourceName(id: string, name: string) {
  resourceNames.value = { ...resourceNames.value, [id]: name };
}

function pickDocument(resource: DiResource) {
  const name = resource.fileName || diPageResourceLabel(resource) || resource.name || resource.id.slice(0, 8);
  form.value.resourceId = resource.id;
  form.value.resourceName = name;
  rememberResourceName(resource.id, name);
}

function clearDocument() {
  form.value.resourceId = '';
  form.value.resourceName = '';
}

function payload() {
  return {
    name: form.value.name.trim(),
    kind: form.value.kind,
    resourceId: form.value.resourceId.trim() || null,
    wbsId: form.value.wbsId || null,
    status: form.value.status,
    note: form.value.note.trim() || null,
  };
}

async function save() {
  if (!canSave.value) return;
  saving.value = true;
  try {
    if (editingId.value) await pmUpdateProcessMap(editingId.value, payload());
    else await pmCreateProcessMap(props.projectId, payload());
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.processMapSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function markCurrent(row: PmProcessMap) {
  if (!row.resourceId) return;
  promotingId.value = row.id;
  try {
    await pmUpdateProcessMap(row.id, { status: 'current', resourceId: row.resourceId });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.processMapSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    promotingId.value = null;
  }
}

async function createBlankDrawio(title: string) {
  const { hubId, folderId } = await ensureProjectUploadsFolder(
    props.projectId,
    props.projectCode,
    props.hubFolderId,
  );
  emit('hubReady', hubId);
  return diCreateBlankDrawio(folderId, title);
}

function openDrawioEditor(resourceId: string, title: string) {
  editorResourceId.value = resourceId;
  editorTitle.value = title;
  editorOpen.value = true;
}

async function startDraw(row: PmProcessMap) {
  drawingId.value = row.id;
  try {
    let resourceId = (row.resourceId || '').trim();
    if (!resourceId) {
      const created = await createBlankDrawio(row.name);
      resourceId = created.id;
      await pmUpdateProcessMap(row.id, { resourceId });
      emit('changed');
    }
    openDrawioEditor(resourceId, row.name);
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    drawingId.value = null;
  }
}

async function drawFromDialog() {
  if (!form.value.name.trim()) return;
  drawingId.value = editingId.value || 'new';
  try {
    let id = editingId.value;
    let resourceId = form.value.resourceId.trim();
    if (!id) {
      const created = await pmCreateProcessMap(props.projectId, {
        ...payload(),
        resourceId: resourceId || null,
        status: form.value.status === 'current' && !resourceId ? 'draft' : form.value.status,
      });
      id = created.id;
      resourceId = created.resourceId || resourceId;
    }
    if (!resourceId) {
      const file = await createBlankDrawio(form.value.name.trim());
      resourceId = file.id;
      form.value.resourceName = file.fileName || file.name || form.value.name.trim();
      rememberResourceName(resourceId, form.value.resourceName);
      await pmUpdateProcessMap(id, { resourceId });
    }
    form.value.resourceId = resourceId;
    editingId.value = id;
    dialog.value = false;
    emit('changed');
    openDrawioEditor(resourceId, form.value.name.trim());
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    drawingId.value = null;
  }
}

async function executeDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteProcessMap(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.processMapDeleted'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  } finally {
    deleting.value = false;
  }
}

function onDeleteDialog(open: boolean) {
  if (!open) deleteTarget.value = null;
}

watch(
  () => props.items,
  async (rows) => {
    const ids = [...new Set(rows.map((row) => row.resourceId).filter((id): id is string => Boolean(id)))];
    for (const id of ids) {
      if (resourceNames.value[id]) continue;
      try {
        const resource = await diGetById(id);
        resourceNames.value = {
          ...resourceNames.value,
          [id]: resource.fileName || resource.name || id.slice(0, 8),
        };
      } catch {
        resourceNames.value = { ...resourceNames.value, [id]: id.slice(0, 8) };
      }
    }
  },
  { immediate: true },
);
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between flex-wrap ga-3 mb-4">
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.processMap.hint') }}</div>
      <v-btn color="primary" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.processMap.new') }}
      </v-btn>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip size="small" color="info" variant="tonal">
        {{ t('projectManagement.processMap.open') }} · {{ openCount }}
      </v-chip>
      <v-chip size="small" :color="incompleteCount ? 'warning' : 'default'" variant="tonal">
        {{ t('projectManagement.processMap.incomplete') }} · {{ incompleteCount }}
      </v-chip>
      <v-chip size="small" :color="currentCount ? 'success' : 'default'" variant="tonal">
        {{ t('projectManagement.processMap.current') }} · {{ currentCount }}
      </v-chip>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip :variant="statusFilter === 'all' ? 'flat' : 'tonal'" @click="statusFilter = 'all'">
        {{ t('projectManagement.processMap.filterAll') }}
      </v-chip>
      <v-chip color="info" :variant="statusFilter === 'open' ? 'flat' : 'tonal'" @click="statusFilter = 'open'">
        {{ t('projectManagement.processMap.open') }}
      </v-chip>
      <v-chip color="warning" :variant="statusFilter === 'incomplete' ? 'flat' : 'tonal'" @click="statusFilter = 'incomplete'">
        {{ t('projectManagement.processMap.incomplete') }}
      </v-chip>
      <v-chip color="success" :variant="statusFilter === 'current' ? 'flat' : 'tonal'" @click="statusFilter = 'current'">
        {{ t('projectManagement.processMap.current') }}
      </v-chip>
    </div>

    <v-data-table
      :headers="headers"
      :items="rows"
      :loading="loading"
      item-value="id"
      density="comfortable"
      class="rounded-lg border"
      hide-default-footer
      :items-per-page="-1"
    >
      <template #item.name="{ item }">
        <div>
          {{ item.name }}
          <div class="text-caption text-medium-emphasis">{{ wbsName(item.wbsId) }}</div>
        </div>
      </template>
      <template #item.kind="{ item }">
        {{ kindLabel(item.kind) }}
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item)" variant="tonal">
          {{ statusLabel(item.status) }}
        </v-chip>
      </template>
      <template #item.resourceId="{ item }">
        <v-btn
          v-if="item.resourceId"
          variant="text"
          size="small"
          class="text-none px-1 text-primary"
          @click="openPreview(item.resourceId)"
        >
          {{ documentLabel(item.resourceId) }}
        </v-btn>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
      <template #item.actions="{ item }">
        <div class="d-flex justify-end ga-1">
          <v-btn
            v-if="item.status === 'draft' && item.resourceId"
            size="small"
            variant="text"
            color="success"
            :loading="promotingId === item.id"
            @click="markCurrent(item)"
          >
            {{ t('projectManagement.processMap.markCurrent') }}
          </v-btn>
          <v-btn
            size="small"
            variant="text"
            color="primary"
            :loading="drawingId === item.id"
            @click="startDraw(item)"
          >
            {{ item.resourceId ? t('projectManagement.processMap.editDiagram') : t('projectManagement.processMap.draw') }}
          </v-btn>
          <v-btn size="small" variant="text" @click="openEdit(item)">{{ t('projectManagement.edit') }}</v-btn>
          <v-btn icon size="small" variant="text" color="error" @click="deleteTarget = item">
            <TrashIcon size="18" />
          </v-btn>
        </div>
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.processMap.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="720" scrollable>
      <v-card rounded="lg">
        <v-card-title class="d-flex align-start ga-3 px-6 py-4">
          <v-avatar :color="statusMeta.color" variant="tonal" rounded="lg">
            <v-icon :icon="editingId ? 'mdi-pencil-outline' : statusMeta.icon" />
          </v-avatar>
          <div class="flex-grow-1">
            <div>{{ editingId ? t('projectManagement.processMap.edit') : t('projectManagement.processMap.new') }}</div>
            <div class="text-body-2 text-medium-emphasis font-weight-regular mt-1">
              {{
                editingId
                  ? t('projectManagement.processMap.dialog.subtitleEdit')
                  : t('projectManagement.processMap.dialog.subtitleNew')
              }}
            </div>
          </div>
        </v-card-title>
        <v-divider />
        <v-card-text class="d-flex flex-column ga-5 px-6 py-5">
          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.processMap.dialog.sectionIdentity') }}</div>
            <v-text-field
              v-model="form.name"
              :label="t('projectManagement.fields.name')"
              density="comfortable"
              hide-details="auto"
              prepend-inner-icon="mdi-format-title"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-2">{{ t('projectManagement.processMap.dialog.sectionKind') }}</div>
            <div class="pm-process-card-grid">
              <v-card
                v-for="choice in kindChoices"
                :key="choice.value"
                :variant="form.kind === choice.value ? 'tonal' : 'outlined'"
                :color="form.kind === choice.value ? choice.color : undefined"
                rounded="lg"
                class="pm-process-card pa-3"
                role="button"
                @click="selectKind(choice.value)"
              >
                <div class="d-flex align-start ga-3">
                  <v-icon :icon="choice.icon" size="22" />
                  <div>
                    <div class="font-weight-medium">{{ choice.title }}</div>
                    <div class="text-caption text-medium-emphasis">{{ choice.hint }}</div>
                  </div>
                </div>
              </v-card>
            </div>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.processMap.dialog.sectionDoc') }}</div>
            <p class="text-caption text-medium-emphasis mb-3">
              {{ t('projectManagement.processMap.dialog.sectionDocHint') }}
            </p>
            <div v-if="form.resourceId" class="d-flex align-center ga-2 flex-wrap mb-3">
              <v-chip
                size="small"
                color="primary"
                variant="tonal"
                prepend-icon="mdi-vector-polyline"
              >
                {{ form.resourceName || documentLabel(form.resourceId) }}
              </v-chip>
              <v-btn size="small" variant="text" class="text-none" @click="clearDocument">
                {{ t('projectManagement.decision.clearDocument') }}
              </v-btn>
            </div>
            <div v-else class="text-caption text-medium-emphasis mb-3">
              {{ t('projectManagement.processMap.dialog.noDocument') }}
            </div>
            <div class="d-flex flex-wrap ga-2">
              <v-btn variant="tonal" class="text-none" prepend-icon="mdi-folder-open-outline" @click="pickOpen = true">
                {{ t('projectManagement.pickDocument.open') }}
              </v-btn>
              <v-btn
                variant="tonal"
                color="primary"
                class="text-none"
                prepend-icon="mdi-vector-polyline-plus"
                :loading="drawingId === (editingId || 'new')"
                :disabled="!form.name.trim()"
                @click="drawFromDialog"
              >
                {{ form.resourceId ? t('projectManagement.processMap.editDiagram') : t('projectManagement.processMap.draw') }}
              </v-btn>
            </div>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.processMap.dialog.sectionWhere') }}</div>
            <v-select
              v-model="form.wbsId"
              :items="wbsItems"
              :label="t('projectManagement.fields.wbsCode')"
              :hint="t('projectManagement.processMap.dialog.wbsHint')"
              persistent-hint
              density="comfortable"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-2">{{ t('projectManagement.processMap.dialog.sectionStatus') }}</div>
            <div class="pm-process-card-grid pm-process-card-grid--status">
              <v-card
                v-for="choice in statusChoices"
                :key="choice.value"
                :variant="form.status === choice.value ? 'tonal' : 'outlined'"
                :color="form.status === choice.value ? choice.color : undefined"
                rounded="lg"
                class="pm-process-card pa-3"
                role="button"
                @click="selectStatus(choice.value)"
              >
                <div class="d-flex align-start ga-3">
                  <v-icon :icon="choice.icon" size="22" />
                  <div>
                    <div class="font-weight-medium">{{ choice.title }}</div>
                    <div class="text-caption text-medium-emphasis">{{ choice.hint }}</div>
                  </div>
                </div>
              </v-card>
            </div>
            <v-alert
              v-if="form.status === 'current' && !form.resourceId.trim()"
              class="mt-3"
              color="warning"
              variant="tonal"
              density="compact"
              icon="mdi-alert"
            >
              {{ t('projectManagement.processMap.dialog.currentNeedsDoc') }}
            </v-alert>
            <v-alert
              v-else-if="form.status === 'superseded' && !form.note.trim()"
              class="mt-3"
              color="warning"
              variant="tonal"
              density="compact"
              icon="mdi-alert"
            >
              {{ t('projectManagement.processMap.dialog.supersedeNeedsNote') }}
            </v-alert>
            <v-alert
              v-else-if="!form.resourceId.trim()"
              class="mt-3"
              color="warning"
              variant="tonal"
              density="compact"
              icon="mdi-alert"
            >
              {{ t('projectManagement.processMap.dialog.incompleteNow') }}
            </v-alert>
            <v-alert
              v-else
              class="mt-3"
              color="primary"
              variant="tonal"
              density="compact"
              icon="mdi-information-outline"
            >
              {{
                form.status === 'current'
                  ? t('projectManagement.processMap.dialog.officialReady')
                  : t('projectManagement.processMap.dialog.draftReady')
              }}
            </v-alert>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.processMap.dialog.sectionNote') }}</div>
            <v-textarea
              v-model="form.note"
              :label="t('projectManagement.processMap.note')"
              :hint="t('projectManagement.processMap.dialog.noteHint')"
              persistent-hint
              density="comfortable"
              rows="2"
              auto-grow
            />
          </section>
        </v-card-text>
        <v-divider />
        <v-card-actions class="px-6 py-3">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" :disabled="!canSave" @click="save">
            {{ t('projectManagement.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="Boolean(deleteTarget)" max-width="440" @update:model-value="onDeleteDialog">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.processMap.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.processMap.deleteConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteTarget = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="executeDelete">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <PmPickDocumentDialog
      v-model="pickOpen"
      :project-id="projectId"
      :project-code="projectCode"
      :hub-folder-id="hubFolderId"
      @pick="pickDocument"
      @hub-ready="emit('hubReady', $event)"
    />

    <DiDrawioEditorDialog
      v-model="editorOpen"
      :resource-id="editorResourceId"
      :title="editorTitle"
      @saved="emit('changed')"
    />

    <DiFilePreviewDialog
      v-model="previewOpen"
      :resource="previewResource"
      show-open-in-library
      @download="downloadPreview"
      @updated="previewResource = $event"
      @open-in-library="onPreviewOpenInLibrary"
    />
  </div>
</template>

<style scoped>
.pm-process-card-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.pm-process-card-grid--status {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.pm-process-card {
  cursor: pointer;
}

@media (max-width: 600px) {
  .pm-process-card-grid,
  .pm-process-card-grid--status {
    grid-template-columns: 1fr;
  }
}
</style>
