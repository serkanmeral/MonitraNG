<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreateObligation,
  pmDateInput,
  pmDatePayload,
  pmDeleteObligation,
  pmUpdateObligation,
} from '@/services/projectManagementService';
import type { PmObligation, PmObligationStatus, PmWbsItem } from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  items: PmObligation[];
  wbs: PmWbsItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const statusFilter = ref<'all' | 'open' | 'overdue' | 'unbound'>('all');
const dialog = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmObligation | null>(null);
const deleting = ref(false);
const closingId = ref<string | null>(null);

const form = ref({
  title: '',
  clauseRef: '',
  sourceResourceId: '',
  workItemId: '',
  evidenceResourceId: '',
  wbsId: '',
  status: 'open' as PmObligationStatus,
  dueDate: '',
  note: '',
});

const statusItems = computed(() => [
  { title: t('projectManagement.obligation.status.open'), value: 'open' },
  { title: t('projectManagement.obligation.status.inProgress'), value: 'inProgress' },
  { title: t('projectManagement.obligation.status.satisfied'), value: 'satisfied' },
  { title: t('projectManagement.obligation.status.waived'), value: 'waived' },
]);

const wbsItems = computed(() => [
  { title: t('projectManagement.obligation.projectLevel'), value: '' },
  ...props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
]);

const openCount = computed(() => props.items.filter((row) => row.open).length);
const overdueCount = computed(() => props.items.filter((row) => row.overdue).length);
const unboundCount = computed(() => props.items.filter((row) => row.unbound).length);

const rows = computed(() => {
  if (statusFilter.value === 'open') return props.items.filter((row) => row.open);
  if (statusFilter.value === 'overdue') return props.items.filter((row) => row.overdue);
  if (statusFilter.value === 'unbound') return props.items.filter((row) => row.unbound);
  return props.items;
});

