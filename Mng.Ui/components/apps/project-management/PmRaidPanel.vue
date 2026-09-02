<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreateRaidItem,
  pmDateInput,
  pmDatePayload,
  pmDeleteRaidItem,
  pmUpdateRaidItem,
} from '@/services/projectManagementService';
import type {
  PmRaidItem,
  PmRaidKind,
  PmRaidLevel,
  PmRaidResponse,
  PmRaidStatus,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  items: PmRaidItem[];
  wbs: PmWbsItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const kindFilter = ref<'all' | PmRaidKind>('all');
const dialog = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmRaidItem | null>(null);
const deleting = ref(false);

const form = ref({
  kind: 'risk' as PmRaidKind,
  title: '',
  body: '',
  status: 'open' as PmRaidStatus,
  impact: 'medium' as PmRaidLevel,
  likelihood: 'medium' as PmRaidLevel,
  response: 'none' as PmRaidResponse,
  owner: '',
  dueDate: '',
  wbsIds: [] as string[],
});

const kindItems = computed(() => [
  { title: t('projectManagement.raid.kind.risk'), value: 'risk' },
  { title: t('projectManagement.raid.kind.assumption'), value: 'assumption' },
  { title: t('projectManagement.raid.kind.issue'), value: 'issue' },
  { title: t('projectManagement.raid.kind.dependency'), value: 'dependency' },
]);

const levelItems = computed(() => [
  { title: t('projectManagement.raid.level.low'), value: 'low' },
  { title: t('projectManagement.raid.level.medium'), value: 'medium' },
  { title: t('projectManagement.raid.level.high'), value: 'high' },
]);

const responseItems = computed(() => [
  { title: t('projectManagement.raid.response.none'), value: 'none' },
  { title: t('projectManagement.raid.response.avoid'), value: 'avoid' },
  { title: t('projectManagement.raid.response.mitigate'), value: 'mitigate' },
  { title: t('projectManagement.raid.response.transfer'), value: 'transfer' },
  { title: t('projectManagement.raid.response.accept'), value: 'accept' },
]);

const statusItems = computed(() => {
  const prefix = 'projectManagement.raid.status';
  if (form.value.kind === 'risk') {
    return [
      { title: t(`${prefix}.open`), value: 'open' },
      { title: t(`${prefix}.mitigating`), value: 'mitigating' },
      { title: t(`${prefix}.closed`), value: 'closed' },
    ];
  }
  if (form.value.kind === 'assumption') {
    return [
      { title: t(`${prefix}.open`), value: 'open' },
      { title: t(`${prefix}.validated`), value: 'validated' },
      { title: t(`${prefix}.invalid`), value: 'invalid' },
    ];
  }
  if (form.value.kind === 'issue') {
    return [
      { title: t(`${prefix}.open`), value: 'open' },
      { title: t(`${prefix}.inProgress`), value: 'inProgress' },
      { title: t(`${prefix}.closed`), value: 'closed' },
    ];
  }
  return [
    { title: t(`${prefix}.open`), value: 'open' },
    { title: t(`${prefix}.waiting`), value: 'waiting' },
    { title: t(`${prefix}.resolved`), value: 'resolved' },
  ];
});

const wbsItems = computed(() =>
  props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
);

const filtered = computed(() => {
  if (kindFilter.value === 'all') return props.items;
  return props.items.filter((row) => row.kind === kindFilter.value);
});

const headers = computed(() => [
  { title: t('projectManagement.fields.kind'), key: 'kind', width: 130 },
  { title: t('projectManagement.fields.name'), key: 'title', minWidth: 200 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.raid.impact'), key: 'impact', width: 110 },
  { title: t('projectManagement.raid.owner'), key: 'owner', width: 120 },
  { title: t('projectManagement.actions'), key: 'actions', width: 120, sortable: false, align: 'end' as const },
]);

function kindLabel(kind?: string | null) {
  const key = `projectManagement.raid.kind.${kind || 'risk'}`;
  const label = t(key);
  return label === key ? (kind || 'risk') : label;
}

function statusLabel(status?: string | null) {
  const key = `projectManagement.raid.status.${status || 'open'}`;
  const label = t(key);
  return label === key ? (status || 'open') : label;
}

function levelLabel(level?: string | null) {
  const key = `projectManagement.raid.level.${level || 'medium'}`;
  const label = t(key);
  return label === key ? (level || 'medium') : label;
}

function statusColor(status?: string | null) {
  if (status === 'closed' || status === 'validated' || status === 'resolved') return 'success';
  if (status === 'invalid') return 'error';
  if (status === 'mitigating' || status === 'waiting' || status === 'inProgress') return 'warning';
  return 'info';
}

function impactColor(row: PmRaidItem) {
  if (row.kind === 'risk' && row.elevated) return 'error';
  if (row.impact === 'high') return 'warning';
  return 'default';
}

function onKindChange() {
  form.value.status = 'open';
}

function openCreate() {
  editingId.value = null;
  form.value = {
    kind: kindFilter.value === 'all' ? 'risk' : kindFilter.value,
    title: '',
    body: '',
    status: 'open',
    impact: 'medium',
    likelihood: 'medium',
    response: 'none',
    owner: '',
    dueDate: '',
    wbsIds: [],
  };
  dialog.value = true;
}

function openEdit(row: PmRaidItem) {
  editingId.value = row.id;
  form.value = {
    kind: (row.kind as PmRaidKind) || 'risk',
    title: row.title,
    body: row.body || '',
    status: (row.status as PmRaidStatus) || 'open',
    impact: (row.impact as PmRaidLevel) || 'medium',
    likelihood: (row.likelihood as PmRaidLevel) || 'medium',
    response: (row.response as PmRaidResponse) || 'none',
    owner: row.owner || '',
    dueDate: pmDateInput(row.dueDate),
    wbsIds: [...(row.wbsIds || [])],
  };
  dialog.value = true;
}

async function save() {
  if (!form.value.title.trim()) return;
  saving.value = true;
  try {
    const body = {
      kind: form.value.kind,
      title: form.value.title.trim(),
      body: form.value.body.trim() || null,
      status: form.value.status,
      impact: form.value.impact,
      likelihood: form.value.likelihood,
      response: form.value.response,
      owner: form.value.owner.trim() || null,
      dueDate: pmDatePayload(form.value.dueDate),
      wbsIds: form.value.wbsIds,
    };
    if (editingId.value) await pmUpdateRaidItem(editingId.value, body);
    else await pmCreateRaidItem(props.projectId, body);
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.raidSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function executeDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteRaidItem(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.raidDeleted'),
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
    <div class="d-flex align-center justify-space-between mb-3 ga-3 flex-wrap">
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.raid.hint') }}</div>
      <v-btn color="primary" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.raid.new') }}
      </v-btn>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip :variant="kindFilter === 'all' ? 'flat' : 'tonal'" @click="kindFilter = 'all'">
        {{ t('projectManagement.raid.filterAll') }}
      </v-chip>
      <v-chip
        v-for="item in kindItems"
        :key="item.value"
        :variant="kindFilter === item.value ? 'flat' : 'tonal'"
        @click="kindFilter = item.value"
      >
        {{ item.title }}
      </v-chip>
    </div>

    <v-data-table
      :headers="headers"
      :items="filtered"
      :loading="loading"
      item-value="id"
      density="comfortable"
      class="rounded-lg border"
      hide-default-footer
      :items-per-page="-1"
    >
      <template #item.kind="{ item }">
        {{ kindLabel(item.kind) }}
      </template>
      <template #item.title="{ item }">
        <div>{{ item.title }}</div>
        <div v-if="item.kind === 'risk' && item.score" class="text-caption text-medium-emphasis">
          {{ t('projectManagement.raid.score') }} {{ item.score }}
        </div>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item.status)" variant="tonal">
          {{ statusLabel(item.status) }}
        </v-chip>
      </template>
      <template #item.impact="{ item }">
        <v-chip size="small" :color="impactColor(item)" variant="tonal">
          {{ levelLabel(item.impact) }}
        </v-chip>
      </template>
      <template #item.owner="{ item }">
        {{ item.owner || '—' }}
      </template>
      <template #item.actions="{ item }">
        <div class="d-flex justify-end ga-1">
          <v-btn size="small" variant="text" @click="openEdit(item)">{{ t('projectManagement.edit') }}</v-btn>
          <v-btn icon size="small" variant="text" color="error" @click="deleteTarget = item">
            <TrashIcon size="18" />
          </v-btn>
        </div>
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.raid.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="640">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingId ? t('projectManagement.raid.edit') : t('projectManagement.raid.new') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-select
            v-model="form.kind"
            :items="kindItems"
            :label="t('projectManagement.fields.kind')"
            density="comfortable"
            @update:model-value="onKindChange"
          />
          <v-text-field v-model="form.title" :label="t('projectManagement.fields.name')" density="comfortable" />
          <v-textarea v-model="form.body" :label="t('projectManagement.raid.body')" density="comfortable" rows="3" auto-grow />
          <div class="d-flex ga-3">
            <v-select v-model="form.status" :items="statusItems" :label="t('projectManagement.fields.status')" density="comfortable" />
            <v-select v-model="form.impact" :items="levelItems" :label="t('projectManagement.raid.impact')" density="comfortable" />
          </div>
          <div v-if="form.kind === 'risk'" class="d-flex ga-3">
            <v-select v-model="form.likelihood" :items="levelItems" :label="t('projectManagement.raid.likelihood')" density="comfortable" />
            <v-select v-model="form.response" :items="responseItems" :label="t('projectManagement.raid.responseLabel')" density="comfortable" />
          </div>
          <div class="d-flex ga-3">
            <v-text-field v-model="form.owner" :label="t('projectManagement.raid.owner')" density="comfortable" />
            <v-text-field v-model="form.dueDate" type="date" :label="t('projectManagement.raid.dueDate')" density="comfortable" />
          </div>
          <v-select
            v-model="form.wbsIds"
            :items="wbsItems"
            :label="t('projectManagement.raid.affectedWbs')"
            density="comfortable"
            multiple
            chips
            closable-chips
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" :disabled="!form.title.trim()" @click="save">
            {{ t('projectManagement.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="Boolean(deleteTarget)" max-width="440" @update:model-value="onDeleteDialog">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.raid.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.raid.deleteConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteTarget = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="deleting" @click="executeDelete">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
