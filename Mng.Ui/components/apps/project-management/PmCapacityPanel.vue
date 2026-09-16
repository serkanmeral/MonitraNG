<script setup lang="ts">
import { computed, ref, watch } from 'vue';
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

const peopleSearch = ref('');
const peopleFilter = ref<'all' | 'overloaded' | 'ok'>('all');
const peoplePage = ref(1);
const peopleItemsPerPage = ref(25);
const assignmentSearch = ref('');
const assignmentPage = ref(1);
const assignmentItemsPerPage = ref(25);
const expandedPeople = ref<string[]>([]);

const pageSizeOptions = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
  { value: 100, title: '100' },
];

const form = ref({
  wbsId: '',
  name: '',
  role: '',
  plannedHours: 8,
  start: '',
  finish: '',
});

const wbsById = computed(() => {
  const map = new Map<string, PmWbsItem>();
  for (const row of props.wbs) map.set(row.id, row);
  return map;
});

const wbsItems = computed(() =>
  props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
);

const people = computed(() => props.capacity?.people ?? []);
const weeklyHours = computed(() => props.capacity?.weeklyCapacityHours ?? 40);
const overloadedCount = computed(() => people.value.filter((row) => row.overloaded).length);

const filteredPeople = computed(() => {
  const query = peopleSearch.value.trim().toLowerCase();
  return people.value.filter((row) => {
    if (peopleFilter.value === 'overloaded' && !row.overloaded) return false;
    if (peopleFilter.value === 'ok' && row.overloaded) return false;
    if (!query) return true;
    return (row.name || '').toLowerCase().includes(query);
  });
});

const filteredAssignments = computed(() => {
  const query = assignmentSearch.value.trim().toLowerCase();
  if (!query) return props.assignments;
  return props.assignments.filter((row) => {
    const wbs = wbsById.value.get(row.wbsId);
    const haystack = [row.name, row.role, wbs?.wbsCode, wbs?.name, String(row.plannedHours)]
      .filter(Boolean)
      .join(' ')
      .toLowerCase();
    return haystack.includes(query);
  });
});

watch([peopleSearch, peopleFilter], () => {
  peoplePage.value = 1;
  expandedPeople.value = [];
});

watch(assignmentSearch, () => {
  assignmentPage.value = 1;
});

