<script setup lang="ts">
import { computed, ref } from 'vue';
import PmPickDocumentDialog from '@/components/apps/project-management/PmPickDocumentDialog.vue';
import PmPickWorkItemDialog from '@/components/apps/project-management/PmPickWorkItemDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePmDate } from '@/composables/usePmDate';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import { diGetById } from '@/services/documentIntelligenceService';
import { ocGetWorkItemProfile } from '@/services/operationCoreService';
import {
  pmCreateObligation,
  pmDateInput,
  pmDatePayload,
  pmDeleteObligation,
  pmUpdateObligation,
} from '@/services/projectManagementService';
import type { DiResource } from '@/types/apps/documentIntelligence';
import type { PmObligation, PmObligationStatus, PmWbsItem, PmWorkItemCandidate } from '@/types/apps/projectManagement';
import { diPageResourceLabel } from '@/utils/diPageResource';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  projectCode: string;
  hubFolderId?: string | null;
  items: PmObligation[];
  wbs: PmWbsItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
  hubReady: [id: string];
}>();

const { t } = useAppI18n();
const { formatPmDateOrDash } = usePmDate();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const statusFilter = ref<'all' | 'open' | 'overdue' | 'unbound'>('all');
const dialog = ref(false);
const pickOpen = ref(false);
const pickTarget = ref<'source' | 'evidence'>('source');
const workPickOpen = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmObligation | null>(null);
const deleting = ref(false);
const closingId = ref<string | null>(null);

const STATUS_META: Record<PmObligationStatus, { icon: string; color: string }> = {
  open: { icon: 'mdi-circle-outline', color: 'info' },
  inProgress: { icon: 'mdi-progress-clock', color: 'primary' },
  satisfied: { icon: 'mdi-check-circle-outline', color: 'success' },
  waived: { icon: 'mdi-minus-circle-outline', color: 'warning' },
};

const form = ref({
  title: '',
  clauseRef: '',
  sourceResourceId: '',
  sourceTitle: '',
  workItemId: '',
  workItemKey: '',
  workItemTitle: '',
  evidenceResourceId: '',
  evidenceTitle: '',
  wbsId: '',
  status: 'open' as PmObligationStatus,
  dueDate: '',
  note: '',
});

const statusChoices = computed(() =>
  (['open', 'inProgress', 'satisfied', 'waived'] as PmObligationStatus[]).map((value) => ({
    value,
    title: t(`projectManagement.obligation.status.${value}`),
    hint: t(`projectManagement.obligation.dialog.statusHint.${value}`),
    icon: STATUS_META[value].icon,
    color: STATUS_META[value].color,
  })),
);

const statusMeta = computed(() => STATUS_META[form.value.status] || STATUS_META.open);

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

