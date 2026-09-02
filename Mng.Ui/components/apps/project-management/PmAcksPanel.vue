<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreateAck,
  pmDateInput,
  pmDatePayload,
  pmDeleteAck,
  pmUpdateAck,
} from '@/services/projectManagementService';
import type { PmAckStatus, PmAcknowledgement, PmWbsItem } from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  items: PmAcknowledgement[];
  wbs: PmWbsItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const statusFilter = ref<'all' | 'pending' | 'overdue'>('all');
const dialog = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmAcknowledgement | null>(null);
const deleting = ref(false);
const acknowledgingId = ref<string | null>(null);

const form = ref({
  title: '',
  resourceId: '',
  versionLabel: '',
  personName: '',
  personId: '',
  wbsId: '',
  status: 'pending' as PmAckStatus,
  dueDate: '',
  note: '',
});

const statusItems = computed(() => [
  { title: t('projectManagement.ack.status.pending'), value: 'pending' },
  { title: t('projectManagement.ack.status.acknowledged'), value: 'acknowledged' },
  { title: t('projectManagement.ack.status.waived'), value: 'waived' },
]);

const wbsItems = computed(() => [
  { title: t('projectManagement.ack.projectLevel'), value: '' },
  ...props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
]);

const pendingCount = computed(() => props.items.filter((row) => row.pending).length);
const overdueCount = computed(() => props.items.filter((row) => row.overdue).length);

const rows = computed(() => {
  if (statusFilter.value === 'pending') return props.items.filter((row) => row.pending);
  if (statusFilter.value === 'overdue') return props.items.filter((row) => row.overdue);
  return props.items;
});

