<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreateProcessMap,
  pmDeleteProcessMap,
  pmUpdateProcessMap,
} from '@/services/projectManagementService';
import type {
  PmProcessMap,
  PmProcessMapKind,
  PmProcessMapStatus,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  items: PmProcessMap[];
  wbs: PmWbsItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const statusFilter = ref<'all' | 'open' | 'incomplete' | 'current'>('all');
const dialog = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmProcessMap | null>(null);
const deleting = ref(false);
const promotingId = ref<string | null>(null);

const form = ref({
  name: '',
  kind: 'procedure' as PmProcessMapKind,
  resourceId: '',
  wbsId: '',
  status: 'draft' as PmProcessMapStatus,
  note: '',
});

const kindItems = computed(() => [
  { title: t('projectManagement.processMap.kind.procedure'), value: 'procedure' },
  { title: t('projectManagement.processMap.kind.workflow'), value: 'workflow' },
  { title: t('projectManagement.processMap.kind.org'), value: 'org' },
  { title: t('projectManagement.processMap.kind.other'), value: 'other' },
]);

const statusItems = computed(() => [
  { title: t('projectManagement.processMap.status.draft'), value: 'draft' },
  { title: t('projectManagement.processMap.status.current'), value: 'current' },
  { title: t('projectManagement.processMap.status.superseded'), value: 'superseded' },
]);

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
  { title: t('projectManagement.actions'), key: 'actions', width: 200, sortable: false, align: 'end' as const },
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

function resourceHref(id: string) {
  return `/apps/document-intelligence/r/${encodeURIComponent(id)}`;
}

function openCreate() {
  editingId.value = null;
  form.value = { name: '', kind: 'procedure', resourceId: '', wbsId: '', status: 'draft', note: '' };
  dialog.value = true;
}

function openEdit(row: PmProcessMap) {
  editingId.value = row.id;
  form.value = {
    name: row.name,
    kind: (row.kind as PmProcessMapKind) || 'procedure',
    resourceId: row.resourceId || '',
    wbsId: row.wbsId || '',
    status: (row.status as PmProcessMapStatus) || 'draft',
    note: row.note || '',
  };
  dialog.value = true;
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
        <NuxtLink v-if="item.resourceId" :to="resourceHref(item.resourceId)" class="text-primary text-caption">
          {{ item.resourceId.slice(0, 8) }}
        </NuxtLink>
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

    <v-dialog v-model="dialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingId ? t('projectManagement.processMap.edit') : t('projectManagement.processMap.new') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="form.name" :label="t('projectManagement.fields.name')" density="comfortable" />
          <v-select v-model="form.kind" :items="kindItems" :label="t('projectManagement.fields.kind')" density="comfortable" />
          <v-select v-model="form.wbsId" :items="wbsItems" :label="t('projectManagement.fields.wbsCode')" density="comfortable" />
          <v-select v-model="form.status" :items="statusItems" :label="t('projectManagement.fields.status')" density="comfortable" />
          <v-text-field v-model="form.resourceId" :label="t('projectManagement.processMap.resourceId')" density="comfortable" />
          <v-textarea v-model="form.note" :label="t('projectManagement.processMap.note')" density="comfortable" rows="2" auto-grow />
        </v-card-text>
        <v-card-actions>
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
  </div>
</template>
