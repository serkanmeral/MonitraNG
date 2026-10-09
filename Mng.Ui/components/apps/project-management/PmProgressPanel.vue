<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import PmPickDocumentDialog from '@/components/apps/project-management/PmPickDocumentDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreatePaymentSlice,
  pmCreateProgressClaim,
  pmDateInput,
  pmDatePayload,
  pmDeletePaymentSlice,
  pmDeleteProgressClaim,
  pmUpdatePaymentSlice,
  pmUpdateProgressClaim,
  pmUpsertPaymentTerms,
  pmWriteSlicePlan,
} from '@/services/projectManagementService';
import type { DiResource } from '@/types/apps/documentIntelligence';
import type {
  PmBudgetLine,
  PmPaymentCadence,
  PmPaymentSlice,
  PmPaymentSliceKind,
  PmProgressClaim,
  PmProgressClaimStatus,
  PmProjectProgress,
  PmStageGate,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  projectCode: string;
  hubFolderId?: string | null;
  progress: PmProjectProgress | null;
  wbs: PmWbsItem[];
  gates: PmStageGate[];
  budgetLines: PmBudgetLine[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
  hubReady: [string];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const termsSaving = ref(false);
const terms = ref({ baseAmount: 0, currency: 'TRY', penaltyCapPercent: 35 });

const selectedSliceId = ref<string | null>(null);
const sliceDialog = ref(false);
const sliceSaving = ref(false);
const sliceEditingId = ref<string | null>(null);
const sliceDelete = ref<PmPaymentSlice | null>(null);
const sliceDeleting = ref(false);
const planWritingId = ref<string | null>(null);

const claimDialog = ref(false);
const claimSaving = ref(false);
const claimEditingId = ref<string | null>(null);
const claimDelete = ref<PmProgressClaim | null>(null);
const claimDeleting = ref(false);
const pickOpen = ref(false);
const evidenceTitles = ref<Record<string, string>>({});

const sliceForm = ref(emptySlice());
const claimForm = ref(emptyClaim());

const slices = computed(() => props.progress?.slices ?? []);
const claims = computed(() => props.progress?.claims ?? []);
const selectedSlice = computed(() => slices.value.find((row) => row.id === selectedSliceId.value) ?? null);
const selectedClaims = computed(() =>
  claims.value
    .filter((row) => row.sliceId === selectedSliceId.value)
    .slice()
    .sort((a, b) => a.sequence - b.sequence),
);

const currency = computed(() => props.progress?.terms.currency || 'TRY');

const wbsItems = computed(() => [
  { title: t('projectManagement.progress.none'), value: '' },
  ...props.wbs.map((row) => ({ title: `${row.wbsCode || '—'} ${row.name}`, value: row.id })),
]);
const gateItems = computed(() => [
  { title: t('projectManagement.progress.none'), value: '' },
  ...props.gates.map((row) => ({ title: row.name, value: row.id })),
]);
const budgetItems = computed(() => [
  { title: t('projectManagement.progress.none'), value: '' },
  ...props.budgetLines.map((row) => ({ title: row.name, value: row.id })),
]);

const kindItems = computed(() => [
  { title: t('projectManagement.progress.kind.percent'), value: 'percent' },
  { title: t('projectManagement.progress.kind.unit'), value: 'unit' },
]);
const cadenceItems = computed(() => [
  { title: t('projectManagement.progress.cadence.once'), value: 'once' },
  { title: t('projectManagement.progress.cadence.installments'), value: 'installments' },
]);

const claimStatusItems = computed(() => {
  const current = (claimForm.value.status || 'draft') as PmProgressClaimStatus;
  const allowed: PmProgressClaimStatus[] = [current];
  if (current === 'draft') allowed.push('submitted');
  if (current === 'submitted') allowed.push('draft', 'accepted');
  if (current === 'accepted') allowed.push('paid');
  return allowed.map((value) => ({ title: t(`projectManagement.progress.status.${value}`), value }));
});

const canSaveSlice = computed(() => sliceForm.value.name.trim().length > 0 && !sliceSaving.value);
const canSaveClaim = computed(() => claimForm.value.periodLabel.trim().length > 0 && !claimSaving.value);

watch(
  () => props.progress?.terms,
  (value) => {
    terms.value = {
      baseAmount: value?.baseAmount ?? 0,
      currency: value?.currency || 'TRY',
      penaltyCapPercent: value?.penaltyCapPercent ?? 35,
    };
  },
  { immediate: true },
);

watch(slices, (rows) => {
  if (!rows.length) {
    selectedSliceId.value = null;
    return;
  }
  if (!rows.some((row) => row.id === selectedSliceId.value)) selectedSliceId.value = rows[0].id;
});

function emptySlice() {
  return {
    name: '',
    kind: 'percent' as PmPaymentSliceKind,
    percent: 0,
    cadence: 'once' as PmPaymentCadence,
    installmentCount: 4,
    intervalMonths: 3,
    unitPrice: 0,
    gateId: '',
    wbsId: '',
    budgetLineId: '',
    anchorDate: '',
    note: '',
  };
}

function emptyClaim() {
  return {
    periodLabel: '',
    sequence: 1,
    dueDate: '',
    claimedAmount: 0,
    acceptedAmount: 0,
    deduction: 0,
    adjustmentAmount: 0,
    quantity: 0,
    status: 'draft' as PmProgressClaimStatus,
    resourceIds: [] as string[],
    note: '',
  };
}

function money(value: number) {
  return `${new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value || 0)} ${currency.value}`;
}

function openSlice(row?: PmPaymentSlice) {
  sliceEditingId.value = row?.id ?? null;
  sliceForm.value = row
    ? {
        name: row.name,
        kind: (row.kind as PmPaymentSliceKind) || 'percent',
        percent: row.percent,
        cadence: (row.cadence as PmPaymentCadence) || 'once',
        installmentCount: row.installmentCount || 4,
        intervalMonths: row.intervalMonths || 3,
        unitPrice: row.unitPrice,
        gateId: row.gateId || '',
        wbsId: row.wbsId || '',
        budgetLineId: row.budgetLineId || '',
        anchorDate: pmDateInput(row.anchorDate),
        note: row.note || '',
      }
    : emptySlice();
  sliceDialog.value = true;
}

function openClaim(row?: PmProgressClaim) {
  if (!selectedSlice.value && !row) return;
  claimEditingId.value = row?.id ?? null;
  claimForm.value = row
    ? {
        periodLabel: row.periodLabel,
        sequence: row.sequence,
        dueDate: pmDateInput(row.dueDate),
        claimedAmount: row.claimedAmount,
        acceptedAmount: row.acceptedAmount,
        deduction: row.deduction,
        adjustmentAmount: row.adjustmentAmount,
        quantity: row.quantity,
        status: (row.status as PmProgressClaimStatus) || 'draft',
        resourceIds: [...(row.resourceIds || [])],
        note: row.note || '',
      }
    : {
        ...emptyClaim(),
        periodLabel: selectedSlice.value?.name || '',
        sequence: (selectedClaims.value.at(-1)?.sequence || 0) + 1,
      };
  claimDialog.value = true;
}

function slicePayload() {
  const form = sliceForm.value;
  return {
    name: form.name.trim(),
    kind: form.kind,
    percent: form.kind === 'percent' ? Number(form.percent) || 0 : 0,
    cadence: form.kind === 'percent' ? form.cadence : 'once',
    installmentCount: form.cadence === 'installments' ? Number(form.installmentCount) || 1 : 1,
    intervalMonths: form.cadence === 'installments' ? Number(form.intervalMonths) || 3 : 0,
    unitPrice: form.kind === 'unit' ? Number(form.unitPrice) || 0 : 0,
    gateId: form.gateId || '',
    wbsId: form.wbsId || '',
    budgetLineId: form.budgetLineId || '',
    anchorDate: pmDatePayload(form.anchorDate),
    note: form.note.trim() || null,
  };
}

async function saveTerms() {
  termsSaving.value = true;
  try {
    await pmUpsertPaymentTerms(props.projectId, {
      baseAmount: Number(terms.value.baseAmount) || 0,
      currency: terms.value.currency.trim() || 'TRY',
      penaltyCapPercent: Number(terms.value.penaltyCapPercent),
    });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.progressSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    termsSaving.value = false;
  }
}

async function saveSlice() {
  if (!canSaveSlice.value) return;
  sliceSaving.value = true;
  try {
    if (sliceEditingId.value) await pmUpdatePaymentSlice(sliceEditingId.value, slicePayload());
    else await pmCreatePaymentSlice(props.projectId, slicePayload());
    sliceDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.progressSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    sliceSaving.value = false;
  }
}

async function saveClaim() {
  if (!canSaveClaim.value || !selectedSlice.value) return;
  claimSaving.value = true;
  try {
    const form = claimForm.value;
    const body = {
      periodLabel: form.periodLabel.trim(),
      sequence: Number(form.sequence) || 1,
      dueDate: pmDatePayload(form.dueDate),
      claimedAmount: Number(form.claimedAmount) || 0,
      acceptedAmount: Number(form.acceptedAmount) || 0,
      deduction: Number(form.deduction) || 0,
      adjustmentAmount: Number(form.adjustmentAmount) || 0,
      quantity: Number(form.quantity) || 0,
      status: form.status,
      resourceIds: form.resourceIds,
      note: form.note.trim() || null,
    };
    if (claimEditingId.value) await pmUpdateProgressClaim(claimEditingId.value, body);
    else await pmCreateProgressClaim(props.projectId, { ...body, sliceId: selectedSlice.value.id });
    claimDialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.progressSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    claimSaving.value = false;
  }
}

async function writePlan(row: PmPaymentSlice) {
  planWritingId.value = row.id;
  try {
    await pmWriteSlicePlan(row.id);
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.progressPlanWritten'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    planWritingId.value = null;
  }
}

async function executeSliceDelete() {
  if (!sliceDelete.value) return;
  sliceDeleting.value = true;
  try {
    await pmDeletePaymentSlice(sliceDelete.value.id);
    sliceDelete.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.progressDeleted'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  } finally {
    sliceDeleting.value = false;
  }
}

async function executeClaimDelete() {
  if (!claimDelete.value) return;
  claimDeleting.value = true;
  try {
    await pmDeleteProgressClaim(claimDelete.value.id);
    claimDelete.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.progressDeleted'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.deleteFailed');
  } finally {
    claimDeleting.value = false;
  }
}

function pickDocument(resource: DiResource) {
  const id = resource.id;
  if (!id || claimForm.value.resourceIds.includes(id)) return;
  claimForm.value.resourceIds = [...claimForm.value.resourceIds, id];
  evidenceTitles.value = { ...evidenceTitles.value, [id]: resource.title || resource.name || id };
}

function evidenceLabel(id: string) {
  return evidenceTitles.value[id] || id;
}

function onSliceDeleteDialog(open: boolean) {
  if (!open) sliceDelete.value = null;
}

function onClaimDeleteDialog(open: boolean) {
  if (!open) claimDelete.value = null;
}

function sliceShare(row: PmPaymentSlice) {
  if (row.kind === 'unit') return t('projectManagement.progress.unitPriceValue', { amount: money(row.unitPrice) });
  const cadence = row.cadence === 'installments'
    ? t('projectManagement.progress.installmentValue', { count: row.installmentCount, months: row.intervalMonths })
    : t('projectManagement.progress.cadence.once');
  return `${row.percent}% · ${money(row.scheduledAmount)} · ${cadence}`;
}
</script>

<template>
  <div>
    <div class="text-body-2 text-medium-emphasis mb-4">{{ t('projectManagement.progress.hint') }}</div>

    <div v-if="progress" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip size="small" variant="tonal">{{ t('projectManagement.progress.base') }} {{ money(progress.terms.baseAmount) }}</v-chip>
      <v-chip size="small" variant="tonal">{{ t('projectManagement.progress.cap') }} %{{ progress.terms.penaltyCapPercent }} · {{ money(progress.penaltyCapAmount) }}</v-chip>
      <v-chip size="small" variant="tonal" color="primary">{{ t('projectManagement.progress.accepted') }} {{ money(progress.acceptedNet) }}</v-chip>
      <v-chip size="small" variant="tonal" color="success">{{ t('projectManagement.progress.paid') }} {{ money(progress.paidNet) }}</v-chip>
      <v-chip size="small" variant="tonal">{{ t('projectManagement.progress.remaining') }} {{ money(progress.percentRemaining) }}</v-chip>
      <v-chip size="small" variant="tonal" color="warning">{{ t('projectManagement.progress.deducted') }} {{ money(progress.deducted) }}</v-chip>
      <v-chip size="small" variant="tonal">{{ t('projectManagement.progress.outstanding') }} {{ money(progress.outstanding) }}</v-chip>
    </div>

    <v-alert v-if="progress?.percentOver" type="warning" variant="tonal" density="compact" class="mb-4">
      {{ t('projectManagement.progress.percentOver', { total: progress.percentTotal }) }}
    </v-alert>

    <v-card variant="outlined" rounded="lg" class="mb-4">
      <v-card-text>
        <div class="text-subtitle-2 mb-3">{{ t('projectManagement.progress.termsTitle') }}</div>
        <v-row dense>
          <v-col cols="12" sm="4">
            <v-text-field v-model.number="terms.baseAmount" type="number" :label="t('projectManagement.progress.base')" hide-details />
          </v-col>
          <v-col cols="12" sm="4">
            <v-text-field v-model="terms.currency" :label="t('projectManagement.progress.currency')" maxlength="3" hide-details />
          </v-col>
          <v-col cols="12" sm="4">
            <v-text-field v-model.number="terms.penaltyCapPercent" type="number" :label="t('projectManagement.progress.capPercent')" hide-details />
          </v-col>
        </v-row>
        <div class="d-flex justify-end mt-3">
          <v-btn color="primary" :loading="termsSaving" @click="saveTerms">{{ t('projectManagement.save') }}</v-btn>
        </div>
      </v-card-text>
    </v-card>

    <div class="d-flex align-center mb-2">
      <div class="text-subtitle-2">{{ t('projectManagement.progress.slicesTitle') }}</div>
      <v-spacer />
      <v-btn size="small" color="primary" variant="flat" @click="openSlice()">
        <PlusIcon size="16" class="me-1" />
        {{ t('projectManagement.progress.newSlice') }}
      </v-btn>
    </div>

    <v-table v-if="slices.length" density="compact" class="mb-6">
      <thead>
        <tr>
          <th>{{ t('projectManagement.progress.name') }}</th>
          <th>{{ t('projectManagement.progress.share') }}</th>
          <th />
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in slices"
          :key="row.id"
          :style="row.id === selectedSliceId ? { background: 'rgba(var(--v-theme-primary), 0.12)', cursor: 'pointer' } : { cursor: 'pointer' }"
          @click="selectedSliceId = row.id"
        >
          <td>{{ row.name }}</td>
          <td>{{ sliceShare(row) }}</td>
          <td class="text-end text-no-wrap">
            <v-btn
              v-if="row.budgetLineId"
              size="small"
              variant="text"
              :loading="planWritingId === row.id"
              @click.stop="writePlan(row)"
            >
              {{ t('projectManagement.progress.writePlan') }}
            </v-btn>
            <v-btn size="small" variant="text" @click.stop="openSlice(row)">{{ t('projectManagement.edit') }}</v-btn>
            <v-btn size="small" variant="text" color="error" @click.stop="sliceDelete = row">
              <TrashIcon size="16" />
            </v-btn>
          </td>
        </tr>
      </tbody>
    </v-table>
    <div v-else class="text-body-2 text-medium-emphasis mb-6">{{ t('projectManagement.progress.emptySlices') }}</div>

    <div class="d-flex align-center mb-2">
      <div class="text-subtitle-2">{{ t('projectManagement.progress.claimsTitle') }}</div>
      <v-spacer />
      <v-btn size="small" color="primary" variant="flat" :disabled="!selectedSlice" @click="openClaim()">
        <PlusIcon size="16" class="me-1" />
        {{ t('projectManagement.progress.newClaim') }}
      </v-btn>
    </div>
    <div v-if="!selectedSlice" class="text-body-2 text-medium-emphasis">{{ t('projectManagement.progress.pickSlice') }}</div>
    <v-table v-else-if="selectedClaims.length" density="compact">
      <thead>
        <tr>
          <th>{{ t('projectManagement.progress.period') }}</th>
          <th>{{ t('projectManagement.progress.statusLabel') }}</th>
          <th>{{ t('projectManagement.progress.net') }}</th>
          <th />
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in selectedClaims" :key="row.id">
          <td>{{ row.sequence }}. {{ row.periodLabel }}</td>
          <td>{{ t(`projectManagement.progress.status.${row.status}`) }}</td>
          <td>{{ money(row.net) }}</td>
          <td class="text-end">
            <v-btn size="small" variant="text" @click="openClaim(row)">{{ t('projectManagement.edit') }}</v-btn>
            <v-btn v-if="row.status === 'draft'" size="small" variant="text" color="error" @click="claimDelete = row">
              <TrashIcon size="16" />
            </v-btn>
          </td>
        </tr>
      </tbody>
    </v-table>
    <div v-else class="text-body-2 text-medium-emphasis">{{ t('projectManagement.progress.emptyClaims') }}</div>

    <v-dialog v-model="sliceDialog" max-width="640">
      <v-card rounded="lg">
        <v-card-title>{{ sliceEditingId ? t('projectManagement.progress.editSlice') : t('projectManagement.progress.newSlice') }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="sliceForm.name" :label="t('projectManagement.progress.name')" />
          <v-select v-model="sliceForm.kind" :items="kindItems" :label="t('projectManagement.progress.kindLabel')" />
          <template v-if="sliceForm.kind === 'percent'">
            <v-text-field v-model.number="sliceForm.percent" type="number" :label="t('projectManagement.progress.percent')" />
            <v-select v-model="sliceForm.cadence" :items="cadenceItems" :label="t('projectManagement.progress.cadenceLabel')" />
            <v-row v-if="sliceForm.cadence === 'installments'" dense>
              <v-col cols="6">
                <v-text-field v-model.number="sliceForm.installmentCount" type="number" :label="t('projectManagement.progress.count')" />
              </v-col>
              <v-col cols="6">
                <v-text-field v-model.number="sliceForm.intervalMonths" type="number" :label="t('projectManagement.progress.interval')" />
              </v-col>
            </v-row>
          </template>
          <v-text-field v-else v-model.number="sliceForm.unitPrice" type="number" :label="t('projectManagement.progress.unitPrice')" />
          <v-text-field v-model="sliceForm.anchorDate" type="date" :label="t('projectManagement.progress.anchor')" />
          <v-select v-model="sliceForm.gateId" :items="gateItems" :label="t('projectManagement.progress.gate')" />
          <v-select v-model="sliceForm.wbsId" :items="wbsItems" :label="t('projectManagement.progress.wbs')" />
          <v-select v-model="sliceForm.budgetLineId" :items="budgetItems" :label="t('projectManagement.progress.budgetLine')" />
          <div class="text-caption text-medium-emphasis mb-2">{{ t('projectManagement.progress.budgetHint') }}</div>
          <v-textarea v-model="sliceForm.note" :label="t('projectManagement.progress.note')" rows="2" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="sliceDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="sliceSaving" :disabled="!canSaveSlice" @click="saveSlice">{{ t('projectManagement.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="claimDialog" max-width="720">
      <v-card rounded="lg">
        <v-card-title>{{ claimEditingId ? t('projectManagement.progress.editClaim') : t('projectManagement.progress.newClaim') }}</v-card-title>
        <v-card-text>
          <v-text-field v-model="claimForm.periodLabel" :label="t('projectManagement.progress.period')" />
          <v-row dense>
            <v-col cols="6">
              <v-text-field v-model.number="claimForm.sequence" type="number" :label="t('projectManagement.progress.sequence')" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="claimForm.dueDate" type="date" :label="t('projectManagement.progress.due')" />
            </v-col>
          </v-row>
          <v-row dense>
            <v-col cols="6">
              <v-text-field v-model.number="claimForm.claimedAmount" type="number" :label="t('projectManagement.progress.claimed')" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model.number="claimForm.acceptedAmount" type="number" :label="t('projectManagement.progress.acceptedAmount')" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model.number="claimForm.deduction" type="number" :label="t('projectManagement.progress.deduction')" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model.number="claimForm.adjustmentAmount" type="number" :label="t('projectManagement.progress.adjustment')" />
            </v-col>
          </v-row>
          <v-text-field
            v-if="selectedSlice?.kind === 'unit'"
            v-model.number="claimForm.quantity"
            type="number"
            :label="t('projectManagement.progress.quantity')"
          />
          <v-select v-if="claimEditingId" v-model="claimForm.status" :items="claimStatusItems" :label="t('projectManagement.progress.statusLabel')" />
          <div class="text-caption text-medium-emphasis mb-2">{{ t('projectManagement.progress.evidenceHint') }}</div>
          <div class="d-flex flex-wrap ga-2 mb-2">
            <v-chip
              v-for="id in claimForm.resourceIds"
              :key="id"
              closable
              size="small"
              @click:close="claimForm.resourceIds = claimForm.resourceIds.filter((item) => item !== id)"
            >
              {{ evidenceLabel(id) }}
            </v-chip>
          </div>
          <v-btn size="small" variant="tonal" @click="pickOpen = true">{{ t('projectManagement.pickDocument.open') }}</v-btn>
          <v-textarea v-model="claimForm.note" class="mt-3" :label="t('projectManagement.progress.note')" rows="2" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="claimDialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="claimSaving" :disabled="!canSaveClaim" @click="saveClaim">{{ t('projectManagement.save') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <PmPickDocumentDialog
      v-model="pickOpen"
      :project-id="projectId"
      :project-code="projectCode"
      :hub-folder-id="hubFolderId"
      :exclude-resource-ids="claimForm.resourceIds"
      @pick="pickDocument"
      @hub-ready="emit('hubReady', $event)"
    />

    <v-dialog :model-value="Boolean(sliceDelete)" max-width="440" @update:model-value="onSliceDeleteDialog">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.progress.deleteSliceTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.progress.deleteSliceConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="sliceDelete = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="sliceDeleting" @click="executeSliceDelete">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="Boolean(claimDelete)" max-width="440" @update:model-value="onClaimDeleteDialog">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.progress.deleteClaimTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.progress.deleteClaimConfirm') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="claimDelete = null">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="error" :loading="claimDeleting" @click="executeClaimDelete">{{ t('projectManagement.delete') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
