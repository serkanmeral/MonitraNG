<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreateBudgetLine,
  pmDeleteBudgetLine,
  pmUpdateBudgetLine,
} from '@/services/projectManagementService';
import type {
  PmBudgetCategory,
  PmBudgetLine,
  PmProjectBudget,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  lines: PmBudgetLine[];
  budget: PmProjectBudget | null;
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
const deleteTarget = ref<PmBudgetLine | null>(null);
const deleting = ref(false);

const form = ref({
  wbsId: '',
  category: 'labor' as PmBudgetCategory,
  name: '',
  plannedAmount: 0,
  actualAmount: 0,
  currency: 'TRY',
  note: '',
});

const categoryItems = computed(() => [
  { title: t('projectManagement.budget.category.labor'), value: 'labor' },
  { title: t('projectManagement.budget.category.material'), value: 'material' },
  { title: t('projectManagement.budget.category.subcontract'), value: 'subcontract' },
  { title: t('projectManagement.budget.category.other'), value: 'other' },
]);

const wbsItems = computed(() =>
  props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
);

const packages = computed(() => props.budget?.packages ?? []);
const currency = computed(() => props.budget?.currency || 'TRY');

const headers = computed(() => [
  { title: t('projectManagement.budget.line'), key: 'name', minWidth: 160 },
  { title: t('projectManagement.fields.wbsCode'), key: 'wbs', minWidth: 150 },
  { title: t('projectManagement.fields.kind'), key: 'category', width: 130 },
  { title: t('projectManagement.budget.planned'), key: 'plannedAmount', width: 110 },
  { title: t('projectManagement.budget.actual'), key: 'actualAmount', width: 110 },
  { title: t('projectManagement.budget.variance'), key: 'variance', width: 110 },
  { title: t('projectManagement.actions'), key: 'actions', width: 120, sortable: false, align: 'end' as const },
]);

const canSave = computed(() =>
  Boolean(form.value.name.trim() && form.value.wbsId && form.value.plannedAmount >= 0 && form.value.actualAmount >= 0),
);

function wbsName(id?: string | null) {
  if (!id) return '';
  const row = props.wbs.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function categoryLabel(value?: string | null) {
  const key = `projectManagement.budget.category.${value || 'other'}`;
  const label = t(key);
  return label === key ? (value || 'other') : label;
}

function money(value: number, code?: string | null) {
  const cur = code || currency.value;
  return `${Number(value || 0).toLocaleString('tr-TR', { minimumFractionDigits: 0, maximumFractionDigits: 2 })} ${cur}`;
}

function openCreate() {
  editingId.value = null;
  form.value = {
    wbsId: props.wbs[0]?.id || '',
    category: 'labor',
    name: '',
    plannedAmount: 0,
    actualAmount: 0,
    currency: currency.value || 'TRY',
    note: '',
  };
  dialog.value = true;
}

function openEdit(row: PmBudgetLine) {
  editingId.value = row.id;
  form.value = {
    wbsId: row.wbsId,
    category: (row.category as PmBudgetCategory) || 'labor',
    name: row.name,
    plannedAmount: row.plannedAmount,
    actualAmount: row.actualAmount,
    currency: row.currency || 'TRY',
    note: row.note || '',
  };
  dialog.value = true;
}

async function save() {
  if (!canSave.value) return;
  saving.value = true;
  try {
    const body = {
      wbsId: form.value.wbsId,
      category: form.value.category,
      name: form.value.name.trim(),
      plannedAmount: Number(form.value.plannedAmount) || 0,
      actualAmount: Number(form.value.actualAmount) || 0,
      currency: form.value.currency.trim() || 'TRY',
      note: form.value.note.trim() || null,
    };
    if (editingId.value) await pmUpdateBudgetLine(editingId.value, body);
    else await pmCreateBudgetLine(props.projectId, body);
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.budgetSaved'),
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
    await pmDeleteBudgetLine(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.budgetDeleted'),
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
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.budget.hint') }}</div>
      <v-btn color="primary" :disabled="!wbs.length" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.budget.new') }}
      </v-btn>
    </div>

    <div v-if="budget" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip size="small" variant="tonal">
        {{ t('projectManagement.budget.planned') }} · {{ money(budget.plannedAmount) }}
      </v-chip>
      <v-chip size="small" variant="tonal">
        {{ t('projectManagement.budget.actual') }} · {{ money(budget.actualAmount) }}
      </v-chip>
      <v-chip size="small" :color="budget.overCount ? 'error' : 'success'" variant="tonal">
        {{ t('projectManagement.budget.variance') }} · {{ money(budget.variance) }}
      </v-chip>
    </div>

    <div v-if="packages.length" class="d-flex flex-column ga-2 mb-4">
      <div
        v-for="pack in packages"
        :key="pack.wbsId"
        class="rounded-lg border pa-3 d-flex align-center justify-space-between flex-wrap ga-2"
      >
        <div class="d-flex align-center ga-2">
          <span class="text-subtitle-2">{{ wbsName(pack.wbsId) }}</span>
          <v-chip size="x-small" :color="pack.over ? 'error' : 'success'" variant="tonal">
            {{ pack.over ? t('projectManagement.budget.over') : t('projectManagement.budget.ok') }}
          </v-chip>
        </div>
        <span class="text-caption text-medium-emphasis">
          {{ money(pack.actualAmount, pack.currency) }} / {{ money(pack.plannedAmount, pack.currency) }}
        </span>
      </div>
    </div>

    <v-data-table
      :headers="headers"
      :items="lines"
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
      <template #item.category="{ item }">
        {{ categoryLabel(item.category) }}
      </template>
      <template #item.plannedAmount="{ item }">
        {{ money(item.plannedAmount, item.currency) }}
      </template>
      <template #item.actualAmount="{ item }">
        {{ money(item.actualAmount, item.currency) }}
      </template>
      <template #item.variance="{ item }">
        <span :class="item.over ? 'text-error' : ''">{{ money(item.variance, item.currency) }}</span>
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
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.budget.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingId ? t('projectManagement.budget.edit') : t('projectManagement.budget.new') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="form.name" :label="t('projectManagement.budget.line')" density="comfortable" />
          <v-select
            v-model="form.wbsId"
            :items="wbsItems"
            :label="t('projectManagement.fields.wbsCode')"
            density="comfortable"
          />
          <v-select
            v-model="form.category"
            :items="categoryItems"
            :label="t('projectManagement.fields.kind')"
            density="comfortable"
          />
          <div class="d-flex ga-3">
            <v-text-field
              v-model.number="form.plannedAmount"
              type="number"
              min="0"
              :label="t('projectManagement.budget.planned')"
              density="comfortable"
            />
            <v-text-field
              v-model.number="form.actualAmount"
              type="number"
              min="0"
              :label="t('projectManagement.budget.actual')"
              density="comfortable"
            />
          </div>
          <v-text-field v-model="form.currency" :label="t('projectManagement.budget.currency')" density="comfortable" />
          <v-textarea v-model="form.note" :label="t('projectManagement.budget.note')" density="comfortable" rows="2" auto-grow />
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
        <v-card-title>{{ t('projectManagement.budget.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.budget.deleteConfirm') }}</v-card-text>
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
