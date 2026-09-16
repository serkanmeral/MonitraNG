<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import PmPickDocumentDialog from '@/components/apps/project-management/PmPickDocumentDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreateAck,
  pmDateInput,
  pmDatePayload,
  pmDeleteAck,
  pmUpdateAck,
} from '@/services/projectManagementService';
import type { DiResource } from '@/types/apps/documentIntelligence';
import type { PmAckStatus, PmAcknowledgement, PmWbsItem } from '@/types/apps/projectManagement';
import { diPageResourceLabel } from '@/utils/diPageResource';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  projectCode: string;
  hubFolderId?: string | null;
  items: PmAcknowledgement[];
  wbs: PmWbsItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
  hubReady: [id: string];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const statusFilter = ref<'all' | 'pending' | 'overdue'>('all');
const search = ref('');
const page = ref(1);
const itemsPerPage = ref(25);
const dialog = ref(false);
const pickOpen = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmAcknowledgement | null>(null);
const deleting = ref(false);
const acknowledgingId = ref<string | null>(null);

const pageSizeOptions = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
  { value: 100, title: '100' },
];

const STATUS_META: Record<PmAckStatus, { icon: string; color: string }> = {
  pending: { icon: 'mdi-clock-outline', color: 'info' },
  acknowledged: { icon: 'mdi-check-circle-outline', color: 'success' },
  waived: { icon: 'mdi-minus-circle-outline', color: 'warning' },
};

const form = ref({
  title: '',
  resourceId: '',
  versionLabel: '',
  personName: '',
  personId: '',
  wbsId: '',
  status: 'pending' as PmAckStatus,
  dueDate: '',
  note: '',
});

const statusChoices = computed(() =>
  (['pending', 'acknowledged', 'waived'] as PmAckStatus[]).map((value) => ({
    value,
    title: t(`projectManagement.ack.status.${value}`),
    hint: t(`projectManagement.ack.dialog.statusHint.${value}`),
    icon: STATUS_META[value].icon,
    color: STATUS_META[value].color,
  })),
);

const statusMeta = computed(() => STATUS_META[form.value.status] || STATUS_META.pending);

const wbsById = computed(() => {
  const map = new Map<string, PmWbsItem>();
  for (const row of props.wbs) map.set(row.id, row);
  return map;
});

const wbsItems = computed(() => [
  { title: t('projectManagement.ack.projectLevel'), value: '' },
  ...props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
]);

const pendingCount = computed(() => props.items.filter((row) => row.pending).length);
const overdueCount = computed(() => props.items.filter((row) => row.overdue).length);

const rows = computed(() => {
  const query = search.value.trim().toLowerCase();
  return props.items.filter((row) => {
    if (statusFilter.value === 'pending' && !row.pending) return false;
    if (statusFilter.value === 'overdue' && !row.overdue) return false;
    if (!query) return true;
    const wbs = row.wbsId ? wbsById.value.get(row.wbsId) : null;
    const haystack = [row.title, row.personName, row.versionLabel, wbs?.wbsCode, wbs?.name]
      .filter(Boolean)
      .join(' ')
      .toLowerCase();
    return haystack.includes(query);
  });
});

watch([search, statusFilter], () => {
  page.value = 1;
});