const formOverdue = computed(() => {
  if (form.value.status === 'satisfied' || form.value.status === 'waived') return false;
  if (!form.value.dueDate) return false;
  return form.value.dueDate < new Date().toISOString().slice(0, 10);
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

function workItemLabel(id?: string | null, key?: string | null, title?: string | null) {
  if (key && title) return `${key} · ${title}`;
  if (title) return title;
  if (key) return key;
  if (!id) return '';
  return id.slice(0, 8);
}

function emptyForm() {
  return {
    title: '',
    clauseRef: '',
    sourceResourceId: '',
    sourceTitle: '',
    workItemId: '',
    workItemKey: '',
    workItemTitle: '',
    evidenceResourceId: '',
    evidenceTitle: '',
    wbsId: '',
    status: 'open' as PmObligationStatus,
    dueDate: '',
    note: '',
  };
}

async function resolveDocumentLabel(id: string) {
  if (!id) return '';
  try {
    const resource = await diGetById(id);
    return diPageResourceLabel(resource) || id;
  } catch {
    return id;
  }
}

async function resolveWorkItemLabel(id: string) {
  if (!id) return { key: '', title: '' };
  try {
    const profile = await ocGetWorkItemProfile(id);
    return {
      key: profile.workItem?.key || '',
      title: profile.workItem?.title || '',
    };
  } catch {
    return { key: '', title: '' };
  }
}

function openCreate() {
  editingId.value = null;
  form.value = emptyForm();
  dialog.value = true;
}

async function openEdit(row: PmObligation) {
  editingId.value = row.id;
  form.value = {
    title: row.title,
    clauseRef: row.clauseRef || '',
    sourceResourceId: row.sourceResourceId || '',
    sourceTitle: row.sourceResourceId || '',
    workItemId: row.workItemId || '',
    workItemKey: '',
    workItemTitle: row.workItemId || '',
    evidenceResourceId: row.evidenceResourceId || '',
    evidenceTitle: row.evidenceResourceId || '',
    wbsId: row.wbsId || '',
    status: (row.status as PmObligationStatus) || 'open',
    dueDate: pmDateInput(row.dueDate),
    note: row.note || '',
  };
  dialog.value = true;
  const [sourceTitle, evidenceTitle, work] = await Promise.all([
    resolveDocumentLabel(row.sourceResourceId || ''),
    resolveDocumentLabel(row.evidenceResourceId || ''),
    resolveWorkItemLabel(row.workItemId || ''),
  ]);
  if (editingId.value !== row.id) return;
  form.value.sourceTitle = sourceTitle;
  form.value.evidenceTitle = evidenceTitle;
  form.value.workItemKey = work.key;
  form.value.workItemTitle = work.title;
}

function selectStatus(status: PmObligationStatus) {
  form.value.status = status;
}

function openPicker(target: 'source' | 'evidence') {
  pickTarget.value = target;
  pickOpen.value = true;
}

function pickDocument(resource: DiResource) {
  const label = diPageResourceLabel(resource) || resource.id;
  if (pickTarget.value === 'evidence') {
    form.value.evidenceResourceId = resource.id;
    form.value.evidenceTitle = label;
    return;
  }
  form.value.sourceResourceId = resource.id;
  form.value.sourceTitle = label;
}

function pickWorkItem(row: PmWorkItemCandidate) {
  form.value.workItemId = row.id;
  form.value.workItemKey = row.key || '';
  form.value.workItemTitle = row.title || '';
}

function clearSource() {
  form.value.sourceResourceId = '';
  form.value.sourceTitle = '';
}

function clearEvidence() {
  form.value.evidenceResourceId = '';
  form.value.evidenceTitle = '';
}

function clearWorkItem() {
  form.value.workItemId = '';
  form.value.workItemKey = '';
  form.value.workItemTitle = '';
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
        {{ formatPmDateOrDash(item.dueDate) }}
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

    <v-dialog v-model="dialog" max-width="720" scrollable>
      <v-card rounded="lg">
        <v-card-title class="d-flex align-start ga-3 px-6 py-4">
          <v-avatar :color="statusMeta.color" variant="tonal" rounded="lg">
            <v-icon :icon="editingId ? 'mdi-pencil-outline' : statusMeta.icon" />
          </v-avatar>
          <div class="flex-grow-1">
            <div>{{ editingId ? t('projectManagement.obligation.edit') : t('projectManagement.obligation.new') }}</div>
            <div class="text-body-2 text-medium-emphasis font-weight-regular mt-1">
              {{
                editingId
                  ? t('projectManagement.obligation.dialog.subtitleEdit')
                  : t('projectManagement.obligation.dialog.subtitleNew')
              }}
            </div>
          </div>
        </v-card-title>
        <v-divider />
        <v-card-text class="d-flex flex-column ga-5 px-6 py-5">
          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.obligation.dialog.sectionClause') }}</div>
            <v-text-field
              v-model="form.clauseRef"
              :label="t('projectManagement.obligation.clause')"
              density="comfortable"
              prepend-inner-icon="mdi-pound"
              hide-details="auto"
              class="mb-3"
            />
            <v-textarea
              v-model="form.title"
              :label="t('projectManagement.obligation.statement')"
              density="comfortable"
              rows="2"
              auto-grow
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.obligation.dialog.sectionDocuments') }}</div>
            <p class="text-caption text-medium-emphasis mb-3">
              {{ t('projectManagement.obligation.dialog.sectionDocumentsHint') }}
            </p>

            <div class="text-body-2 font-weight-medium mb-1">{{ t('projectManagement.obligation.source') }}</div>
            <p class="text-caption text-medium-emphasis mb-2">{{ t('projectManagement.obligation.sourceHint') }}</p>
            <div v-if="form.sourceResourceId" class="d-flex align-center ga-2 flex-wrap mb-2">
              <NuxtLink :to="resourceHref(form.sourceResourceId)" class="text-decoration-none" @click.stop>
                <v-chip size="small" color="primary" variant="tonal">
                  {{ form.sourceTitle || form.sourceResourceId }}
                </v-chip>
              </NuxtLink>
              <v-btn size="small" variant="text" @click="clearSource">
                {{ t('projectManagement.decision.clearDocument') }}
              </v-btn>
            </div>
            <div v-else class="text-caption text-medium-emphasis mb-2">
              {{ t('projectManagement.obligation.noSource') }}
            </div>
            <v-btn variant="tonal" class="text-none mb-4" @click="openPicker('source')">
              {{ t('projectManagement.pickDocument.open') }}
            </v-btn>

            <div class="text-body-2 font-weight-medium mb-1">{{ t('projectManagement.obligation.evidence') }}</div>
            <p class="text-caption text-medium-emphasis mb-2">{{ t('projectManagement.obligation.evidenceHint') }}</p>
            <div v-if="form.evidenceResourceId" class="d-flex align-center ga-2 flex-wrap mb-2">
              <NuxtLink :to="resourceHref(form.evidenceResourceId)" class="text-decoration-none" @click.stop>
                <v-chip size="small" color="success" variant="tonal">
                  {{ form.evidenceTitle || form.evidenceResourceId }}
                </v-chip>
              </NuxtLink>
              <v-btn size="small" variant="text" @click="clearEvidence">
                {{ t('projectManagement.decision.clearDocument') }}
              </v-btn>
            </div>
            <div v-else class="text-caption text-medium-emphasis mb-2">
              {{ t('projectManagement.obligation.noEvidence') }}
            </div>
            <v-btn variant="tonal" class="text-none" @click="openPicker('evidence')">
              {{ t('projectManagement.pickDocument.open') }}
            </v-btn>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.obligation.dialog.sectionWhere') }}</div>
            <v-select
              v-model="form.wbsId"
              :items="wbsItems"
              :label="t('projectManagement.fields.wbsCode')"
              :hint="t('projectManagement.obligation.dialog.wbsHint')"
              persistent-hint
              density="comfortable"
              class="mb-3"
            />
            <div class="text-body-2 font-weight-medium mb-1">{{ t('projectManagement.fields.workItem') }}</div>
            <p class="text-caption text-medium-emphasis mb-2">{{ t('projectManagement.obligation.dialog.workItemHint') }}</p>
            <div v-if="form.workItemId" class="d-flex align-center ga-2 flex-wrap mb-2">
              <NuxtLink :to="workItemHref(form.workItemId)" class="text-decoration-none" @click.stop>
                <v-chip size="small" color="primary" variant="tonal">
                  {{ workItemLabel(form.workItemId, form.workItemKey, form.workItemTitle) }}
                </v-chip>
              </NuxtLink>
              <v-btn size="small" variant="text" @click="clearWorkItem">
                {{ t('projectManagement.obligation.clearWorkItem') }}
              </v-btn>
            </div>
            <div v-else class="text-caption text-medium-emphasis mb-2">
              {{ t('projectManagement.obligation.noWorkItem') }}
            </div>
            <v-btn variant="tonal" class="text-none" @click="workPickOpen = true">
              {{ t('projectManagement.pickWorkItem.open') }}
            </v-btn>
          </section>

          <section>
            <div class="text-subtitle-2 mb-2">{{ t('projectManagement.obligation.dialog.sectionStatus') }}</div>
            <div class="pm-obligation-status-grid">
              <v-card
                v-for="choice in statusChoices"
                :key="choice.value"
                :variant="form.status === choice.value ? 'tonal' : 'outlined'"
                :color="form.status === choice.value ? choice.color : undefined"
                rounded="lg"
                class="pm-obligation-status-card pa-3"
                role="button"
                @click="selectStatus(choice.value)"
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
            <v-text-field
              v-model="form.dueDate"
              type="date"
              :label="t('projectManagement.obligation.dueDate')"
              density="comfortable"
              hide-details="auto"
              class="mt-3"
              prepend-inner-icon="mdi-calendar"
            />
            <v-alert
              v-if="form.status === 'satisfied' && !form.evidenceResourceId"
              class="mt-3"
              color="warning"
              variant="tonal"
              density="compact"
              icon="mdi-alert"
            >
              {{ t('projectManagement.obligation.dialog.satisfiedNeedsEvidence') }}
            </v-alert>
            <v-alert
              v-else-if="form.status === 'waived' && !form.note.trim()"
              class="mt-3"
              color="warning"
              variant="tonal"
              density="compact"
              icon="mdi-alert"
            >
              {{ t('projectManagement.obligation.dialog.waiveNeedsNote') }}
            </v-alert>
            <v-alert
              v-else-if="form.status === 'open' || form.status === 'inProgress'"
              class="mt-3"
              :color="formOverdue ? 'warning' : 'primary'"
              variant="tonal"
              density="compact"
              :icon="formOverdue ? 'mdi-alert' : 'mdi-information-outline'"
            >
              {{
                formOverdue
                  ? t('projectManagement.obligation.dialog.overdueNow')
                  : t('projectManagement.obligation.dialog.notOverdue')
              }}
            </v-alert>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.obligation.dialog.sectionNote') }}</div>
            <v-textarea
              v-model="form.note"
              :label="t('projectManagement.obligation.note')"
              :hint="t('projectManagement.obligation.dialog.noteHint')"
              persistent-hint
              density="comfortable"
              rows="2"
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

    <PmPickDocumentDialog
      v-model="pickOpen"
      :project-id="projectId"
      :project-code="projectCode"
      :hub-folder-id="hubFolderId"
      @pick="pickDocument"
      @hub-ready="emit('hubReady', $event)"
    />

    <PmPickWorkItemDialog v-model="workPickOpen" :project-id="projectId" @pick="pickWorkItem" />

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

<style scoped>
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.pm-obligation-status-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.pm-obligation-status-card {
  cursor: pointer;
}

@media (max-width: 600px) {
  .pm-obligation-status-grid {
    grid-template-columns: 1fr;
  }
}
</style>
