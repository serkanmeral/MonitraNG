<script setup lang="ts">
import { computed, ref } from 'vue';
import PmPickDocumentDialog from '@/components/apps/project-management/PmPickDocumentDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePmDate } from '@/composables/usePmDate';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import { diGetById } from '@/services/documentIntelligenceService';
import {
  pmCreateAuditPack,
  pmDateInput,
  pmDatePayload,
  pmDeleteAuditPack,
  pmUpdateAuditPack,
} from '@/services/projectManagementService';
import type { DiResource } from '@/types/apps/documentIntelligence';
import type {
  PmAuditPack,
  PmAuditPackKind,
  PmAuditPackStatus,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { diPageResourceLabel } from '@/utils/diPageResource';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  projectCode: string;
  hubFolderId?: string | null;
  items: PmAuditPack[];
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

const statusFilter = ref<'all' | 'open' | 'incomplete' | 'overdue'>('all');
const dialog = ref(false);
const pickOpen = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmAuditPack | null>(null);
const deleting = ref(false);
const issuingId = ref<string | null>(null);

const KIND_META: Record<PmAuditPackKind, { icon: string; color: string }> = {
  audit: { icon: 'mdi-shield-search', color: 'primary' },
  customer: { icon: 'mdi-account-tie-outline', color: 'info' },
  internal: { icon: 'mdi-office-building-outline', color: 'secondary' },
};

const STATUS_META: Record<PmAuditPackStatus, { icon: string; color: string }> = {
  draft: { icon: 'mdi-file-outline', color: 'info' },
  assembled: { icon: 'mdi-folder-check-outline', color: 'primary' },
  issued: { icon: 'mdi-send-check-outline', color: 'success' },
  withdrawn: { icon: 'mdi-undo-variant', color: 'warning' },
};

const form = ref({
  name: '',
  kind: 'audit' as PmAuditPackKind,
  wbsId: '',
  status: 'draft' as PmAuditPackStatus,
  dueDate: '',
  resources: [] as { id: string; title: string }[],
  recipient: '',
  note: '',
});

const kindChoices = computed(() =>
  (['audit', 'customer', 'internal'] as PmAuditPackKind[]).map((value) => ({
    value,
    title: t(`projectManagement.auditPack.kind.${value}`),
    hint: t(`projectManagement.auditPack.dialog.kindHint.${value}`),
    icon: KIND_META[value].icon,
    color: KIND_META[value].color,
  })),
);

const statusChoices = computed(() =>
  (['draft', 'assembled', 'issued', 'withdrawn'] as PmAuditPackStatus[]).map((value) => ({
    value,
    title: t(`projectManagement.auditPack.status.${value}`),
    hint: t(`projectManagement.auditPack.dialog.statusHint.${value}`),
    icon: STATUS_META[value].icon,
    color: STATUS_META[value].color,
  })),
);

const statusMeta = computed(() => STATUS_META[form.value.status] || STATUS_META.draft);

const wbsItems = computed(() => [
  { title: t('projectManagement.auditPack.projectLevel'), value: '' },
  ...props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
]);

const openCount = computed(() => props.items.filter((row) => row.open).length);
const incompleteCount = computed(() => props.items.filter((row) => row.incomplete).length);
const overdueCount = computed(() => props.items.filter((row) => row.overdue).length);

const rows = computed(() => {
  if (statusFilter.value === 'open') return props.items.filter((row) => row.open);
  if (statusFilter.value === 'incomplete') return props.items.filter((row) => row.incomplete);
  if (statusFilter.value === 'overdue') return props.items.filter((row) => row.overdue);
  return props.items;
});

const headers = computed(() => [
  { title: t('projectManagement.fields.name'), key: 'name', minWidth: 180 },
  { title: t('projectManagement.fields.kind'), key: 'kind', width: 120 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.auditPack.items'), key: 'itemCount', width: 90 },
  { title: t('projectManagement.auditPack.dueDate'), key: 'dueDate', width: 120 },
  { title: t('projectManagement.actions'), key: 'actions', width: 180, sortable: false, align: 'end' as const },
]);

const selectedIds = computed(() => form.value.resources.map((row) => row.id));

const canSave = computed(() => {
  if (!form.value.name.trim()) return false;
  if (form.value.status === 'withdrawn' && !form.value.note.trim()) return false;
  if (form.value.status === 'issued' && form.value.resources.length === 0) return false;
  return true;
});

const formOverdue = computed(() => {
  if (form.value.status === 'issued' || form.value.status === 'withdrawn') return false;
  if (!form.value.dueDate) return false;
  return form.value.dueDate < new Date().toISOString().slice(0, 10);
});

function wbsName(id?: string | null) {
  if (!id) return t('projectManagement.auditPack.projectLevel');
  const row = props.wbs.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function statusLabel(status?: string | null) {
  const key = `projectManagement.auditPack.status.${status || 'draft'}`;
  const label = t(key);
  return label === key ? status || 'draft' : label;
}

function kindLabel(kind?: string | null) {
  const key = `projectManagement.auditPack.kind.${kind || 'audit'}`;
  const label = t(key);
  return label === key ? kind || 'audit' : label;
}

function statusColor(row: PmAuditPack) {
  if (row.status === 'withdrawn') return 'warning';
  if (row.overdue) return 'error';
  if (row.status === 'issued') return 'success';
  if (row.incomplete) return 'warning';
  return 'info';
}

function resourceHref(id: string) {
  return `/apps/document-intelligence/r/${encodeURIComponent(id)}`;
}

function emptyForm() {
  return {
    name: '',
    kind: 'audit' as PmAuditPackKind,
    wbsId: '',
    status: 'draft' as PmAuditPackStatus,
    dueDate: '',
    resources: [] as { id: string; title: string }[],
    recipient: '',
    note: '',
  };
}

async function resolveDocumentLabel(id: string) {
  if (!id) return id;
  try {
    const resource = await diGetById(id);
    return diPageResourceLabel(resource) || id;
  } catch {
    return id;
  }
}

function openCreate() {
  editingId.value = null;
  form.value = emptyForm();
  dialog.value = true;
}

async function openEdit(row: PmAuditPack) {
  editingId.value = row.id;
  const ids = row.resourceIds || [];
  form.value = {
    name: row.name,
    kind: (row.kind as PmAuditPackKind) || 'audit',
    wbsId: row.wbsId || '',
    status: (row.status as PmAuditPackStatus) || 'draft',
    dueDate: pmDateInput(row.dueDate),
    resources: ids.map((id) => ({ id, title: id })),
    recipient: row.recipient || '',
    note: row.note || '',
  };
  dialog.value = true;
  const labels = await Promise.all(ids.map((id) => resolveDocumentLabel(id)));
  if (editingId.value !== row.id) return;
  form.value.resources = ids.map((id, index) => ({ id, title: labels[index] || id }));
}

function selectKind(kind: PmAuditPackKind) {
  form.value.kind = kind;
}

function selectStatus(status: PmAuditPackStatus) {
  form.value.status = status;
}

function pickDocument(resource: DiResource) {
  const id = resource.id;
  if (form.value.resources.some((row) => row.id === id)) return;
  form.value.resources = [
    ...form.value.resources,
    { id, title: diPageResourceLabel(resource) || id },
  ];
}

function removeResource(id: string) {
  form.value.resources = form.value.resources.filter((row) => row.id !== id);
}

function payload() {
  return {
    name: form.value.name.trim(),
    kind: form.value.kind,
    wbsId: form.value.wbsId || null,
    status: form.value.status,
    dueDate: pmDatePayload(form.value.dueDate),
    resourceIds: form.value.resources.map((row) => row.id),
    recipient: form.value.recipient.trim() || null,
    note: form.value.note.trim() || null,
  };
}

async function save() {
  if (!canSave.value) return;
  saving.value = true;
  try {
    if (editingId.value) await pmUpdateAuditPack(editingId.value, payload());
    else await pmCreateAuditPack(props.projectId, payload());
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.auditPackSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function issue(row: PmAuditPack) {
  if (!row.resourceIds?.length) return;
  issuingId.value = row.id;
  try {
    await pmUpdateAuditPack(row.id, { status: 'issued', resourceIds: row.resourceIds });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.auditPackSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    issuingId.value = null;
  }
}

async function executeDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteAuditPack(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.auditPackDeleted'),
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
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.auditPack.hint') }}</div>
      <v-btn color="primary" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.auditPack.new') }}
      </v-btn>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip size="small" color="info" variant="tonal">
        {{ t('projectManagement.auditPack.open') }} · {{ openCount }}
      </v-chip>
      <v-chip size="small" :color="incompleteCount ? 'warning' : 'default'" variant="tonal">
        {{ t('projectManagement.auditPack.incomplete') }} · {{ incompleteCount }}
      </v-chip>
      <v-chip size="small" :color="overdueCount ? 'error' : 'success'" variant="tonal">
        {{ t('projectManagement.auditPack.overdue') }} · {{ overdueCount }}
      </v-chip>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip :variant="statusFilter === 'all' ? 'flat' : 'tonal'" @click="statusFilter = 'all'">
        {{ t('projectManagement.auditPack.filterAll') }}
      </v-chip>
      <v-chip color="info" :variant="statusFilter === 'open' ? 'flat' : 'tonal'" @click="statusFilter = 'open'">
        {{ t('projectManagement.auditPack.open') }}
      </v-chip>
      <v-chip color="warning" :variant="statusFilter === 'incomplete' ? 'flat' : 'tonal'" @click="statusFilter = 'incomplete'">
        {{ t('projectManagement.auditPack.incomplete') }}
      </v-chip>
      <v-chip color="error" :variant="statusFilter === 'overdue' ? 'flat' : 'tonal'" @click="statusFilter = 'overdue'">
        {{ t('projectManagement.auditPack.overdue') }}
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
      <template #item.name="{ item }">
        <div>
          {{ item.name }}
          <div class="text-caption text-medium-emphasis">{{ wbsName(item.wbsId) }}</div>
        </div>
      </template>
      <template #item.kind="{ item }">
        {{ kindLabel(item.kind) }}
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item)" variant="tonal">
          {{ statusLabel(item.status) }}
        </v-chip>
      </template>
      <template #item.itemCount="{ item }">
        <span v-if="item.itemCount">{{ item.itemCount }}</span>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
      <template #item.dueDate="{ item }">
        {{ formatPmDateOrDash(item.dueDate) }}
      </template>
      <template #item.actions="{ item }">
        <div class="d-flex justify-end ga-1">
          <v-btn
            v-if="item.open && item.itemCount"
            size="small"
            variant="text"
            color="success"
            :loading="issuingId === item.id"
            @click="issue(item)"
          >
            {{ t('projectManagement.auditPack.markIssued') }}
          </v-btn>
          <v-btn size="small" variant="text" @click="openEdit(item)">{{ t('projectManagement.edit') }}</v-btn>
          <v-btn icon size="small" variant="text" color="error" @click="deleteTarget = item">
            <TrashIcon size="18" />
          </v-btn>
        </div>
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.auditPack.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="720" scrollable>
      <v-card rounded="lg">
        <v-card-title class="d-flex align-start ga-3 px-6 py-4">
          <v-avatar :color="statusMeta.color" variant="tonal" rounded="lg">
            <v-icon :icon="editingId ? 'mdi-pencil-outline' : statusMeta.icon" />
          </v-avatar>
          <div class="flex-grow-1">
            <div>{{ editingId ? t('projectManagement.auditPack.edit') : t('projectManagement.auditPack.new') }}</div>
            <div class="text-body-2 text-medium-emphasis font-weight-regular mt-1">
              {{
                editingId
                  ? t('projectManagement.auditPack.dialog.subtitleEdit')
                  : t('projectManagement.auditPack.dialog.subtitleNew')
              }}
            </div>
          </div>
        </v-card-title>
        <v-divider />
        <v-card-text class="d-flex flex-column ga-5 px-6 py-5">
          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.auditPack.dialog.sectionName') }}</div>
            <v-text-field
              v-model="form.name"
              :label="t('projectManagement.fields.name')"
              density="comfortable"
              prepend-inner-icon="mdi-package-variant-closed"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-2">{{ t('projectManagement.auditPack.dialog.sectionKind') }}</div>
            <div class="pm-audit-card-grid">
              <v-card
                v-for="choice in kindChoices"
                :key="choice.value"
                :variant="form.kind === choice.value ? 'tonal' : 'outlined'"
                :color="form.kind === choice.value ? choice.color : undefined"
                rounded="lg"
                class="pm-audit-card pa-3"
                role="button"
                @click="selectKind(choice.value)"
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
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.auditPack.dialog.sectionEvidence') }}</div>
            <p class="text-caption text-medium-emphasis mb-3">
              {{ t('projectManagement.auditPack.dialog.sectionEvidenceHint') }}
            </p>
            <div v-if="form.resources.length" class="d-flex flex-wrap ga-2 mb-3">
              <v-chip
                v-for="doc in form.resources"
                :key="doc.id"
                size="small"
                color="primary"
                variant="tonal"
                closable
                @click:close="removeResource(doc.id)"
              >
                <NuxtLink :to="resourceHref(doc.id)" class="text-decoration-none" @click.stop>
                  {{ doc.title || doc.id }}
                </NuxtLink>
              </v-chip>
            </div>
            <div v-else class="text-caption text-medium-emphasis mb-3">
              {{ t('projectManagement.auditPack.noEvidence') }}
            </div>
            <v-btn variant="tonal" class="text-none" @click="pickOpen = true">
              {{ t('projectManagement.pickDocument.open') }}
            </v-btn>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.auditPack.dialog.sectionWhere') }}</div>
            <v-select
              v-model="form.wbsId"
              :items="wbsItems"
              :label="t('projectManagement.fields.wbsCode')"
              :hint="t('projectManagement.auditPack.dialog.wbsHint')"
              persistent-hint
              density="comfortable"
              class="mb-3"
            />
            <v-text-field
              v-model="form.recipient"
              :label="t('projectManagement.auditPack.recipient')"
              :hint="t('projectManagement.auditPack.dialog.recipientHint')"
              persistent-hint
              density="comfortable"
              prepend-inner-icon="mdi-account-outline"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-2">{{ t('projectManagement.auditPack.dialog.sectionStatus') }}</div>
            <div class="pm-audit-card-grid">
              <v-card
                v-for="choice in statusChoices"
                :key="choice.value"
                :variant="form.status === choice.value ? 'tonal' : 'outlined'"
                :color="form.status === choice.value ? choice.color : undefined"
                rounded="lg"
                class="pm-audit-card pa-3"
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
              :label="t('projectManagement.auditPack.dueDate')"
              density="comfortable"
              hide-details="auto"
              class="mt-3"
              prepend-inner-icon="mdi-calendar"
            />
            <v-alert
              v-if="form.status === 'issued' && !form.resources.length"
              class="mt-3"
              color="warning"
              variant="tonal"
              density="compact"
              icon="mdi-alert"
            >
              {{ t('projectManagement.auditPack.dialog.issuedNeedsEvidence') }}
            </v-alert>
            <v-alert
              v-else-if="form.status === 'withdrawn' && !form.note.trim()"
              class="mt-3"
              color="warning"
              variant="tonal"
              density="compact"
              icon="mdi-alert"
            >
              {{ t('projectManagement.auditPack.dialog.withdrawNeedsNote') }}
            </v-alert>
            <v-alert
              v-else-if="form.status === 'draft' || form.status === 'assembled'"
              class="mt-3"
              :color="formOverdue ? 'warning' : form.resources.length ? 'primary' : 'warning'"
              variant="tonal"
              density="compact"
              :icon="formOverdue || !form.resources.length ? 'mdi-alert' : 'mdi-information-outline'"
            >
              {{
                !form.resources.length
                  ? t('projectManagement.auditPack.dialog.incompleteNow')
                  : formOverdue
                    ? t('projectManagement.auditPack.dialog.overdueNow')
                    : t('projectManagement.auditPack.dialog.notOverdue')
              }}
            </v-alert>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.auditPack.dialog.sectionNote') }}</div>
            <v-textarea
              v-model="form.note"
              :label="t('projectManagement.auditPack.note')"
              :hint="t('projectManagement.auditPack.dialog.noteHint')"
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
      :exclude-resource-ids="selectedIds"
      @pick="pickDocument"
      @hub-ready="emit('hubReady', $event)"
    />

    <v-dialog :model-value="Boolean(deleteTarget)" max-width="440" @update:model-value="onDeleteDialog">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.auditPack.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.auditPack.deleteConfirm') }}</v-card-text>
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

.pm-audit-card-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.pm-audit-card {
  cursor: pointer;
}

@media (max-width: 600px) {
  .pm-audit-card-grid {
    grid-template-columns: 1fr;
  }
}
</style>
