<script setup lang="ts">
import { computed, ref } from 'vue';
import PmPickDocumentDialog from '@/components/apps/project-management/PmPickDocumentDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePmDate } from '@/composables/usePmDate';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import { diGetById } from '@/services/documentIntelligenceService';
import {
  pmCreateStakeholder,
  pmDateInput,
  pmDatePayload,
  pmDeleteStakeholder,
  pmUpdateStakeholder,
} from '@/services/projectManagementService';
import type { DiResource } from '@/types/apps/documentIntelligence';
import type {
  PmStakeholder,
  PmStakeholderKind,
  PmStakeholderStatus,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { diPageResourceLabel } from '@/utils/diPageResource';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  projectCode: string;
  hubFolderId?: string | null;
  items: PmStakeholder[];
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
const search = ref('');
const dialog = ref(false);
const pickOpen = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmStakeholder | null>(null);
const deleting = ref(false);
const activatingId = ref<string | null>(null);

const KIND_META: Record<PmStakeholderKind, { icon: string; color: string }> = {
  customer: { icon: 'mdi-account-tie-outline', color: 'info' },
  supplier: { icon: 'mdi-truck-outline', color: 'secondary' },
  consultant: { icon: 'mdi-briefcase-outline', color: 'primary' },
  regulator: { icon: 'mdi-shield-account-outline', color: 'warning' },
  sponsor: { icon: 'mdi-account-star-outline', color: 'success' },
  other: { icon: 'mdi-account-outline', color: 'default' },
};

const STATUS_META: Record<PmStakeholderStatus, { icon: string; color: string }> = {
  invited: { icon: 'mdi-email-outline', color: 'info' },
  active: { icon: 'mdi-check-decagram-outline', color: 'success' },
  revoked: { icon: 'mdi-cancel', color: 'warning' },
};

const form = ref(emptyForm());

const kindChoices = computed(() =>
  (['customer', 'supplier', 'consultant', 'regulator', 'sponsor', 'other'] as PmStakeholderKind[]).map((value) => ({
    value,
    title: t(`projectManagement.stakeholder.kind.${value}`),
    hint: t(`projectManagement.stakeholder.dialog.kindHint.${value}`),
    icon: KIND_META[value].icon,
    color: KIND_META[value].color,
  })),
);

const statusChoices = computed(() =>
  (['invited', 'active', 'revoked'] as PmStakeholderStatus[]).map((value) => ({
    value,
    title: t(`projectManagement.stakeholder.status.${value}`),
    hint: t(`projectManagement.stakeholder.dialog.statusHint.${value}`),
    icon: STATUS_META[value].icon,
    color: STATUS_META[value].color,
  })),
);

const statusMeta = computed(() => STATUS_META[form.value.status] || STATUS_META.invited);

const wbsItems = computed(() => [
  { title: t('projectManagement.stakeholder.projectLevel'), value: '' },
  ...props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
]);

const openCount = computed(() => props.items.filter((row) => row.open).length);
const incompleteCount = computed(() => props.items.filter((row) => row.incomplete).length);
const overdueCount = computed(() => props.items.filter((row) => row.overdue).length);

const rows = computed(() => {
  let list = props.items;
  if (statusFilter.value === 'open') list = list.filter((row) => row.open);
  else if (statusFilter.value === 'incomplete') list = list.filter((row) => row.incomplete);
  else if (statusFilter.value === 'overdue') list = list.filter((row) => row.overdue);
  const q = search.value.trim().toLowerCase();
  if (!q) return list;
  return list.filter((row) =>
    [row.name, row.organization, row.email, kindLabel(row.kind), statusLabel(row.status)]
      .some((value) => String(value || '').toLowerCase().includes(q)),
  );
});

const headers = computed(() => [
  { title: t('projectManagement.fields.name'), key: 'name', minWidth: 180 },
  { title: t('projectManagement.fields.kind'), key: 'kind', width: 130 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.stakeholder.items'), key: 'itemCount', width: 90 },
  { title: t('projectManagement.stakeholder.accessUntil'), key: 'accessUntil', width: 120 },
  { title: t('projectManagement.actions'), key: 'actions', width: 180, sortable: false, align: 'end' as const },
]);

const selectedIds = computed(() => form.value.resources.map((row) => row.id));

const canSave = computed(() => {
  if (!form.value.name.trim()) return false;
  if (form.value.status === 'revoked' && !form.value.note.trim()) return false;
  return true;
});

const formOverdue = computed(() => {
  if (form.value.status === 'revoked') return false;
  if (!form.value.accessUntil) return false;
  return form.value.accessUntil < new Date().toISOString().slice(0, 10);
});

function emptyForm() {
  return {
    name: '',
    organization: '',
    kind: 'customer' as PmStakeholderKind,
    email: '',
    wbsId: '',
    status: 'invited' as PmStakeholderStatus,
    accessUntil: '',
    resources: [] as { id: string; title: string }[],
    note: '',
  };
}

function wbsName(id?: string | null) {
  if (!id) return t('projectManagement.stakeholder.projectLevel');
  const row = props.wbs.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function statusLabel(status?: string | null) {
  const key = `projectManagement.stakeholder.status.${status || 'invited'}`;
  const label = t(key);
  return label === key ? status || 'invited' : label;
}

function kindLabel(kind?: string | null) {
  const key = `projectManagement.stakeholder.kind.${kind || 'customer'}`;
  const label = t(key);
  return label === key ? kind || 'customer' : label;
}

function kindColor(kind?: string | null) {
  const key = (kind || 'customer') as PmStakeholderKind;
  return KIND_META[key]?.color || 'default';
}

function statusColor(row: PmStakeholder) {
  if (row.status === 'revoked') return 'warning';
  if (row.overdue) return 'error';
  if (row.incomplete) return 'warning';
  if (row.status === 'active') return 'success';
  return 'info';
}

function resourceHref(id: string) {
  return `/apps/document-intelligence/r/${encodeURIComponent(id)}`;
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

async function openEdit(row: PmStakeholder) {
  editingId.value = row.id;
  const ids = row.resourceIds || [];
  form.value = {
    name: row.name,
    organization: row.organization || '',
    kind: (row.kind as PmStakeholderKind) || 'customer',
    email: row.email || '',
    wbsId: row.wbsId || '',
    status: (row.status as PmStakeholderStatus) || 'invited',
    accessUntil: pmDateInput(row.accessUntil),
    resources: ids.map((id) => ({ id, title: id })),
    note: row.note || '',
  };
  dialog.value = true;
  const labels = await Promise.all(ids.map((id) => resolveDocumentLabel(id)));
  if (editingId.value !== row.id) return;
  form.value.resources = ids.map((id, index) => ({ id, title: labels[index] || id }));
}

function selectKind(kind: PmStakeholderKind) {
  form.value.kind = kind;
}

function selectStatus(status: PmStakeholderStatus) {
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
    organization: form.value.organization.trim() || null,
    kind: form.value.kind,
    email: form.value.email.trim() || null,
    wbsId: form.value.wbsId || null,
    status: form.value.status,
    accessUntil: pmDatePayload(form.value.accessUntil),
    resourceIds: form.value.resources.map((row) => row.id),
    note: form.value.note.trim() || null,
  };
}

async function save() {
  if (!canSave.value) return;
  saving.value = true;
  try {
    if (editingId.value) await pmUpdateStakeholder(editingId.value, payload());
    else await pmCreateStakeholder(props.projectId, payload());
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.stakeholderSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function activate(row: PmStakeholder) {
  activatingId.value = row.id;
  try {
    await pmUpdateStakeholder(row.id, { status: 'active', resourceIds: row.resourceIds });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.stakeholderSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    activatingId.value = null;
  }
}

async function executeDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteStakeholder(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.stakeholderDeleted'),
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
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.stakeholder.hint') }}</div>
      <v-btn color="primary" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.stakeholder.new') }}
      </v-btn>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip size="small" color="info" variant="tonal">
        {{ t('projectManagement.stakeholder.open') }} · {{ openCount }}
      </v-chip>
      <v-chip size="small" :color="incompleteCount ? 'warning' : 'default'" variant="tonal">
        {{ t('projectManagement.stakeholder.incomplete') }} · {{ incompleteCount }}
      </v-chip>
      <v-chip size="small" :color="overdueCount ? 'error' : 'success'" variant="tonal">
        {{ t('projectManagement.stakeholder.overdue') }} · {{ overdueCount }}
      </v-chip>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4 align-center">
      <v-chip :variant="statusFilter === 'all' ? 'flat' : 'tonal'" @click="statusFilter = 'all'">
        {{ t('projectManagement.stakeholder.filterAll') }}
      </v-chip>
      <v-chip color="info" :variant="statusFilter === 'open' ? 'flat' : 'tonal'" @click="statusFilter = 'open'">
        {{ t('projectManagement.stakeholder.open') }}
      </v-chip>
      <v-chip color="warning" :variant="statusFilter === 'incomplete' ? 'flat' : 'tonal'" @click="statusFilter = 'incomplete'">
        {{ t('projectManagement.stakeholder.incomplete') }}
      </v-chip>
      <v-chip color="error" :variant="statusFilter === 'overdue' ? 'flat' : 'tonal'" @click="statusFilter = 'overdue'">
        {{ t('projectManagement.stakeholder.overdue') }}
      </v-chip>
      <v-spacer />
      <v-text-field
        v-model="search"
        :label="t('projectManagement.stakeholder.search')"
        density="compact"
        hide-details
        clearable
        prepend-inner-icon="mdi-magnify"
        style="max-width: 260px"
      />
    </div>

    <v-data-table
      :headers="headers"
      :items="rows"
      :loading="loading"
      item-value="id"
      density="comfortable"
      class="rounded-lg border"
      :items-per-page="25"
    >
      <template #item.name="{ item }">
        <div>
          {{ item.name }}
          <div class="text-caption text-medium-emphasis">
            {{ item.organization || wbsName(item.wbsId) }}
          </div>
        </div>
      </template>
      <template #item.kind="{ item }">
        <v-chip size="small" :color="kindColor(item.kind)" variant="tonal">
          {{ kindLabel(item.kind) }}
        </v-chip>
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
      <template #item.accessUntil="{ item }">
        {{ formatPmDateOrDash(item.accessUntil) }}
      </template>
      <template #item.actions="{ item }">
        <div class="d-flex justify-end ga-1">
          <v-btn
            v-if="item.status === 'invited'"
            size="small"
            variant="text"
            color="success"
            :loading="activatingId === item.id"
            @click="activate(item)"
          >
            {{ t('projectManagement.stakeholder.markActive') }}
          </v-btn>
          <v-btn size="small" variant="text" @click="openEdit(item)">{{ t('projectManagement.edit') }}</v-btn>
          <v-btn icon size="small" variant="text" color="error" @click="deleteTarget = item">
            <TrashIcon size="18" />
          </v-btn>
        </div>
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.stakeholder.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="720" scrollable>
      <v-card rounded="lg">
        <v-card-title class="d-flex align-start ga-3 px-6 py-4">
          <v-avatar :color="statusMeta.color" variant="tonal" rounded="lg">
            <v-icon :icon="editingId ? 'mdi-pencil-outline' : statusMeta.icon" />
          </v-avatar>
          <div class="flex-grow-1">
            <div>{{ editingId ? t('projectManagement.stakeholder.edit') : t('projectManagement.stakeholder.new') }}</div>
            <div class="text-body-2 text-medium-emphasis font-weight-regular mt-1">
              {{
                editingId
                  ? t('projectManagement.stakeholder.dialog.subtitleEdit')
                  : t('projectManagement.stakeholder.dialog.subtitleNew')
              }}
            </div>
          </div>
        </v-card-title>
        <v-divider />
        <v-card-text class="d-flex flex-column ga-5 px-6 py-5">
          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.stakeholder.dialog.sectionWho') }}</div>
            <v-text-field
              v-model="form.name"
              :label="t('projectManagement.fields.name')"
              density="comfortable"
              prepend-inner-icon="mdi-account-outline"
              class="mb-3"
            />
            <v-text-field
              v-model="form.organization"
              :label="t('projectManagement.stakeholder.organization')"
              density="comfortable"
              prepend-inner-icon="mdi-office-building-outline"
              class="mb-3"
            />
            <v-text-field
              v-model="form.email"
              :label="t('projectManagement.stakeholder.email')"
              :hint="t('projectManagement.stakeholder.dialog.emailHint')"
              persistent-hint
              density="comfortable"
              prepend-inner-icon="mdi-email-outline"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-2">{{ t('projectManagement.stakeholder.dialog.sectionKind') }}</div>
            <div class="pm-stakeholder-card-grid">
              <v-card
                v-for="choice in kindChoices"
                :key="choice.value"
                :variant="form.kind === choice.value ? 'tonal' : 'outlined'"
                :color="form.kind === choice.value ? choice.color : undefined"
                rounded="lg"
                class="pm-stakeholder-card pa-3"
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
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.stakeholder.dialog.sectionDocs') }}</div>
            <p class="text-caption text-medium-emphasis mb-3">
              {{ t('projectManagement.stakeholder.dialog.sectionDocsHint') }}
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
              {{ t('projectManagement.stakeholder.noDocuments') }}
            </div>
            <v-btn variant="tonal" class="text-none" @click="pickOpen = true">
              {{ t('projectManagement.pickDocument.open') }}
            </v-btn>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.stakeholder.dialog.sectionWhere') }}</div>
            <v-select
              v-model="form.wbsId"
              :items="wbsItems"
              :label="t('projectManagement.fields.wbsCode')"
              :hint="t('projectManagement.stakeholder.dialog.wbsHint')"
              persistent-hint
              density="comfortable"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-2">{{ t('projectManagement.stakeholder.dialog.sectionStatus') }}</div>
            <div class="pm-stakeholder-card-grid pm-stakeholder-card-grid--status">
              <v-card
                v-for="choice in statusChoices"
                :key="choice.value"
                :variant="form.status === choice.value ? 'tonal' : 'outlined'"
                :color="form.status === choice.value ? choice.color : undefined"
                rounded="lg"
                class="pm-stakeholder-card pa-3"
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
              v-model="form.accessUntil"
              type="date"
              :label="t('projectManagement.stakeholder.accessUntil')"
              density="comfortable"
              hide-details="auto"
              class="mt-3"
              prepend-inner-icon="mdi-calendar"
            />
            <v-alert
              v-if="form.status === 'revoked' && !form.note.trim()"
              class="mt-3"
              color="warning"
              variant="tonal"
              density="compact"
              icon="mdi-alert"
            >
              {{ t('projectManagement.stakeholder.dialog.revokeNeedsNote') }}
            </v-alert>
            <v-alert
              v-else-if="form.status !== 'revoked'"
              class="mt-3"
              :color="formOverdue || !form.resources.length ? 'warning' : 'primary'"
              variant="tonal"
              density="compact"
              :icon="formOverdue || !form.resources.length ? 'mdi-alert' : 'mdi-information-outline'"
            >
              {{
                !form.resources.length
                  ? t('projectManagement.stakeholder.dialog.incompleteNow')
                  : formOverdue
                    ? t('projectManagement.stakeholder.dialog.overdueNow')
                    : t('projectManagement.stakeholder.dialog.notOverdue')
              }}
            </v-alert>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.stakeholder.dialog.sectionNote') }}</div>
            <v-textarea
              v-model="form.note"
              :label="t('projectManagement.stakeholder.note')"
              :hint="t('projectManagement.stakeholder.dialog.noteHint')"
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
        <v-card-title>{{ t('projectManagement.stakeholder.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.stakeholder.deleteConfirm') }}</v-card-text>
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

.pm-stakeholder-card-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.pm-stakeholder-card-grid--status {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.pm-stakeholder-card {
  cursor: pointer;
}

@media (max-width: 600px) {
  .pm-stakeholder-card-grid,
  .pm-stakeholder-card-grid--status {
    grid-template-columns: 1fr;
  }
}
</style>
