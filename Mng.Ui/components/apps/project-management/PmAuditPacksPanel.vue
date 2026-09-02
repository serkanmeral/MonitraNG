<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreateAuditPack,
  pmDateInput,
  pmDatePayload,
  pmDeleteAuditPack,
  pmUpdateAuditPack,
} from '@/services/projectManagementService';
import type {
  PmAuditPack,
  PmAuditPackKind,
  PmAuditPackStatus,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  items: PmAuditPack[];
  wbs: PmWbsItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const statusFilter = ref<'all' | 'open' | 'incomplete' | 'overdue'>('all');
const dialog = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmAuditPack | null>(null);
const deleting = ref(false);
const issuingId = ref<string | null>(null);

const form = ref({
  name: '',
  kind: 'audit' as PmAuditPackKind,
  wbsId: '',
  status: 'draft' as PmAuditPackStatus,
  dueDate: '',
  resourceText: '',
  recipient: '',
  note: '',
});

const kindItems = computed(() => [
  { title: t('projectManagement.auditPack.kind.audit'), value: 'audit' },
  { title: t('projectManagement.auditPack.kind.customer'), value: 'customer' },
  { title: t('projectManagement.auditPack.kind.internal'), value: 'internal' },
]);

const statusItems = computed(() => [
  { title: t('projectManagement.auditPack.status.draft'), value: 'draft' },
  { title: t('projectManagement.auditPack.status.assembled'), value: 'assembled' },
  { title: t('projectManagement.auditPack.status.issued'), value: 'issued' },
  { title: t('projectManagement.auditPack.status.withdrawn'), value: 'withdrawn' },
]);

const wbsItems = computed(() => [
  { title: t('projectManagement.auditPack.projectLevel'), value: '' },
  ...props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
]);

const openCount = computed(() => props.items.filter((row) => row.open).length);
const incompleteCount = computed(() => props.items.filter((row) => row.incomplete).length);
const overdueCount = computed(() => props.items.filter((row) => row.overdue).length);

const rows = computed(() => {
  if (statusFilter.value === 'open') return props.items.filter((row) => row.open);
  if (statusFilter.value === 'incomplete') return props.items.filter((row) => row.incomplete);
  if (statusFilter.value === 'overdue') return props.items.filter((row) => row.overdue);
  return props.items;
});

const headers = computed(() => [
  { title: t('projectManagement.fields.name'), key: 'name', minWidth: 180 },
  { title: t('projectManagement.fields.kind'), key: 'kind', width: 120 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.auditPack.items'), key: 'itemCount', width: 90 },
  { title: t('projectManagement.auditPack.dueDate'), key: 'dueDate', width: 120 },
  { title: t('projectManagement.actions'), key: 'actions', width: 180, sortable: false, align: 'end' as const },
]);

function parseResourceIds(text: string) {
  return text
    .split(/[\n,;]+/)
    .map((id) => id.trim())
    .filter(Boolean);
}

const canSave = computed(() => {
  if (!form.value.name.trim()) return false;
  if (form.value.status === 'withdrawn' && !form.value.note.trim()) return false;
  if (form.value.status === 'issued' && parseResourceIds(form.value.resourceText).length === 0) return false;
  return true;
});

function wbsName(id?: string | null) {
  if (!id) return t('projectManagement.auditPack.projectLevel');
  const row = props.wbs.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function statusLabel(status?: string | null) {
  const key = `projectManagement.auditPack.status.${status || 'draft'}`;
  const label = t(key);
  return label === key ? status || 'draft' : label;
}

function kindLabel(kind?: string | null) {
  const key = `projectManagement.auditPack.kind.${kind || 'audit'}`;
  const label = t(key);
  return label === key ? kind || 'audit' : label;
}

function statusColor(row: PmAuditPack) {
  if (row.overdue || row.status === 'withdrawn') return row.status === 'withdrawn' ? 'warning' : 'error';
  if (row.status === 'issued') return 'success';
  if (row.incomplete) return 'warning';
  return 'info';
}

function resourceHref(id: string) {
  return `/apps/document-intelligence/r/${encodeURIComponent(id)}`;
}

function openCreate() {
  editingId.value = null;
  form.value = {
    name: '',
    kind: 'audit',
    wbsId: '',
    status: 'draft',
    dueDate: '',
    resourceText: '',
    recipient: '',
    note: '',
  };
  dialog.value = true;
}

function openEdit(row: PmAuditPack) {
  editingId.value = row.id;
  form.value = {
    name: row.name,
    kind: (row.kind as PmAuditPackKind) || 'audit',
    wbsId: row.wbsId || '',
    status: (row.status as PmAuditPackStatus) || 'draft',
    dueDate: pmDateInput(row.dueDate),
    resourceText: (row.resourceIds || []).join('\n'),
    recipient: row.recipient || '',
    note: row.note || '',
  };
  dialog.value = true;
}

function payload() {
  return {
    name: form.value.name.trim(),
    kind: form.value.kind,
    wbsId: form.value.wbsId || null,
    status: form.value.status,
    dueDate: pmDatePayload(form.value.dueDate),
    resourceIds: parseResourceIds(form.value.resourceText),
    recipient: form.value.recipient.trim() || null,
    note: form.value.note.trim() || null,
  };
}

async function save() {
  if (!canSave.value) return;
  saving.value = true;
  try {
    if (editingId.value) await pmUpdateAuditPack(editingId.value, payload());
    else await pmCreateAuditPack(props.projectId, payload());
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.auditPackSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function issue(row: PmAuditPack) {
  if (!row.resourceIds?.length) return;
  issuingId.value = row.id;
  try {
    await pmUpdateAuditPack(row.id, { status: 'issued', resourceIds: row.resourceIds });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.auditPackSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    issuingId.value = null;
  }
}

async function executeDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteAuditPack(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.auditPackDeleted'),
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
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.auditPack.hint') }}</div>
      <v-btn color="primary" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.auditPack.new') }}
      </v-btn>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip size="small" color="info" variant="tonal">
        {{ t('projectManagement.auditPack.status.draft') }} · {{ openCount }}
      </v-chip>
      <v-chip size="small" :color="incompleteCount ? 'warning' : 'default'" variant="tonal">
        {{ t('projectManagement.auditPack.incomplete') }} · {{ incompleteCount }}
      </v-chip>
      <v-chip size="small" :color="overdueCount ? 'error' : 'success'" variant="tonal">
        {{ t('projectManagement.auditPack.overdue') }} · {{ overdueCount }}
      </v-chip>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip :variant="statusFilter === 'all' ? 'flat' : 'tonal'" @click="statusFilter = 'all'">
        {{ t('projectManagement.auditPack.filterAll') }}
      </v-chip>
      <v-chip color="info" :variant="statusFilter === 'open' ? 'flat' : 'tonal'" @click="statusFilter = 'open'">
        {{ t('projectManagement.auditPack.open') }}
      </v-chip>
      <v-chip color="warning" :variant="statusFilter === 'incomplete' ? 'flat' : 'tonal'" @click="statusFilter = 'incomplete'">
        {{ t('projectManagement.auditPack.incomplete') }}
      </v-chip>
      <v-chip color="error" :variant="statusFilter === 'overdue' ? 'flat' : 'tonal'" @click="statusFilter = 'overdue'">
        {{ t('projectManagement.auditPack.overdue') }}
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
      <template #item.itemCount="{ item }">
        <div class="d-flex flex-wrap ga-1">
          <NuxtLink
            v-for="id in (item.resourceIds || []).slice(0, 3)"
            :key="id"
            :to="resourceHref(id)"
            class="text-primary text-caption"
          >
            {{ id.slice(0, 8) }}
          </NuxtLink>
          <span v-if="!item.itemCount" class="text-medium-emphasis">—</span>
        </div>
      </template>
      <template #item.dueDate="{ item }">
        {{ pmDateInput(item.dueDate) || '—' }}
      </template>
      <template #item.actions="{ item }">
        <div class="d-flex justify-end ga-1">
          <v-btn
            v-if="item.open && item.itemCount"
            size="small"
            variant="text"
            color="success"
            :loading="issuingId === item.id"
            @click="issue(item)"
          >
            {{ t('projectManagement.auditPack.markIssued') }}
          </v-btn>
          <v-btn size="small" variant="text" @click="openEdit(item)">{{ t('projectManagement.edit') }}</v-btn>
          <v-btn icon size="small" variant="text" color="error" @click="deleteTarget = item">
            <TrashIcon size="18" />
          </v-btn>
        </div>
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.auditPack.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingId ? t('projectManagement.auditPack.edit') : t('projectManagement.auditPack.new') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="form.name" :label="t('projectManagement.fields.name')" density="comfortable" />
          <v-select v-model="form.kind" :items="kindItems" :label="t('projectManagement.fields.kind')" density="comfortable" />
          <v-select v-model="form.wbsId" :items="wbsItems" :label="t('projectManagement.fields.wbsCode')" density="comfortable" />
          <v-select v-model="form.status" :items="statusItems" :label="t('projectManagement.fields.status')" density="comfortable" />
          <v-text-field v-model="form.recipient" :label="t('projectManagement.auditPack.recipient')" density="comfortable" />
          <v-text-field v-model="form.dueDate" type="date" :label="t('projectManagement.auditPack.dueDate')" density="comfortable" />
          <v-textarea
            v-model="form.resourceText"
            :label="t('projectManagement.auditPack.resourceIds')"
            density="comfortable"
            rows="3"
            auto-grow
          />
          <v-textarea v-model="form.note" :label="t('projectManagement.auditPack.note')" density="comfortable" rows="2" auto-grow />
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
        <v-card-title>{{ t('projectManagement.auditPack.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.auditPack.deleteConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteTarget = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="executeDelete">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
