<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreateStageGate,
  pmDeleteStageGate,
  pmUpdateStageGate,
} from '@/services/projectManagementService';
import type { PmStageGate, PmStageGateStatus, PmWbsItem } from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  gates: PmStageGate[];
  wbs: PmWbsItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const dialog = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmStageGate | null>(null);
const deleting = ref(false);
const newCriterion = ref('');

const form = ref({
  name: '',
  wbsId: '',
  status: 'open' as PmStageGateStatus,
  criteria: [] as string[],
  satisfied: [] as string[],
  note: '',
});

const statusItems = computed(() => [
  { title: t('projectManagement.stageGate.status.open'), value: 'open' },
  { title: t('projectManagement.stageGate.status.passed'), value: 'passed' },
  { title: t('projectManagement.stageGate.status.failed'), value: 'failed' },
  { title: t('projectManagement.stageGate.status.waived'), value: 'waived' },
]);

const wbsItems = computed(() => [
  { title: t('projectManagement.stageGate.noWbs'), value: '' },
  ...props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
]);

const suggestItems = computed(() => [
  t('projectManagement.stageGate.suggest.scope'),
  t('projectManagement.stageGate.suggest.plan'),
  t('projectManagement.stageGate.suggest.scopeChange'),
  t('projectManagement.stageGate.suggest.evidence'),
]);

const headers = computed(() => [
  { title: t('projectManagement.fields.name'), key: 'name', minWidth: 180 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 120 },
  { title: t('projectManagement.stageGate.milestone'), key: 'wbs', minWidth: 160 },
  { title: t('projectManagement.stageGate.criteria'), key: 'criteria', minWidth: 160 },
  { title: t('projectManagement.actions'), key: 'actions', width: 120, sortable: false, align: 'end' as const },
]);

const noteRequired = computed(() => form.value.status === 'failed' || form.value.status === 'waived');
const allMet = computed(() =>
  form.value.criteria.every((item) =>
    form.value.satisfied.some((s) => s.toLocaleLowerCase('tr') === item.toLocaleLowerCase('tr')),
  ),
);
const canSave = computed(() => {
  if (!form.value.name.trim()) return false;
  if (form.value.status === 'passed' && !allMet.value) return false;
  if (noteRequired.value && !form.value.note.trim()) return false;
  return true;
});

function statusLabel(status?: string | null) {
  const key = `projectManagement.stageGate.status.${status || 'open'}`;
  const label = t(key);
  return label === key ? (status || 'open') : label;
}

function statusColor(status?: string | null) {
  if (status === 'passed') return 'success';
  if (status === 'failed') return 'error';
  if (status === 'waived') return 'warning';
  return 'info';
}