const headers = computed(() => [
  { title: t('projectManagement.obligation.clause'), key: 'clauseRef', width: 110 },
  { title: t('projectManagement.obligation.statement'), key: 'title', minWidth: 180 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.fields.workItem'), key: 'workItemId', minWidth: 120 },
  { title: t('projectManagement.obligation.evidence'), key: 'evidence', width: 90 },
  { title: t('projectManagement.obligation.dueDate'), key: 'dueDate', width: 120 },
  { title: t('projectManagement.actions'), key: 'actions', width: 180, sortable: false, align: 'end' as const },
]);

const canSave = computed(() => {
  if (!form.value.title.trim()) return false;
  if (form.value.status === 'waived' && !form.value.note.trim()) return false;
  if (form.value.status === 'satisfied' && !form.value.evidenceResourceId.trim()) return false;
  return true;
});

function wbsName(id?: string | null) {
  if (!id) return t('projectManagement.obligation.projectLevel');
  const row = props.wbs.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function statusLabel(status?: string | null) {
  const key = `projectManagement.obligation.status.${status || 'open'}`;
  const label = t(key);
  return label === key ? status || 'open' : label;
}

function statusColor(row: PmObligation) {
  if (row.overdue) return 'error';
  if (row.status === 'satisfied') return 'success';
  if (row.status === 'waived') return 'warning';
  if (row.unbound) return 'default';
  return 'info';
}

function resourceHref(id: string) {
  return `/apps/document-intelligence/r/${encodeURIComponent(id)}`;
}

function workItemHref(id: string) {
  return `/apps/operation-core/work-items/${encodeURIComponent(id)}/profile`;
}

function openCreate() {
  editingId.value = null;
  form.value = {
    title: '',
    clauseRef: '',
    sourceResourceId: '',
    workItemId: '',
    evidenceResourceId: '',
    wbsId: '',
    status: 'open',
    dueDate: '',
    note: '',
  };
  dialog.value = true;
}

function openEdit(row: PmObligation) {
  editingId.value = row.id;
  form.value = {
    title: row.title,
    clauseRef: row.clauseRef || '',
    sourceResourceId: row.sourceResourceId || '',
    workItemId: row.workItemId || '',
    evidenceResourceId: row.evidenceResourceId || '',
    wbsId: row.wbsId || '',
    status: (row.status as PmObligationStatus) || 'open',
    dueDate: pmDateInput(row.dueDate),
    note: row.note || '',
  };
  dialog.value = true;
}

function payload() {
  return {
    title: form.value.title.trim(),
    clauseRef: form.value.clauseRef.trim() || null,
    sourceResourceId: form.value.sourceResourceId.trim() || null,
    workItemId: form.value.workItemId.trim() || null,
    evidenceResourceId: form.value.evidenceResourceId.trim() || null,
    wbsId: form.value.wbsId || null,
    status: form.value.status,
    dueDate: pmDatePayload(form.value.dueDate),
    note: form.value.note.trim() || null,
  };
}

async function save() {
  if (!canSave.value) return;
  saving.value = true;
  try {
    if (editingId.value) await pmUpdateObligation(editingId.value, payload());
    else await pmCreateObligation(props.projectId, payload());
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.obligationSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function satisfy(row: PmObligation) {
  if (!row.evidenceResourceId) return;
  closingId.value = row.id;
  try {
    await pmUpdateObligation(row.id, { status: 'satisfied', evidenceResourceId: row.evidenceResourceId });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.obligationSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    closingId.value = null;
  }
}

async function executeDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteObligation(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.obligationDeleted'),
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
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.obligation.hint') }}</div>
      <v-btn color="primary" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.obligation.new') }}
      </v-btn>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip size="small" color="info" variant="tonal">
        {{ t('projectManagement.obligation.status.open') }} · {{ openCount }}
      </v-chip>
      <v-chip size="small" :color="overdueCount ? 'error' : 'success'" variant="tonal">
        {{ t('projectManagement.obligation.overdue') }} · {{ overdueCount }}
      </v-chip>
      <v-chip size="small" :color="unboundCount ? 'warning' : 'default'" variant="tonal">
        {{ t('projectManagement.obligation.unbound') }} · {{ unboundCount }}
      </v-chip>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip :variant="statusFilter === 'all' ? 'flat' : 'tonal'" @click="statusFilter = 'all'">
        {{ t('projectManagement.obligation.filterAll') }}
      </v-chip>
      <v-chip color="info" :variant="statusFilter === 'open' ? 'flat' : 'tonal'" @click="statusFilter = 'open'">
        {{ t('projectManagement.obligation.status.open') }}
      </v-chip>
      <v-chip color="error" :variant="statusFilter === 'overdue' ? 'flat' : 'tonal'" @click="statusFilter = 'overdue'">
        {{ t('projectManagement.obligation.overdue') }}
      </v-chip>
      <v-chip color="warning" :variant="statusFilter === 'unbound' ? 'flat' : 'tonal'" @click="statusFilter = 'unbound'">
        {{ t('projectManagement.obligation.unbound') }}
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
      <template #item.clauseRef="{ item }">
        {{ item.clauseRef || '—' }}
      </template>
      <template #item.title="{ item }">
        <div>
          <NuxtLink v-if="item.sourceResourceId" :to="resourceHref(item.sourceResourceId)" class="text-primary">
            {{ item.title }}
          </NuxtLink>
          <span v-else>{{ item.title }}</span>
          <div class="text-caption text-medium-emphasis">{{ wbsName(item.wbsId) }}</div>
        </div>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item)" variant="tonal">
          {{ statusLabel(item.status) }}
        </v-chip>
      </template>
      <template #item.workItemId="{ item }">
        <NuxtLink v-if="item.workItemId" :to="workItemHref(item.workItemId)" class="text-primary">
          {{ item.workItemId.slice(0, 8) }}
        </NuxtLink>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
      <template #item.evidence="{ item }">
        <NuxtLink v-if="item.evidenceResourceId" :to="resourceHref(item.evidenceResourceId)" class="text-primary">
          {{ t('projectManagement.obligation.hasEvidence') }}
        </NuxtLink>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
      <template #item.dueDate="{ item }">
        {{ pmDateInput(item.dueDate) || '—' }}
      </template>
      <template #item.actions="{ item }">
        <div class="d-flex justify-end ga-1">
          <v-btn
            v-if="item.open && item.evidenceResourceId"
            size="small"
            variant="text"
            color="success"
            :loading="closingId === item.id"
            @click="satisfy(item)"
          >
            {{ t('projectManagement.obligation.markSatisfied') }}
          </v-btn>
          <v-btn size="small" variant="text" @click="openEdit(item)">{{ t('projectManagement.edit') }}</v-btn>
          <v-btn icon size="small" variant="text" color="error" @click="deleteTarget = item">
            <TrashIcon size="18" />
          </v-btn>
        </div>
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.obligation.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingId ? t('projectManagement.obligation.edit') : t('projectManagement.obligation.new') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="form.clauseRef" :label="t('projectManagement.obligation.clause')" density="comfortable" />
          <v-textarea v-model="form.title" :label="t('projectManagement.obligation.statement')" density="comfortable" rows="2" auto-grow />
          <v-text-field v-model="form.sourceResourceId" :label="t('projectManagement.obligation.sourceId')" density="comfortable" />
          <v-text-field v-model="form.workItemId" :label="t('projectManagement.obligation.workItemId')" density="comfortable" />
          <v-text-field v-model="form.evidenceResourceId" :label="t('projectManagement.obligation.evidenceId')" density="comfortable" />
          <v-select
            v-model="form.wbsId"
            :items="wbsItems"
            :label="t('projectManagement.fields.wbsCode')"
            density="comfortable"
          />
          <v-select
            v-model="form.status"
            :items="statusItems"
            :label="t('projectManagement.fields.status')"
            density="comfortable"
          />
          <v-text-field
            v-model="form.dueDate"
            type="date"
            :label="t('projectManagement.obligation.dueDate')"
            density="comfortable"
          />
          <v-textarea v-model="form.note" :label="t('projectManagement.obligation.note')" density="comfortable" rows="2" auto-grow />
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
        <v-card-title>{{ t('projectManagement.obligation.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.obligation.deleteConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteTarget = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="executeDelete">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
