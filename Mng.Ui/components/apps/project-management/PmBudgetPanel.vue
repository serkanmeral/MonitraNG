<script setup lang="ts">
import { computed, ref, watch } from 'vue';
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

const packageSearch = ref('');
const packageFilter = ref<'all' | 'over' | 'ok'>('all');
const packagePage = ref(1);
const packageItemsPerPage = ref(25);
const expandedPackages = ref<string[]>([]);
const lineSearch = ref('');
const lineFilter = ref<'all' | 'over' | 'ok'>('all');
const linePage = ref(1);
const lineItemsPerPage = ref(25);

const pageSizeOptions = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
  { value: 100, title: '100' },
];

const CATEGORY_META: Record<PmBudgetCategory, { icon: string; color: string }> = {
  labor: { icon: 'mdi-account-hard-hat', color: 'primary' },
  material: { icon: 'mdi-package-variant-closed', color: 'warning' },
  subcontract: { icon: 'mdi-account-group-outline', color: 'info' },
  other: { icon: 'mdi-dots-horizontal-circle-outline', color: 'secondary' },
};

const form = ref({
  wbsId: '',
  category: 'labor' as PmBudgetCategory,
  name: '',
  plannedAmount: 0,
  actualAmount: 0,
  currency: 'TRY',
  note: '',
});

const categoryChoices = computed(() =>
  (['labor', 'material', 'subcontract', 'other'] as PmBudgetCategory[]).map((value) => ({
    value,
    title: t(`projectManagement.budget.category.${value}`),
    hint: t(`projectManagement.budget.dialog.categoryHint.${value}`),
    icon: CATEGORY_META[value].icon,
    color: CATEGORY_META[value].color,
  })),
);

const categoryMeta = computed(() => CATEGORY_META[form.value.category] || CATEGORY_META.labor);

const currencyItems = [
  { title: 'TRY', value: 'TRY' },
  { title: 'USD', value: 'USD' },
  { title: 'EUR', value: 'EUR' },
];

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

const packages = computed(() => props.budget?.packages ?? []);
const currency = computed(() => props.budget?.currency || 'TRY');
const mixedCurrency = computed(() => {
  const codes = new Set(props.lines.map((row) => (row.currency || 'TRY').toUpperCase()));
  return codes.size > 1;
});

const filteredPackages = computed(() => {
  const query = packageSearch.value.trim().toLowerCase();
  return packages.value.filter((row) => {
    if (packageFilter.value === 'over' && !row.over) return false;
    if (packageFilter.value === 'ok' && row.over) return false;
    if (!query) return true;
    const wbs = wbsById.value.get(row.wbsId);
    const haystack = [wbs?.wbsCode, wbs?.name, row.currency].filter(Boolean).join(' ').toLowerCase();
    return haystack.includes(query);
  });
});

const filteredLines = computed(() => {
  const query = lineSearch.value.trim().toLowerCase();
  return props.lines.filter((row) => {
    if (lineFilter.value === 'over' && !row.over) return false;
    if (lineFilter.value === 'ok' && row.over) return false;
    if (!query) return true;
    const wbs = wbsById.value.get(row.wbsId);
    const haystack = [row.name, row.category, row.currency, wbs?.wbsCode, wbs?.name]
      .filter(Boolean)
      .join(' ')
      .toLowerCase();
    return haystack.includes(query);
  });
});

watch([packageSearch, packageFilter], () => {
  packagePage.value = 1;
  expandedPackages.value = [];
});

watch([lineSearch, lineFilter], () => {
  linePage.value = 1;
});

const packageHeaders = computed(() => [
  { title: t('projectManagement.fields.wbsCode'), key: 'wbs', minWidth: 180 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 120 },
  { title: t('projectManagement.budget.planned'), key: 'plannedAmount', width: 140 },
  { title: t('projectManagement.budget.actual'), key: 'actualAmount', width: 140 },
  { title: t('projectManagement.budget.variance'), key: 'variance', width: 140 },
]);

