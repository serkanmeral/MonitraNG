<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreateAssignment,
  pmDateInput,
  pmDatePayload,
  pmDeleteAssignment,
  pmUpdateAssignment,
} from '@/services/projectManagementService';
import type {
  PmCapacityPerson,
  PmProjectCapacity,
  PmResourceAssignment,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  assignments: PmResourceAssignment[];
  capacity: PmProjectCapacity | null;
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
const deleteTarget = ref<PmResourceAssignment | null>(null);
const deleting = ref(false);

const form = ref({
  wbsId: '',
  name: '',
  role: '',
  plannedHours: 8,
  start: '',
  finish: '',
});

const wbsItems = computed(() =>
  props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
);

const people = computed(() => props.capacity?.people ?? []);
const weeklyHours = computed(() => props.capacity?.weeklyCapacityHours ?? 40);

const headers = computed(() => [
  { title: t('projectManagement.capacity.resource'), key: 'name', minWidth: 140 },
  { title: t('projectManagement.fields.wbsCode'), key: 'wbs', minWidth: 160 },
  { title: t('projectManagement.capacity.hours'), key: 'plannedHours', width: 100 },
  { title: t('projectManagement.capacity.window'), key: 'window', minWidth: 180 },
  { title: t('projectManagement.actions'), key: 'actions', width: 120, sortable: false, align: 'end' as const },
]);

const canSave = computed(() => Boolean(form.value.name.trim() && form.value.wbsId && form.value.plannedHours >= 0));

function wbsName(id?: string | null) {
  if (!id) return '';
  const row = props.wbs.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function windowLabel(row: PmResourceAssignment) {
  if (row.unscheduled) return t('projectManagement.capacity.unscheduled');
  const start = pmDateInput(row.effectiveStart);
  const finish = pmDateInput(row.effectiveFinish);
  if (!start && !finish) return t('projectManagement.capacity.unscheduled');
  return `${start || '—'} → ${finish || '—'}`;
}

function openCreate() {
  editingId.value = null;
  const first = props.wbs[0];
  form.value = {
    wbsId: first?.id || '',
    name: '',
    role: '',
    plannedHours: 8,
    start: '',
    finish: '',
  };
  dialog.value = true;
}

function openEdit(row: PmResourceAssignment) {
  editingId.value = row.id;
  form.value = {
    wbsId: row.wbsId,
    name: row.name,
    role: row.role || '',
    plannedHours: row.plannedHours,
    start: pmDateInput(row.start),
    finish: pmDateInput(row.finish),
  };
  dialog.value = true;
}

async function save() {
  if (!canSave.value) return;
  saving.value = true;
  try {
    const body = {
      wbsId: form.value.wbsId,
      name: form.value.name.trim(),
      role: form.value.role.trim() || null,
      plannedHours: Number(form.value.plannedHours) || 0,
      start: pmDatePayload(form.value.start),
      finish: pmDatePayload(form.value.finish),
    };
    if (editingId.value) await pmUpdateAssignment(editingId.value, body);
    else await pmCreateAssignment(props.projectId, body);
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.assignmentSaved'),
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
    await pmDeleteAssignment(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.assignmentDeleted'),
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

function weekLabel(value?: string | null) {
  return pmDateInput(value) || '—';
}

function personLoad(person: PmCapacityPerson) {
  return `${person.totalHours} / ${weeklyHours.value}${t('projectManagement.capacity.hoursUnit')}`;
}
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-3">
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.capacity.hint') }}</div>
      <v-btn color="primary" :disabled="!wbs.length" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.capacity.new') }}
      </v-btn>
    </div>

    <div v-if="people.length" class="d-flex flex-column ga-3 mb-4">
      <div
        v-for="person in people"
        :key="person.key"
        class="rounded-lg border pa-3"
      >
        <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-2">
          <div class="d-flex align-center ga-2">
            <span class="text-subtitle-2">{{ person.name }}</span>
            <v-chip
              size="x-small"
              :color="person.overloaded ? 'error' : 'success'"
              variant="tonal"
            >
              {{ person.overloaded ? t('projectManagement.capacity.overloaded') : t('projectManagement.capacity.ok') }}
            </v-chip>
          </div>
          <span class="text-caption text-medium-emphasis">{{ personLoad(person) }}</span>
        </div>
        <div v-if="person.weeks.length" class="d-flex flex-wrap ga-1">
          <v-chip
            v-for="week in person.weeks"
            :key="week.weekStart"
            size="x-small"
            :color="week.overloaded ? 'error' : 'default'"
            variant="tonal"
          >
            {{ weekLabel(week.weekStart) }} · {{ week.hours }}h
          </v-chip>
        </div>
        <div v-if="person.unscheduledHours" class="text-caption text-medium-emphasis mt-2">
          {{ t('projectManagement.capacity.unscheduledHours', { hours: person.unscheduledHours }) }}
        </div>
      </div>
    </div>

    <v-data-table
      :headers="headers"
      :items="assignments"
      :loading="loading"
      item-value="id"
      density="comfortable"
      class="rounded-lg border"
      hide-default-footer
      :items-per-page="-1"
    >
      <template #item.wbs="{ item }">
        {{ wbsName(item.wbsId) || '—' }}
      </template>
      <template #item.plannedHours="{ item }">
        {{ item.plannedHours }}
      </template>
      <template #item.window="{ item }">
        {{ windowLabel(item) }}
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
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.capacity.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingId ? t('projectManagement.capacity.edit') : t('projectManagement.capacity.new') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="form.name" :label="t('projectManagement.capacity.resource')" density="comfortable" />
          <v-select
            v-model="form.wbsId"
            :items="wbsItems"
            :label="t('projectManagement.fields.wbsCode')"
            density="comfortable"
          />
          <v-text-field v-model="form.role" :label="t('projectManagement.capacity.role')" density="comfortable" />
          <v-text-field
            v-model.number="form.plannedHours"
            type="number"
            min="0"
            :label="t('projectManagement.capacity.hours')"
            density="comfortable"
          />
          <div class="d-flex ga-3">
            <v-text-field
              v-model="form.start"
              type="date"
              :label="t('projectManagement.fields.plannedStart')"
              density="comfortable"
            />
            <v-text-field
              v-model="form.finish"
              type="date"
              :label="t('projectManagement.fields.plannedFinish')"
              density="comfortable"
            />
          </div>
          <div class="text-caption text-medium-emphasis">{{ t('projectManagement.capacity.datesHint') }}</div>
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
        <v-card-title>{{ t('projectManagement.capacity.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.capacity.deleteConfirm') }}</v-card-text>
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