function wbsName(id?: string | null) {
  if (!id) return '';
  const row = props.wbs.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function metCount(row: PmStageGate) {
  const criteria = row.criteria || [];
  const satisfied = row.satisfied || [];
  const met = criteria.filter((item) =>
    satisfied.some((s) => s.toLocaleLowerCase('tr') === item.toLocaleLowerCase('tr')),
  ).length;
  return `${met}/${criteria.length}`;
}

function isSatisfied(label: string) {
  return form.value.satisfied.some((s) => s.toLocaleLowerCase('tr') === label.toLocaleLowerCase('tr'));
}

function toggleSatisfied(label: string) {
  if (isSatisfied(label)) {
    form.value.satisfied = form.value.satisfied.filter(
      (s) => s.toLocaleLowerCase('tr') !== label.toLocaleLowerCase('tr'),
    );
  } else {
    form.value.satisfied = [...form.value.satisfied, label];
  }
}

function addCriterion(raw?: string) {
  const label = (raw ?? newCriterion.value).trim();
  if (!label) return;
  if (form.value.criteria.some((item) => item.toLocaleLowerCase('tr') === label.toLocaleLowerCase('tr'))) {
    newCriterion.value = '';
    return;
  }
  form.value.criteria = [...form.value.criteria, label];
  newCriterion.value = '';
}

function removeCriterion(label: string) {
  form.value.criteria = form.value.criteria.filter(
    (item) => item.toLocaleLowerCase('tr') !== label.toLocaleLowerCase('tr'),
  );
  form.value.satisfied = form.value.satisfied.filter(
    (item) => item.toLocaleLowerCase('tr') !== label.toLocaleLowerCase('tr'),
  );
}

function openCreate() {
  editingId.value = null;
  form.value = { name: '', wbsId: '', status: 'open', criteria: [], satisfied: [], note: '' };
  newCriterion.value = '';
  dialog.value = true;
}

function openEdit(row: PmStageGate) {
  editingId.value = row.id;
  form.value = {
    name: row.name,
    wbsId: row.wbsId || '',
    status: (row.status as PmStageGateStatus) || 'open',
    criteria: [...(row.criteria || [])],
    satisfied: [...(row.satisfied || [])],
    note: row.note || '',
  };
  newCriterion.value = '';
  dialog.value = true;
}

async function save() {
  if (!canSave.value) return;
  saving.value = true;
  try {
    const body = {
      name: form.value.name.trim(),
      wbsId: form.value.wbsId || null,
      status: form.value.status,
      criteria: form.value.criteria,
      satisfied: form.value.satisfied,
      note: form.value.note.trim() || null,
    };
    if (editingId.value) await pmUpdateStageGate(editingId.value, body);
    else await pmCreateStageGate(props.projectId, body);
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.gateSaved'),
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
    await pmDeleteStageGate(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.gateDeleted'),
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
    <div class="d-flex align-center justify-space-between mb-3">
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.stageGate.hint') }}</div>
      <v-btn color="primary" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.stageGate.new') }}
      </v-btn>
    </div>

    <v-data-table
      :headers="headers"
      :items="gates"
      :loading="loading"
      item-value="id"
      density="comfortable"
      class="rounded-lg border"
      hide-default-footer
      :items-per-page="-1"
    >
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item.status)" variant="tonal">
          {{ statusLabel(item.status) }}
        </v-chip>
      </template>
      <template #item.wbs="{ item }">
        {{ wbsName(item.wbsId) || '—' }}
      </template>
      <template #item.criteria="{ item }">
        {{ metCount(item) }}
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
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.stageGate.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="640">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingId ? t('projectManagement.stageGate.edit') : t('projectManagement.stageGate.new') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="form.name" :label="t('projectManagement.fields.name')" density="comfortable" />
          <v-select
            v-model="form.wbsId"
            :items="wbsItems"
            :label="t('projectManagement.stageGate.milestone')"
            density="comfortable"
          />
          <v-select
            v-model="form.status"
            :items="statusItems"
            :label="t('projectManagement.fields.status')"
            density="comfortable"
          />
          <div>
            <div class="text-subtitle-2 mb-2">{{ t('projectManagement.stageGate.criteria') }}</div>
            <div class="d-flex flex-wrap ga-1 mb-2">
              <v-chip
                v-for="item in suggestItems"
                :key="item"
                size="x-small"
                variant="outlined"
                @click="addCriterion(item)"
              >
                {{ item }}
              </v-chip>
            </div>
            <div class="d-flex ga-2 mb-2">
              <v-text-field
                v-model="newCriterion"
                :label="t('projectManagement.stageGate.addCriterion')"
                density="comfortable"
                hide-details
                @keyup.enter="addCriterion()"
              />
              <v-btn variant="tonal" @click="addCriterion()">{{ t('projectManagement.stageGate.add') }}</v-btn>
            </div>
            <v-list v-if="form.criteria.length" class="rounded-lg border" density="compact">
              <v-list-item v-for="item in form.criteria" :key="item">
                <v-checkbox
                  :model-value="isSatisfied(item)"
                  :label="item"
                  hide-details
                  density="compact"
                  @update:model-value="toggleSatisfied(item)"
                />
                <template #append>
                  <v-btn icon size="x-small" variant="text" @click="removeCriterion(item)">
                    <TrashIcon size="16" />
                  </v-btn>
                </template>
              </v-list-item>
            </v-list>
            <div v-if="form.status === 'passed' && !allMet" class="text-caption text-error mt-2">
              {{ t('projectManagement.stageGate.passBlocked') }}
            </div>
          </div>
          <v-textarea
            v-model="form.note"
            :label="t('projectManagement.stageGate.note')"
            density="comfortable"
            rows="2"
            auto-grow
            :hint="noteRequired ? t('projectManagement.stageGate.noteRequired') : ''"
            persistent-hint
          />
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
        <v-card-title>{{ t('projectManagement.stageGate.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.stageGate.deleteConfirm') }}</v-card-text>
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