const headers = computed(() => [
  { title: t('projectManagement.ack.document'), key: 'title', minWidth: 180 },
  { title: t('projectManagement.ack.person'), key: 'personName', minWidth: 140 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.fields.wbsCode'), key: 'wbs', minWidth: 140 },
  { title: t('projectManagement.ack.dueDate'), key: 'dueDate', width: 120 },
  { title: t('projectManagement.actions'), key: 'actions', width: 180, sortable: false, align: 'end' as const },
]);

const canSave = computed(() => {
  if (!form.value.title.trim() || !form.value.resourceId.trim() || !form.value.personName.trim()) return false;
  if (form.value.status === 'waived' && !form.value.note.trim()) return false;
  return true;
});

function wbsName(id?: string | null) {
  if (!id) return t('projectManagement.ack.projectLevel');
  const row = props.wbs.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function statusLabel(status?: string | null) {
  const key = `projectManagement.ack.status.${status || 'pending'}`;
  const label = t(key);
  return label === key ? status || 'pending' : label;
}

function statusColor(row: PmAcknowledgement) {
  if (row.overdue) return 'error';
  if (row.status === 'acknowledged') return 'success';
  if (row.status === 'waived') return 'warning';
  return 'info';
}

function resourceHref(id: string) {
  return `/apps/document-intelligence/r/${encodeURIComponent(id)}`;
}

function openCreate() {
  editingId.value = null;
  form.value = {
    title: '',
    resourceId: '',
    versionLabel: '',
    personName: '',
    personId: '',
    wbsId: '',
    status: 'pending',
    dueDate: '',
    note: '',
  };
  dialog.value = true;
}

function openEdit(row: PmAcknowledgement) {
  editingId.value = row.id;
  form.value = {
    title: row.title,
    resourceId: row.resourceId,
    versionLabel: row.versionLabel || '',
    personName: row.personName,
    personId: row.personId || '',
    wbsId: row.wbsId || '',
    status: (row.status as PmAckStatus) || 'pending',
    dueDate: pmDateInput(row.dueDate),
    note: row.note || '',
  };
  dialog.value = true;
}

function payload() {
  return {
    resourceId: form.value.resourceId.trim(),
    title: form.value.title.trim(),
    versionLabel: form.value.versionLabel.trim() || null,
    personName: form.value.personName.trim(),
    personId: form.value.personId.trim() || null,
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
    if (editingId.value) await pmUpdateAck(editingId.value, payload());
    else await pmCreateAck(props.projectId, payload());
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.ackSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function acknowledge(row: PmAcknowledgement) {
  acknowledgingId.value = row.id;
  try {
    await pmUpdateAck(row.id, { status: 'acknowledged' });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.ackSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    acknowledgingId.value = null;
  }
}

async function executeDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteAck(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.ackDeleted'),
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
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.ack.hint') }}</div>
      <v-btn color="primary" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.ack.new') }}
      </v-btn>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip size="small" color="info" variant="tonal">
        {{ t('projectManagement.ack.status.pending') }} · {{ pendingCount }}
      </v-chip>
      <v-chip size="small" :color="overdueCount ? 'error' : 'success'" variant="tonal">
        {{ t('projectManagement.ack.overdue') }} · {{ overdueCount }}
      </v-chip>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip :variant="statusFilter === 'all' ? 'flat' : 'tonal'" @click="statusFilter = 'all'">
        {{ t('projectManagement.ack.filterAll') }}
      </v-chip>
      <v-chip color="info" :variant="statusFilter === 'pending' ? 'flat' : 'tonal'" @click="statusFilter = 'pending'">
        {{ t('projectManagement.ack.status.pending') }}
      </v-chip>
      <v-chip color="error" :variant="statusFilter === 'overdue' ? 'flat' : 'tonal'" @click="statusFilter = 'overdue'">
        {{ t('projectManagement.ack.overdue') }}
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
      <template #item.title="{ item }">
        <div>
          <NuxtLink v-if="item.resourceId" :to="resourceHref(item.resourceId)" class="text-primary">
            {{ item.title }}
          </NuxtLink>
          <span v-else>{{ item.title }}</span>
          <div v-if="item.versionLabel" class="text-caption text-medium-emphasis">{{ item.versionLabel }}</div>
        </div>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item)" variant="tonal">
          {{ statusLabel(item.status) }}
        </v-chip>
      </template>
      <template #item.wbs="{ item }">
        {{ wbsName(item.wbsId) }}
      </template>
      <template #item.dueDate="{ item }">
        {{ pmDateInput(item.dueDate) || '—' }}
      </template>
      <template #item.actions="{ item }">
        <div class="d-flex justify-end ga-1">
          <v-btn
            v-if="item.pending"
            size="small"
            variant="text"
            color="success"
            :loading="acknowledgingId === item.id"
            @click="acknowledge(item)"
          >
            {{ t('projectManagement.ack.markRead') }}
          </v-btn>
          <v-btn size="small" variant="text" @click="openEdit(item)">{{ t('projectManagement.edit') }}</v-btn>
          <v-btn icon size="small" variant="text" color="error" @click="deleteTarget = item">
            <TrashIcon size="18" />
          </v-btn>
        </div>
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.ack.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingId ? t('projectManagement.ack.edit') : t('projectManagement.ack.new') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="form.title" :label="t('projectManagement.ack.document')" density="comfortable" />
          <v-text-field v-model="form.resourceId" :label="t('projectManagement.ack.resourceId')" density="comfortable" />
          <v-text-field v-model="form.versionLabel" :label="t('projectManagement.ack.version')" density="comfortable" />
          <v-text-field v-model="form.personName" :label="t('projectManagement.ack.person')" density="comfortable" />
          <v-text-field v-model="form.personId" :label="t('projectManagement.ack.personId')" density="comfortable" />
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
            :label="t('projectManagement.ack.dueDate')"
            density="comfortable"
          />
          <v-textarea v-model="form.note" :label="t('projectManagement.ack.note')" density="comfortable" rows="2" auto-grow />
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
        <v-card-title>{{ t('projectManagement.ack.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.ack.deleteConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteTarget = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="executeDelete">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