const peopleHeaders = computed(() => [
  { title: t('projectManagement.capacity.resource'), key: 'name', minWidth: 160 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.capacity.hours'), key: 'totalHours', width: 110 },
  { title: t('projectManagement.capacity.unscheduled'), key: 'unscheduledHours', width: 130 },
  { title: t('projectManagement.capacity.weeks'), key: 'weeks', minWidth: 170 },
]);

const assignmentHeaders = computed(() => [
  { title: t('projectManagement.capacity.resource'), key: 'name', minWidth: 140 },
  { title: t('projectManagement.fields.wbsCode'), key: 'wbs', minWidth: 160 },
  { title: t('projectManagement.capacity.hours'), key: 'plannedHours', width: 100 },
  { title: t('projectManagement.capacity.window'), key: 'window', minWidth: 180 },
  { title: t('projectManagement.actions'), key: 'actions', width: 120, sortable: false, align: 'end' as const },
]);

const canSave = computed(() => Boolean(form.value.name.trim() && form.value.wbsId && form.value.plannedHours >= 0));

function wbsName(id?: string | null) {
  if (!id) return '';
  const row = wbsById.value.get(id);
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

function weekLabel(value?: string | null) {
  return pmDateInput(value) || '—';
}

function weekSummary(person: PmCapacityPerson) {
  const weeks = person.weeks?.length ?? 0;
  const over = person.weeks?.filter((week) => week.overloaded).length ?? 0;
  if (weeks === 0 && person.unscheduledHours) {
    return t('projectManagement.capacity.unscheduledHours', { hours: person.unscheduledHours });
  }
  if (over > 0) return t('projectManagement.capacity.weekOverloadedSummary', { weeks, over });
  if (weeks > 0) return t('projectManagement.capacity.weekSummary', { weeks });
  return '—';
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
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-3 ga-3 flex-wrap">
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.capacity.hint') }}</div>
      <v-btn color="primary" :disabled="!wbs.length" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.capacity.new') }}
      </v-btn>
    </div>

    <div v-if="people.length" class="mb-5">
      <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-3">
        <div class="text-subtitle-2">{{ t('projectManagement.capacity.peopleTitle') }}</div>
        <div class="text-caption text-medium-emphasis">
          {{
            t('projectManagement.capacity.summaryLine', {
              overloaded: overloadedCount,
              people: people.length,
              assignments: assignments.length,
            })
          }}
        </div>
      </div>

      <div class="d-flex flex-wrap ga-2 mb-3 align-center">
        <v-text-field
          v-model="peopleSearch"
          :label="t('projectManagement.capacity.search')"
          density="comfortable"
          hide-details
          clearable
          style="max-width: 280px"
        />
        <v-chip :variant="peopleFilter === 'all' ? 'flat' : 'tonal'" @click="peopleFilter = 'all'">
          {{ t('projectManagement.capacity.filterAll') }}
        </v-chip>
        <v-chip
          :variant="peopleFilter === 'overloaded' ? 'flat' : 'tonal'"
          :color="peopleFilter === 'overloaded' ? 'error' : undefined"
          @click="peopleFilter = 'overloaded'"
        >
          {{ t('projectManagement.capacity.overloaded') }}
        </v-chip>
        <v-chip
          :variant="peopleFilter === 'ok' ? 'flat' : 'tonal'"
          :color="peopleFilter === 'ok' ? 'success' : undefined"
          @click="peopleFilter = 'ok'"
        >
          {{ t('projectManagement.capacity.ok') }}
        </v-chip>
      </div>

      <v-data-table
        v-model:page="peoplePage"
        v-model:items-per-page="peopleItemsPerPage"
        v-model:expanded="expandedPeople"
        :headers="peopleHeaders"
        :items="filteredPeople"
        :loading="loading"
        item-value="key"
        show-expand
        density="comfortable"
        class="rounded-lg border"
        :items-per-page-options="pageSizeOptions"
      >
        <template #item.status="{ item }">
          <v-chip size="small" :color="item.overloaded ? 'error' : 'success'" variant="tonal">
            {{ item.overloaded ? t('projectManagement.capacity.overloaded') : t('projectManagement.capacity.ok') }}
          </v-chip>
        </template>
        <template #item.totalHours="{ item }">
          {{ item.totalHours }} / {{ weeklyHours }}{{ t('projectManagement.capacity.hoursUnit') }}
        </template>
        <template #item.unscheduledHours="{ item }">
          {{ item.unscheduledHours ? item.unscheduledHours : '—' }}
        </template>
        <template #item.weeks="{ item }">
          {{ weekSummary(item) }}
        </template>
        <template #expanded-row="{ columns, item }">
          <tr>
            <td :colspan="columns.length" class="py-3">
              <div v-if="item.weeks.length" class="d-flex flex-wrap ga-1">
                <v-chip
                  v-for="week in item.weeks"
                  :key="week.weekStart"
                  size="x-small"
                  :color="week.overloaded ? 'error' : 'default'"
                  variant="tonal"
                >
                  {{ weekLabel(week.weekStart) }} · {{ week.hours }}h
                </v-chip>
              </div>
              <div v-if="item.unscheduledHours" class="text-caption text-medium-emphasis mt-2">
                {{ t('projectManagement.capacity.unscheduledHours', { hours: item.unscheduledHours }) }}
              </div>
              <div v-if="!item.weeks.length && !item.unscheduledHours" class="text-caption text-medium-emphasis">
                —
              </div>
            </td>
          </tr>
        </template>
        <template #no-data>
          <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.capacity.emptyPeople') }}</div>
        </template>
      </v-data-table>
    </div>

    <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-3">
      <div class="text-subtitle-2">{{ t('projectManagement.capacity.assignmentsTitle') }}</div>
      <v-text-field
        v-model="assignmentSearch"
        :label="t('projectManagement.capacity.assignmentSearch')"
        density="comfortable"
        hide-details
        clearable
        style="max-width: 280px"
      />
    </div>

    <v-data-table
      v-model:page="assignmentPage"
      v-model:items-per-page="assignmentItemsPerPage"
      :headers="assignmentHeaders"
      :items="filteredAssignments"
      :loading="loading"
      item-value="id"
      density="comfortable"
      class="rounded-lg border"
      :items-per-page-options="pageSizeOptions"
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