const lineHeaders = computed(() => [
  { title: t('projectManagement.budget.line'), key: 'name', minWidth: 160 },
  { title: t('projectManagement.fields.wbsCode'), key: 'wbs', minWidth: 150 },
  { title: t('projectManagement.fields.kind'), key: 'category', width: 130 },
  { title: t('projectManagement.budget.planned'), key: 'plannedAmount', width: 120 },
  { title: t('projectManagement.budget.actual'), key: 'actualAmount', width: 120 },
  { title: t('projectManagement.budget.variance'), key: 'variance', width: 120 },
  { title: t('projectManagement.actions'), key: 'actions', width: 120, sortable: false, align: 'end' as const },
]);

const formVariance = computed(() =>
  Math.round((Number(form.value.plannedAmount) - Number(form.value.actualAmount)) * 100) / 100,
);
const formOver = computed(() => Number(form.value.actualAmount) > Number(form.value.plannedAmount) + 0.005);

const canSave = computed(() =>
  Boolean(form.value.name.trim() && form.value.wbsId && form.value.plannedAmount >= 0 && form.value.actualAmount >= 0),
);

function wbsName(id?: string | null) {
  if (!id) return '';
  const row = wbsById.value.get(id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function categoryLabel(value?: string | null) {
  const key = `projectManagement.budget.category.${value || 'other'}`;
  const label = t(key);
  return label === key ? (value || 'other') : label;
}

function categoryVisual(value?: string | null) {
  const key = (value || 'other') as PmBudgetCategory;
  return CATEGORY_META[key] || CATEGORY_META.other;
}

function money(value: number, code?: string | null) {
  const cur = code || currency.value;
  return `${Number(value || 0).toLocaleString('tr-TR', { minimumFractionDigits: 0, maximumFractionDigits: 2 })} ${cur}`;
}

function packageLines(wbsId: string) {
  return props.lines.filter((row) => row.wbsId === wbsId);
}

function selectCategory(category: PmBudgetCategory) {
  form.value.category = category;
}

function openCreate() {
  editingId.value = null;
  form.value = {
    wbsId: props.wbs[0]?.id || '',
    category: 'labor',
    name: '',
    plannedAmount: 0,
    actualAmount: 0,
    currency: 'TRY',
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
    currency: (row.currency || 'TRY').toUpperCase(),
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
      currency: (form.value.currency || 'TRY').trim().toUpperCase(),
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
    <div class="d-flex align-center justify-space-between mb-3 ga-3 flex-wrap">
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.budget.hint') }}</div>
      <v-btn color="primary" :disabled="!wbs.length" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.budget.new') }}
      </v-btn>
    </div>

    <div v-if="packages.length" class="mb-5">
      <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-3">
        <div class="text-subtitle-2">{{ t('projectManagement.budget.packagesTitle') }}</div>
        <div class="text-caption text-medium-emphasis">
          {{
            t('projectManagement.budget.summaryLine', {
              over: budget?.overCount || 0,
              packages: packages.length,
              lines: lines.length,
            })
          }}
        </div>
      </div>
      <div v-if="mixedCurrency" class="text-caption text-medium-emphasis mb-3">
        {{ t('projectManagement.budget.mixedCurrency') }}
      </div>
      <div v-else-if="budget" class="d-flex flex-wrap ga-2 mb-3">
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

      <div class="d-flex flex-wrap ga-2 mb-3 align-center">
        <v-text-field
          v-model="packageSearch"
          :label="t('projectManagement.budget.search')"
          density="comfortable"
          hide-details
          clearable
          style="max-width: 280px"
        />
        <v-chip :variant="packageFilter === 'all' ? 'flat' : 'tonal'" @click="packageFilter = 'all'">
          {{ t('projectManagement.budget.filterAll') }}
        </v-chip>
        <v-chip
          :variant="packageFilter === 'over' ? 'flat' : 'tonal'"
          :color="packageFilter === 'over' ? 'error' : undefined"
          @click="packageFilter = 'over'"
        >
          {{ t('projectManagement.budget.over') }}
        </v-chip>
        <v-chip
          :variant="packageFilter === 'ok' ? 'flat' : 'tonal'"
          :color="packageFilter === 'ok' ? 'success' : undefined"
          @click="packageFilter = 'ok'"
        >
          {{ t('projectManagement.budget.ok') }}
        </v-chip>
      </div>

      <v-data-table
        v-model:page="packagePage"
        v-model:items-per-page="packageItemsPerPage"
        v-model:expanded="expandedPackages"
        :headers="packageHeaders"
        :items="filteredPackages"
        :loading="loading"
        item-value="wbsId"
        show-expand
        density="comfortable"
        class="rounded-lg border"
        :items-per-page-options="pageSizeOptions"
      >
        <template #item.wbs="{ item }">
          {{ wbsName(item.wbsId) || '—' }}
        </template>
        <template #item.status="{ item }">
          <v-chip size="small" :color="item.over ? 'error' : 'success'" variant="tonal">
            {{ item.over ? t('projectManagement.budget.over') : t('projectManagement.budget.ok') }}
          </v-chip>
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
        <template #expanded-row="{ columns, item }">
          <tr>
            <td :colspan="columns.length" class="py-3">
              <div class="d-flex flex-column ga-1">
                <div v-for="line in packageLines(item.wbsId)" :key="line.id" class="text-caption">
                  {{ line.name }} · {{ categoryLabel(line.category) }} ·
                  {{ money(line.actualAmount, line.currency) }} /
                  {{ money(line.plannedAmount, line.currency) }}
                </div>
                <div v-if="!packageLines(item.wbsId).length" class="text-caption text-medium-emphasis">—</div>
              </div>
            </td>
          </tr>
        </template>
        <template #no-data>
          <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.budget.emptyPackages') }}</div>
        </template>
      </v-data-table>
    </div>

    <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-3">
      <div class="text-subtitle-2">{{ t('projectManagement.budget.linesTitle') }}</div>
    </div>
    <div class="d-flex flex-wrap ga-2 mb-3 align-center">
      <v-text-field
        v-model="lineSearch"
        :label="t('projectManagement.budget.lineSearch')"
        density="comfortable"
        hide-details
        clearable
        style="max-width: 280px"
      />
      <v-chip :variant="lineFilter === 'all' ? 'flat' : 'tonal'" @click="lineFilter = 'all'">
        {{ t('projectManagement.budget.filterAll') }}
      </v-chip>
      <v-chip
        :variant="lineFilter === 'over' ? 'flat' : 'tonal'"
        :color="lineFilter === 'over' ? 'error' : undefined"
        @click="lineFilter = 'over'"
      >
        {{ t('projectManagement.budget.over') }}
      </v-chip>
      <v-chip
        :variant="lineFilter === 'ok' ? 'flat' : 'tonal'"
        :color="lineFilter === 'ok' ? 'success' : undefined"
        @click="lineFilter = 'ok'"
      >
        {{ t('projectManagement.budget.ok') }}
      </v-chip>
    </div>

    <v-data-table
      v-model:page="linePage"
      v-model:items-per-page="lineItemsPerPage"
      :headers="lineHeaders"
      :items="filteredLines"
      :loading="loading"
      item-value="id"
      density="comfortable"
      class="rounded-lg border"
      :items-per-page-options="pageSizeOptions"
    >
      <template #item.wbs="{ item }">
        {{ wbsName(item.wbsId) || '—' }}
      </template>
      <template #item.category="{ item }">
        <v-chip size="small" :color="categoryVisual(item.category).color" variant="tonal">
          <v-icon start :icon="categoryVisual(item.category).icon" size="16" />
          {{ categoryLabel(item.category) }}
        </v-chip>
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

    <v-dialog v-model="dialog" max-width="720" scrollable>
      <v-card rounded="lg">
        <v-card-title class="d-flex align-start ga-3 px-6 py-4">
          <v-avatar :color="categoryMeta.color" variant="tonal" rounded="lg">
            <v-icon :icon="editingId ? 'mdi-pencil-outline' : categoryMeta.icon" />
          </v-avatar>
          <div class="flex-grow-1">
            <div>{{ editingId ? t('projectManagement.budget.edit') : t('projectManagement.budget.new') }}</div>
            <div class="text-body-2 text-medium-emphasis font-weight-regular mt-1">
              {{
                editingId
                  ? t('projectManagement.budget.dialog.subtitleEdit')
                  : t('projectManagement.budget.dialog.subtitleNew')
              }}
            </div>
          </div>
        </v-card-title>
        <v-divider />
        <v-card-text class="d-flex flex-column ga-5 px-6 py-5">
          <section>
            <div class="text-subtitle-2 mb-2">{{ t('projectManagement.budget.dialog.sectionCategory') }}</div>
            <div class="pm-budget-kind-grid">
              <v-card
                v-for="choice in categoryChoices"
                :key="choice.value"
                :variant="form.category === choice.value ? 'tonal' : 'outlined'"
                :color="form.category === choice.value ? choice.color : undefined"
                rounded="lg"
                class="pm-budget-kind-card pa-3"
                role="button"
                @click="selectCategory(choice.value)"
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
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.budget.dialog.sectionIdentity') }}</div>
            <v-text-field
              v-model="form.name"
              :label="t('projectManagement.budget.line')"
              :placeholder="t(`projectManagement.budget.dialog.nameHint.${form.category}`)"
              density="comfortable"
              hide-details="auto"
              class="mb-3"
              prepend-inner-icon="mdi-format-title"
            />
            <v-select
              v-model="form.wbsId"
              :items="wbsItems"
              :label="t('projectManagement.fields.wbsCode')"
              :hint="t('projectManagement.budget.dialog.wbsHint')"
              persistent-hint
              density="comfortable"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.budget.dialog.sectionAmounts') }}</div>
            <p class="text-caption text-medium-emphasis mb-3">{{ t('projectManagement.budget.dialog.sectionAmountsHint') }}</p>
            <div class="d-flex ga-3 flex-wrap">
              <v-text-field
                v-model.number="form.plannedAmount"
                type="number"
                min="0"
                :label="t('projectManagement.budget.planned')"
                density="comfortable"
                hide-details
                class="flex-grow-1"
                prepend-inner-icon="mdi-cash-clock"
              />
              <v-text-field
                v-model.number="form.actualAmount"
                type="number"
                min="0"
                :label="t('projectManagement.budget.actual')"
                density="comfortable"
                hide-details
                class="flex-grow-1"
                prepend-inner-icon="mdi-cash"
              />
            </div>
            <v-select
              v-model="form.currency"
              :items="currencyItems"
              :label="t('projectManagement.budget.currency')"
              :hint="t('projectManagement.budget.dialog.currencyHint')"
              persistent-hint
              density="comfortable"
              class="mt-3"
            />
            <v-alert
              class="mt-3"
              :color="formOver ? 'warning' : 'primary'"
              variant="tonal"
              density="compact"
              :icon="formOver ? 'mdi-alert' : 'mdi-information-outline'"
            >
              <div class="d-flex align-center ga-2 flex-wrap">
                <span class="font-weight-medium">
                  {{ t('projectManagement.budget.dialog.remaining', { amount: money(formVariance, form.currency) }) }}
                </span>
                <v-chip size="x-small" :color="formOver ? 'warning' : 'default'" variant="flat">
                  {{ formOver ? t('projectManagement.budget.dialog.over') : t('projectManagement.budget.dialog.notOver') }}
                </v-chip>
              </div>
            </v-alert>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.budget.dialog.sectionNote') }}</div>
            <v-textarea
              v-model="form.note"
              :label="t('projectManagement.budget.note')"
              density="comfortable"
              rows="2"
              hide-details="auto"
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

.pm-budget-kind-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.pm-budget-kind-card {
  cursor: pointer;
  height: 100%;
}

.pm-budget-kind-card:hover {
  border-color: rgba(var(--v-theme-primary), 0.4);
}

@media (max-width: 600px) {
  .pm-budget-kind-grid {
    grid-template-columns: 1fr;
  }
}
</style>