const headers = computed(() => [
  { title: t('projectManagement.ack.document'), key: 'title', minWidth: 180 },
  { title: t('projectManagement.ack.person'), key: 'personName', minWidth: 140 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.fields.wbsCode'), key: 'wbs', minWidth: 140 },
  { title: t('projectManagement.ack.dueDate'), key: 'dueDate', width: 120 },
  { title: t('projectManagement.actions'), key: 'actions', width: 180, sortable: false, align: 'end' as const },
]);

const canSave = computed(() => {
  if (!form.value.title.trim() || !form.value.resourceId.trim() || !form.value.personName.trim()) return false;
  if (form.value.status === 'waived' && !form.value.note.trim()) return false;
  return true;
});

const formOverdue = computed(() => {
  if (form.value.status !== 'pending') return false;
  const due = form.value.dueDate;
  if (!due) return false;
  const today = new Date();
  const iso = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
  return due < iso;
});

function wbsName(id?: string | null) {
  if (!id) return t('projectManagement.ack.projectLevel');
  const row = wbsById.value.get(id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function statusLabel(status?: string | null) {
  const key = `projectManagement.ack.status.${status || 'pending'}`;
  const label = t(key);
  return label === key ? status || 'pending' : label;
}

function statusColor(row: PmAcknowledgement) {
  if (row.overdue) return 'error';
  if (row.status === 'acknowledged') return 'success';
  if (row.status === 'waived') return 'warning';
  return 'info';
}

function resourceHref(id: string) {
  return `/apps/document-intelligence/r/${encodeURIComponent(id)}`;
}

function selectStatus(status: PmAckStatus) {
  form.value.status = status;
}

function pickDocument(resource: DiResource) {
  form.value.resourceId = resource.id;
  form.value.title = diPageResourceLabel(resource);
  const version = Number(resource.currentVersionNumber);
  if (version >= 1) form.value.versionLabel = `v${version}`;
}

function clearDocument() {
  form.value.resourceId = '';
  form.value.title = '';
  form.value.versionLabel = '';
}

function openCreate() {
  editingId.value = null;
  form.value = {
    title: '',
    resourceId: '',
    versionLabel: '',
    personName: '',
    personId: '',
    wbsId: '',
    status: 'pending',
    dueDate: '',
    note: '',
  };
  dialog.value = true;
}

function openEdit(row: PmAcknowledgement) {
  editingId.value = row.id;
  form.value = {
    title: row.title,
    resourceId: row.resourceId,
    versionLabel: row.versionLabel || '',
    personName: row.personName,
    personId: row.personId || '',
    wbsId: row.wbsId || '',
    status: (row.status as PmAckStatus) || 'pending',
    dueDate: pmDateInput(row.dueDate),
    note: row.note || '',
  };
  dialog.value = true;
}

function payload() {
  return {
    resourceId: form.value.resourceId.trim(),
    title: form.value.title.trim(),
    versionLabel: form.value.versionLabel.trim() || null,
    personName: form.value.personName.trim(),
    personId: form.value.personId.trim() || null,
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
    if (editingId.value) await pmUpdateAck(editingId.value, payload());
    else await pmCreateAck(props.projectId, payload());
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.ackSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

async function acknowledge(row: PmAcknowledgement) {
  acknowledgingId.value = row.id;
  try {
    await pmUpdateAck(row.id, { status: 'acknowledged' });
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.ackSaved'),
      severity: 'success',
    });
    emit('changed');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    acknowledgingId.value = null;
  }
}

async function executeDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  try {
    await pmDeleteAck(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.ackDeleted'),
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
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.ack.hint') }}</div>
      <v-btn color="primary" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.ack.new') }}
      </v-btn>
    </div>

    <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-3">
      <div class="text-caption text-medium-emphasis">
        {{ t('projectManagement.ack.summaryLine', { pending: pendingCount, overdue: overdueCount }) }}
      </div>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-3 align-center">
      <v-text-field
        v-model="search"
        :label="t('projectManagement.ack.search')"
        density="comfortable"
        hide-details
        clearable
        style="max-width: 280px"
      />
      <v-chip :variant="statusFilter === 'all' ? 'flat' : 'tonal'" @click="statusFilter = 'all'">
        {{ t('projectManagement.ack.filterAll') }}
      </v-chip>
      <v-chip color="info" :variant="statusFilter === 'pending' ? 'flat' : 'tonal'" @click="statusFilter = 'pending'">
        {{ t('projectManagement.ack.status.pending') }}
      </v-chip>
      <v-chip color="error" :variant="statusFilter === 'overdue' ? 'flat' : 'tonal'" @click="statusFilter = 'overdue'">
        {{ t('projectManagement.ack.overdue') }}
      </v-chip>
    </div>

    <v-data-table
      v-model:page="page"
      v-model:items-per-page="itemsPerPage"
      :headers="headers"
      :items="rows"
      :loading="loading"
      item-value="id"
      density="comfortable"
      class="rounded-lg border"
      :items-per-page-options="pageSizeOptions"
    >
      <template #item.title="{ item }">
        <div>
          <NuxtLink v-if="item.resourceId" :to="resourceHref(item.resourceId)" class="text-primary">
            {{ item.title }}
          </NuxtLink>
          <span v-else>{{ item.title }}</span>
          <div v-if="item.versionLabel" class="text-caption text-medium-emphasis">{{ item.versionLabel }}</div>
        </div>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item)" variant="tonal">
          {{ statusLabel(item.status) }}
        </v-chip>
      </template>
      <template #item.wbs="{ item }">
        {{ wbsName(item.wbsId) }}
      </template>
      <template #item.dueDate="{ item }">
        {{ pmDateInput(item.dueDate) || '—' }}
      </template>
      <template #item.actions="{ item }">
        <div class="d-flex justify-end ga-1">
          <v-btn
            v-if="item.pending"
            size="small"
            variant="text"
            color="success"
            :loading="acknowledgingId === item.id"
            @click="acknowledge(item)"
          >
            {{ t('projectManagement.ack.markRead') }}
          </v-btn>
          <v-btn size="small" variant="text" @click="openEdit(item)">{{ t('projectManagement.edit') }}</v-btn>
          <v-btn icon size="small" variant="text" color="error" @click="deleteTarget = item">
            <TrashIcon size="18" />
          </v-btn>
        </div>
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.ack.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="720" scrollable>
      <v-card rounded="lg">
        <v-card-title class="d-flex align-start ga-3 px-6 py-4">
          <v-avatar :color="statusMeta.color" variant="tonal" rounded="lg">
            <v-icon :icon="editingId ? 'mdi-pencil-outline' : statusMeta.icon" />
          </v-avatar>
          <div class="flex-grow-1">
            <div>{{ editingId ? t('projectManagement.ack.edit') : t('projectManagement.ack.new') }}</div>
            <div class="text-body-2 text-medium-emphasis font-weight-regular mt-1">
              {{ editingId ? t('projectManagement.ack.dialog.subtitleEdit') : t('projectManagement.ack.dialog.subtitleNew') }}
            </div>
          </div>
        </v-card-title>
        <v-divider />
        <v-card-text class="d-flex flex-column ga-5 px-6 py-5">
          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.ack.dialog.sectionDocument') }}</div>
            <p class="text-caption text-medium-emphasis mb-3">{{ t('projectManagement.ack.dialog.sectionDocumentHint') }}</p>
            <div v-if="form.resourceId" class="d-flex align-center ga-2 flex-wrap mb-3">
              <NuxtLink :to="resourceHref(form.resourceId)" class="text-decoration-none" @click.stop>
                <v-chip size="small" color="primary" variant="tonal">
                  {{ form.title || form.resourceId }}
                </v-chip>
              </NuxtLink>
              <v-btn size="small" variant="text" @click="clearDocument">
                {{ t('projectManagement.decision.clearDocument') }}
              </v-btn>
            </div>
            <div v-else class="text-caption text-medium-emphasis mb-3">
              {{ t('projectManagement.ack.dialog.noDocument') }}
            </div>
            <v-btn variant="tonal" class="text-none align-self-start mb-3" @click="pickOpen = true">
              {{ t('projectManagement.pickDocument.open') }}
            </v-btn>
            <v-text-field
              v-model="form.versionLabel"
              :label="t('projectManagement.ack.version')"
              :hint="t('projectManagement.ack.dialog.versionHint')"
              persistent-hint
              density="comfortable"
              prepend-inner-icon="mdi-source-branch"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.ack.dialog.sectionPerson') }}</div>
            <v-text-field
              v-model="form.personName"
              :label="t('projectManagement.ack.person')"
              :placeholder="t('projectManagement.ack.dialog.personPlaceholder')"
              :hint="t('projectManagement.ack.dialog.personHint')"
              persistent-hint
              density="comfortable"
              prepend-inner-icon="mdi-account-outline"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.ack.dialog.sectionWhere') }}</div>
            <v-select
              v-model="form.wbsId"
              :items="wbsItems"
              :label="t('projectManagement.fields.wbsCode')"
              :hint="t('projectManagement.ack.dialog.wbsHint')"
              persistent-hint
              density="comfortable"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-2">{{ t('projectManagement.ack.dialog.sectionStatus') }}</div>
            <div class="pm-ack-status-grid">
              <v-card
                v-for="choice in statusChoices"
                :key="choice.value"
                :variant="form.status === choice.value ? 'tonal' : 'outlined'"
                :color="form.status === choice.value ? choice.color : undefined"
                rounded="lg"
                class="pm-ack-status-card pa-3"
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
              :label="t('projectManagement.ack.dueDate')"
              density="comfortable"
              hide-details="auto"
              class="mt-3"
              prepend-inner-icon="mdi-calendar"
            />
            <v-alert
              v-if="form.status === 'pending'"
              class="mt-3"
              :color="formOverdue ? 'warning' : 'primary'"
              variant="tonal"
              density="compact"
              :icon="formOverdue ? 'mdi-alert' : 'mdi-information-outline'"
            >
              {{ formOverdue ? t('projectManagement.ack.dialog.overdueNow') : t('projectManagement.ack.dialog.notOverdue') }}
            </v-alert>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.ack.dialog.sectionNote') }}</div>
            <v-textarea
              v-model="form.note"
              :label="t('projectManagement.ack.note')"
              :hint="t('projectManagement.ack.dialog.noteHint')"
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

    <v-dialog :model-value="Boolean(deleteTarget)" max-width="440" @update:model-value="onDeleteDialog">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.ack.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.ack.deleteConfirm') }}</v-card-text>
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

.pm-ack-status-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 8px;
}

.pm-ack-status-card {
  cursor: pointer;
  height: 100%;
}

.pm-ack-status-card:hover {
  border-color: rgba(var(--v-theme-primary), 0.4);
}

@media (min-width: 700px) {
  .pm-ack-status-grid {
    grid-template-columns: 1fr 1fr 1fr;
  }
}
</style>
