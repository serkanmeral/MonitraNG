<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import { diSearch } from '@/services/documentIntelligenceService';
import {
  pmCreateDecision,
  pmDeleteDecision,
  pmUpdateDecision,
} from '@/services/projectManagementService';
import type { DiResource } from '@/types/apps/documentIntelligence';
import type {
  PmDecision,
  PmDecisionKind,
  PmDecisionStatus,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  decisions: PmDecision[];
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
const deleteTarget = ref<PmDecision | null>(null);
const deleting = ref(false);
const docQuery = ref('');
const docHits = ref<DiResource[]>([]);
const docSearching = ref(false);

const form = ref({
  title: '',
  body: '',
  kind: 'general' as PmDecisionKind,
  status: 'open' as PmDecisionStatus,
  wbsIds: [] as string[],
  workItemIds: [] as string[],
  documentId: '' as string,
  documentName: '' as string,
  resourceIds: [] as string[],
});

const kindItems = computed(() => [
  { title: t('projectManagement.decision.kind.general'), value: 'general' },
  { title: t('projectManagement.decision.kind.scopeChange'), value: 'scopeChange' },
]);

const statusItems = computed(() => [
  { title: t('projectManagement.decision.status.open'), value: 'open' },
  { title: t('projectManagement.decision.status.accepted'), value: 'accepted' },
  { title: t('projectManagement.decision.status.superseded'), value: 'superseded' },
]);

const wbsItems = computed(() =>
  props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
);

const workItemItems = computed(() =>
  props.wbs
    .filter((row) => row.workItemId && row.workItemKey)
    .map((row) => ({
      title: `${row.workItemKey} · ${row.name}`,
      value: row.workItemId as string,
    })),
);

const headers = computed(() => [
  { title: t('projectManagement.fields.name'), key: 'title', minWidth: 200 },
  { title: t('projectManagement.fields.kind'), key: 'kind', width: 140 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 120 },
  { title: t('projectManagement.decision.impacts'), key: 'impacts', minWidth: 220 },
  { title: t('projectManagement.actions'), key: 'actions', width: 120, sortable: false, align: 'end' as const },
]);

function kindLabel(kind?: string | null) {
  const key = `projectManagement.decision.kind.${kind || 'general'}`;
  const label = t(key);
  return label === key ? (kind || 'general') : label;
}

function statusLabel(status?: string | null) {
  const key = `projectManagement.decision.status.${status || 'open'}`;
  const label = t(key);
  return label === key ? (status || 'open') : label;
}

function statusColor(status?: string | null) {
  if (status === 'accepted') return 'success';
  if (status === 'superseded') return 'default';
  return 'info';
}

function wbsName(id: string) {
  const row = props.wbs.find((item) => item.id === id);
  if (!row) return id;
  return `${row.wbsCode || '—'} ${row.name}`;
}

function workItemLabel(id: string) {
  const row = props.wbs.find((item) => item.workItemId === id);
  return row?.workItemKey || id.slice(0, 8);
}

function impactText(row: PmDecision) {
  const bits: string[] = [];
  (row.wbsIds || []).forEach((id) => bits.push(wbsName(id)));
  (row.workItemIds || []).forEach((id) => bits.push(workItemLabel(id)));
  if (row.documentName) bits.push(row.documentName);
  else if (row.documentId) bits.push(row.documentId.slice(0, 8));
  (row.resourceIds || []).forEach((id) => bits.push(id.slice(0, 8)));
  return bits;
}

function openCreate() {
  editingId.value = null;
  form.value = {
    title: '',
    body: '',
    kind: 'general',
    status: 'open',
    wbsIds: [],
    workItemIds: [],
    documentId: '',
    documentName: '',
    resourceIds: [],
  };
  docQuery.value = '';
  docHits.value = [];
  dialog.value = true;
}

function openEdit(row: PmDecision) {
  editingId.value = row.id;
  form.value = {
    title: row.title,
    body: row.body || '',
    kind: (row.kind as PmDecisionKind) || 'general',
    status: (row.status as PmDecisionStatus) || 'open',
    wbsIds: [...(row.wbsIds || [])],
    workItemIds: [...(row.workItemIds || [])],
    documentId: row.documentId || '',
    documentName: row.documentName || '',
    resourceIds: [...(row.resourceIds || [])],
  };
  docQuery.value = '';
  docHits.value = [];
  dialog.value = true;
}

async function searchDocs() {
  const q = docQuery.value.trim();
  if (!q) {
    docHits.value = [];
    return;
  }
  docSearching.value = true;
  try {
    const result = await diSearch(q, 0, 12);
    docHits.value = (result.items || []).filter((item) => item.type !== 'folder');
  } catch (error) {
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    docSearching.value = false;
  }
}

function pickDocument(resource: DiResource, asOfficial: boolean) {
  if (asOfficial) {
    form.value.documentId = resource.id;
    form.value.documentName = resource.title || resource.name;
  } else if (!form.value.resourceIds.includes(resource.id)) {
    form.value.resourceIds = [...form.value.resourceIds, resource.id];
  }
  docHits.value = [];
  docQuery.value = '';
}

function clearOfficialDocument() {
  form.value.documentId = '';
  form.value.documentName = '';
}

async function save() {
  saving.value = true;
  try {
    const body = {
      title: form.value.title.trim(),
      body: form.value.body.trim() || null,
      kind: form.value.kind,
      status: form.value.status,
      documentId: form.value.documentId || null,
      wbsIds: form.value.wbsIds,
      workItemIds: form.value.workItemIds,
      resourceIds: form.value.resourceIds,
    };
    if (editingId.value) await pmUpdateDecision(editingId.value, body);
    else await pmCreateDecision(props.projectId, body);
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.decisionSaved'),
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
    await pmDeleteDecision(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.decisionDeleted'),
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
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.decision.hint') }}</div>
      <v-btn color="primary" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.decision.new') }}
      </v-btn>
    </div>

    <v-data-table
      :headers="headers"
      :items="decisions"
      :loading="loading"
      item-value="id"
      density="comfortable"
      class="rounded-lg border"
      hide-default-footer
      :items-per-page="-1"
    >
      <template #item.kind="{ item }">
        {{ kindLabel(item.kind) }}
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item.status)" variant="tonal">
          {{ statusLabel(item.status) }}
        </v-chip>
      </template>
      <template #item.impacts="{ item }">
        <div class="d-flex flex-wrap ga-1">
          <v-chip v-for="label in impactText(item)" :key="label" size="x-small" variant="tonal">
            {{ label }}
          </v-chip>
          <span v-if="!impactText(item).length" class="text-medium-emphasis">—</span>
        </div>
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
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.decision.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="640">
      <v-card rounded="lg">
        <v-card-title>
          {{ editingId ? t('projectManagement.decision.edit') : t('projectManagement.decision.new') }}
        </v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="form.title" :label="t('projectManagement.fields.name')" density="comfortable" />
          <v-textarea v-model="form.body" :label="t('projectManagement.decision.body')" density="comfortable" rows="3" auto-grow />
          <div class="d-flex ga-3">
            <v-select v-model="form.kind" :items="kindItems" :label="t('projectManagement.fields.kind')" density="comfortable" />
            <v-select v-model="form.status" :items="statusItems" :label="t('projectManagement.fields.status')" density="comfortable" />
          </div>
          <v-select
            v-model="form.wbsIds"
            :items="wbsItems"
            :label="t('projectManagement.decision.affectedWbs')"
            density="comfortable"
            multiple
            chips
            closable-chips
          />
          <v-select
            v-model="form.workItemIds"
            :items="workItemItems"
            :label="t('projectManagement.decision.affectedWorkItems')"
            density="comfortable"
            multiple
            chips
            closable-chips
            :disabled="!workItemItems.length"
          />
          <div>
            <div v-if="form.documentId" class="d-flex align-center ga-2 mb-2">
              <NuxtLink :to="resourceHref(form.documentId)" class="text-decoration-none" @click.stop>
                <v-chip size="small" color="primary" variant="tonal">
                  {{ form.documentName || form.documentId }}
                </v-chip>
              </NuxtLink>
              <v-btn size="small" variant="text" @click="clearOfficialDocument">{{ t('projectManagement.decision.clearDocument') }}</v-btn>
            </div>
            <v-text-field
              v-model="docQuery"
              :label="t('projectManagement.decision.searchDocument')"
              density="comfortable"
              :loading="docSearching"
              hide-details
              @keyup.enter="searchDocs"
            />
            <v-list v-if="docHits.length" class="rounded-lg border mt-2" density="compact">
              <v-list-item v-for="hit in docHits" :key="hit.id">
                <v-list-item-title>{{ hit.title || hit.name }}</v-list-item-title>
                <template #append>
                  <v-btn size="x-small" variant="text" @click="pickDocument(hit, true)">
                    {{ t('projectManagement.decision.asRecord') }}
                  </v-btn>
                  <v-btn size="x-small" variant="text" @click="pickDocument(hit, false)">
                    {{ t('projectManagement.decision.asImpact') }}
                  </v-btn>
                </template>
              </v-list-item>
            </v-list>
            <div v-if="form.resourceIds.length" class="d-flex flex-wrap ga-1 mt-2">
              <v-chip
                v-for="id in form.resourceIds"
                :key="id"
                size="small"
                variant="tonal"
                closable
                @click:close="form.resourceIds = form.resourceIds.filter((item) => item !== id)"
              >
                {{ id.slice(0, 8) }}
              </v-chip>
            </div>
          </div>
          <div class="text-caption text-medium-emphasis">{{ t('projectManagement.decision.scopeHint') }}</div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">{{ t('projectManagement.cancel') }}</v-btn>
          <v-btn color="primary" :loading="saving" :disabled="!form.title.trim()" @click="save">
            {{ t('projectManagement.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="Boolean(deleteTarget)" max-width="440" @update:model-value="onDeleteDialog">
      <v-card rounded="lg">
        <v-card-title>{{ t('projectManagement.decision.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.decision.deleteConfirm') }}</v-card-text>
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
