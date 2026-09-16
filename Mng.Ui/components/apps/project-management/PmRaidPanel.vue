<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmCreateRaidItem,
  pmDateInput,
  pmDatePayload,
  pmDeleteRaidItem,
  pmUpdateRaidItem,
} from '@/services/projectManagementService';
import type {
  PmRaidItem,
  PmRaidKind,
  PmRaidLevel,
  PmRaidResponse,
  PmRaidStatus,
  PmWbsItem,
} from '@/types/apps/projectManagement';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  projectId: string;
  items: PmRaidItem[];
  wbs: PmWbsItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  changed: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const kindFilter = ref<'all' | PmRaidKind>('all');
const dialog = ref(false);
const saving = ref(false);
const editingId = ref<string | null>(null);
const deleteTarget = ref<PmRaidItem | null>(null);
const deleting = ref(false);

const form = ref({
  kind: 'risk' as PmRaidKind,
  title: '',
  body: '',
  status: 'open' as PmRaidStatus,
  impact: 'medium' as PmRaidLevel,
  likelihood: 'medium' as PmRaidLevel,
  response: 'none' as PmRaidResponse,
  owner: '',
  dueDate: '',
  wbsIds: [] as string[],
});

const KIND_META: Record<PmRaidKind, { icon: string; color: string }> = {
  risk: { icon: 'mdi-alert-outline', color: 'warning' },
  assumption: { icon: 'mdi-lightbulb-outline', color: 'info' },
  issue: { icon: 'mdi-alert-octagon-outline', color: 'error' },
  dependency: { icon: 'mdi-link-variant', color: 'primary' },
};

const RAID_LEVEL_SCORE: Record<PmRaidLevel, number> = { low: 1, medium: 2, high: 3 };
const CLOSED_STATUSES = new Set<PmRaidStatus>(['closed', 'validated', 'invalid', 'resolved']);

const kindChoices = computed(() =>
  (['risk', 'assumption', 'issue', 'dependency'] as PmRaidKind[]).map((value) => ({
    value,
    title: t(`projectManagement.raid.kind.${value}`),
    hint: t(`projectManagement.raid.dialog.kindHint.${value}`),
    icon: KIND_META[value].icon,
    color: KIND_META[value].color,
  })),
);

const kindMeta = computed(() => KIND_META[form.value.kind] || KIND_META.risk);

const formScore = computed(
  () => RAID_LEVEL_SCORE[form.value.likelihood] * RAID_LEVEL_SCORE[form.value.impact],
);

const formElevated = computed(
  () => form.value.kind === 'risk' && (form.value.impact === 'high' || formScore.value >= 6),
);

const statusHint = computed(() => {
  if (CLOSED_STATUSES.has(form.value.status)) return t('projectManagement.raid.dialog.closedNoCount');
  return t(`projectManagement.raid.dialog.kindHint.${form.value.kind}`);
});

const levelItems = computed(() => [
  { title: t('projectManagement.raid.level.low'), value: 'low' },
  { title: t('projectManagement.raid.level.medium'), value: 'medium' },
  { title: t('projectManagement.raid.level.high'), value: 'high' },
]);

const responseItems = computed(() => [
  { title: t('projectManagement.raid.response.none'), value: 'none' },
  { title: t('projectManagement.raid.response.avoid'), value: 'avoid' },
  { title: t('projectManagement.raid.response.mitigate'), value: 'mitigate' },
  { title: t('projectManagement.raid.response.transfer'), value: 'transfer' },
  { title: t('projectManagement.raid.response.accept'), value: 'accept' },
]);

const statusItems = computed(() => {
  const prefix = 'projectManagement.raid.status';
  if (form.value.kind === 'risk') {
    return [
      { title: t(`${prefix}.open`), value: 'open' },
      { title: t(`${prefix}.mitigating`), value: 'mitigating' },
      { title: t(`${prefix}.closed`), value: 'closed' },
    ];
  }
  if (form.value.kind === 'assumption') {
    return [
      { title: t(`${prefix}.open`), value: 'open' },
      { title: t(`${prefix}.validated`), value: 'validated' },
      { title: t(`${prefix}.invalid`), value: 'invalid' },
    ];
  }
  if (form.value.kind === 'issue') {
    return [
      { title: t(`${prefix}.open`), value: 'open' },
      { title: t(`${prefix}.inProgress`), value: 'inProgress' },
      { title: t(`${prefix}.closed`), value: 'closed' },
    ];
  }
  return [
    { title: t(`${prefix}.open`), value: 'open' },
    { title: t(`${prefix}.waiting`), value: 'waiting' },
    { title: t(`${prefix}.resolved`), value: 'resolved' },
  ];
});

const wbsItems = computed(() =>
  props.wbs.map((row) => ({
    title: `${row.wbsCode || '—'} ${row.name}`,
    value: row.id,
  })),
);

const filtered = computed(() => {
  if (kindFilter.value === 'all') return props.items;
  return props.items.filter((row) => row.kind === kindFilter.value);
});

const headers = computed(() => [
  { title: t('projectManagement.fields.kind'), key: 'kind', width: 160 },
  { title: t('projectManagement.fields.name'), key: 'title', minWidth: 200 },
  { title: t('projectManagement.fields.status'), key: 'status', width: 130 },
  { title: t('projectManagement.raid.impact'), key: 'impact', width: 110 },
  { title: t('projectManagement.raid.owner'), key: 'owner', width: 120 },
  { title: t('projectManagement.actions'), key: 'actions', width: 120, sortable: false, align: 'end' as const },
]);

function kindVisual(kind?: string | null) {
  const key = (kind || 'risk') as PmRaidKind;
  return KIND_META[key] || KIND_META.risk;
}

function kindLabel(kind?: string | null) {
  const key = `projectManagement.raid.kind.${kind || 'risk'}`;
  const label = t(key);
  return label === key ? (kind || 'risk') : label;
}

function statusLabel(status?: string | null) {
  const key = `projectManagement.raid.status.${status || 'open'}`;
  const label = t(key);
  return label === key ? (status || 'open') : label;
}

function levelLabel(level?: string | null) {
  const key = `projectManagement.raid.level.${level || 'medium'}`;
  const label = t(key);
  return label === key ? (level || 'medium') : label;
}

function statusColor(status?: string | null) {
  if (status === 'closed' || status === 'validated' || status === 'resolved') return 'success';
  if (status === 'invalid') return 'error';
  if (status === 'mitigating' || status === 'waiting' || status === 'inProgress') return 'warning';
  return 'info';
}

function impactColor(row: PmRaidItem) {
  if (row.kind === 'risk' && row.elevated) return 'error';
  if (row.impact === 'high') return 'warning';
  return 'default';
}

function selectKind(kind: PmRaidKind) {
  if (form.value.kind === kind) return;
  form.value.kind = kind;
  form.value.status = 'open';
}

function openCreate() {
  editingId.value = null;
  form.value = {
    kind: kindFilter.value === 'all' ? 'risk' : kindFilter.value,
    title: '',
    body: '',
    status: 'open',
    impact: 'medium',
    likelihood: 'medium',
    response: 'none',
    owner: '',
    dueDate: '',
    wbsIds: [],
  };
  dialog.value = true;
}

function openEdit(row: PmRaidItem) {
  editingId.value = row.id;
  form.value = {
    kind: (row.kind as PmRaidKind) || 'risk',
    title: row.title,
    body: row.body || '',
    status: (row.status as PmRaidStatus) || 'open',
    impact: (row.impact as PmRaidLevel) || 'medium',
    likelihood: (row.likelihood as PmRaidLevel) || 'medium',
    response: (row.response as PmRaidResponse) || 'none',
    owner: row.owner || '',
    dueDate: pmDateInput(row.dueDate),
    wbsIds: [...(row.wbsIds || [])],
  };
  dialog.value = true;
}

async function save() {
  if (!form.value.title.trim()) return;
  saving.value = true;
  try {
    const body = {
      kind: form.value.kind,
      title: form.value.title.trim(),
      body: form.value.body.trim() || null,
      status: form.value.status,
      impact: form.value.impact,
      likelihood: form.value.likelihood,
      response: form.value.response,
      owner: form.value.owner.trim() || null,
      dueDate: pmDatePayload(form.value.dueDate),
      wbsIds: form.value.wbsIds,
    };
    if (editingId.value) await pmUpdateRaidItem(editingId.value, body);
    else await pmCreateRaidItem(props.projectId, body);
    dialog.value = false;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.raidSaved'),
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
    await pmDeleteRaidItem(deleteTarget.value.id);
    deleteTarget.value = null;
    toast.push({
      title: t('projectManagement.notify.successTitle'),
      message: t('projectManagement.notify.raidDeleted'),
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
      <div class="text-body-2 text-medium-emphasis">{{ t('projectManagement.raid.hint') }}</div>
      <v-btn color="primary" @click="openCreate">
        <PlusIcon size="18" class="mr-1" />
        {{ t('projectManagement.raid.new') }}
      </v-btn>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip :variant="kindFilter === 'all' ? 'flat' : 'tonal'" @click="kindFilter = 'all'">
        {{ t('projectManagement.raid.filterAll') }}
      </v-chip>
      <v-chip
        v-for="item in kindChoices"
        :key="item.value"
        :variant="kindFilter === item.value ? 'flat' : 'tonal'"
        :color="kindFilter === item.value ? item.color : undefined"
        @click="kindFilter = item.value"
      >
        <v-icon start :icon="item.icon" size="16" />
        {{ item.title }}
      </v-chip>
    </div>

    <v-data-table
      :headers="headers"
      :items="filtered"
      :loading="loading"
      item-value="id"
      density="comfortable"
      class="rounded-lg border"
      hide-default-footer
      :items-per-page="-1"
    >
      <template #item.kind="{ item }">
        <v-chip size="small" :color="kindVisual(item.kind).color" variant="tonal">
          <v-icon start :icon="kindVisual(item.kind).icon" size="16" />
          {{ kindLabel(item.kind) }}
        </v-chip>
      </template>
      <template #item.title="{ item }">
        <div>{{ item.title }}</div>
        <div v-if="item.kind === 'risk' && item.score" class="text-caption text-medium-emphasis">
          {{ t('projectManagement.raid.score') }} {{ item.score }}
        </div>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item.status)" variant="tonal">
          {{ statusLabel(item.status) }}
        </v-chip>
      </template>
      <template #item.impact="{ item }">
        <v-chip size="small" :color="impactColor(item)" variant="tonal">
          {{ levelLabel(item.impact) }}
        </v-chip>
      </template>
      <template #item.owner="{ item }">
        {{ item.owner || '—' }}
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
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.raid.empty') }}</div>
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="720" scrollable>
      <v-card rounded="lg">
        <v-card-title class="d-flex align-start ga-3 px-6 py-4">
          <v-avatar :color="kindMeta.color" variant="tonal" rounded="lg">
            <v-icon :icon="editingId ? 'mdi-pencil-outline' : kindMeta.icon" />
          </v-avatar>
          <div class="flex-grow-1">
            <div>{{ editingId ? t('projectManagement.raid.edit') : t('projectManagement.raid.new') }}</div>
            <div class="text-body-2 text-medium-emphasis font-weight-regular mt-1">
              {{ editingId ? t('projectManagement.raid.dialog.subtitleEdit') : t('projectManagement.raid.dialog.subtitleNew') }}
            </div>
          </div>
        </v-card-title>
        <v-divider />
        <v-card-text class="d-flex flex-column ga-5 px-6 py-5">
          <section>
            <div class="text-subtitle-2 mb-2">{{ t('projectManagement.raid.dialog.sectionKind') }}</div>
            <div class="pm-raid-kind-grid">
              <v-card
                v-for="choice in kindChoices"
                :key="choice.value"
                :variant="form.kind === choice.value ? 'tonal' : 'outlined'"
                :color="form.kind === choice.value ? choice.color : undefined"
                rounded="lg"
                class="pm-raid-kind-card pa-3"
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
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.raid.dialog.sectionIdentity') }}</div>
            <v-text-field
              v-model="form.title"
              :label="t('projectManagement.fields.name')"
              :placeholder="t(`projectManagement.raid.dialog.titleHint.${form.kind}`)"
              density="comfortable"
              hide-details="auto"
              class="mb-3"
              prepend-inner-icon="mdi-format-title"
            />
            <v-textarea
              v-model="form.body"
              :label="t('projectManagement.raid.body')"
              density="comfortable"
              rows="3"
              hide-details="auto"
              auto-grow
            />
          </section>

          <section v-if="form.kind === 'risk'">
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.raid.dialog.sectionAssess') }}</div>
            <p class="text-caption text-medium-emphasis mb-3">{{ t('projectManagement.raid.dialog.sectionAssessHint') }}</p>
            <div class="d-flex ga-3 flex-wrap">
              <v-select
                v-model="form.likelihood"
                :items="levelItems"
                :label="t('projectManagement.raid.likelihood')"
                density="comfortable"
                hide-details
                class="flex-grow-1"
              />
              <v-select
                v-model="form.impact"
                :items="levelItems"
                :label="t('projectManagement.raid.impact')"
                density="comfortable"
                hide-details
                class="flex-grow-1"
              />
            </div>
            <v-alert
              class="mt-3"
              :color="formElevated ? 'warning' : 'primary'"
              variant="tonal"
              density="compact"
              :icon="formElevated ? 'mdi-alert' : 'mdi-information-outline'"
            >
              <div class="d-flex align-center ga-2 flex-wrap">
                <span class="font-weight-medium">{{ t('projectManagement.raid.dialog.score', { score: formScore }) }}</span>
                <v-chip size="x-small" :color="formElevated ? 'warning' : 'default'" variant="flat">
                  {{ formElevated ? t('projectManagement.raid.dialog.elevated') : t('projectManagement.raid.dialog.notElevated') }}
                </v-chip>
              </div>
            </v-alert>
            <v-select
              v-model="form.response"
              :items="responseItems"
              :label="t('projectManagement.raid.responseLabel')"
              :hint="t('projectManagement.raid.dialog.responseHint')"
              persistent-hint
              density="comfortable"
              class="mt-3"
            />
          </section>

          <section v-else>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.raid.impact') }}</div>
            <v-select
              v-model="form.impact"
              :items="levelItems"
              :label="t('projectManagement.raid.impact')"
              density="comfortable"
              hide-details="auto"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.fields.status') }}</div>
            <v-select
              v-model="form.status"
              :items="statusItems"
              :label="t('projectManagement.fields.status')"
              :hint="statusHint"
              persistent-hint
              density="comfortable"
            />
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.raid.dialog.sectionOwner') }}</div>
            <div class="d-flex ga-3">
              <v-text-field
                v-model="form.owner"
                :label="t('projectManagement.raid.owner')"
                density="comfortable"
                hide-details
                prepend-inner-icon="mdi-account-outline"
              />
              <v-text-field
                v-model="form.dueDate"
                type="date"
                :label="t('projectManagement.raid.dueDate')"
                density="comfortable"
                hide-details
                prepend-inner-icon="mdi-calendar"
              />
            </div>
          </section>

          <section>
            <div class="text-subtitle-2 mb-1">{{ t('projectManagement.raid.dialog.sectionWbs') }}</div>
            <v-select
              v-model="form.wbsIds"
              :items="wbsItems"
              :label="t('projectManagement.raid.affectedWbs')"
              :hint="t('projectManagement.raid.dialog.wbsHint')"
              persistent-hint
              density="comfortable"
              multiple
              chips
              closable-chips
            />
          </section>
        </v-card-text>
        <v-divider />
        <v-card-actions class="px-6 py-3">
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
        <v-card-title>{{ t('projectManagement.raid.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('projectManagement.raid.deleteConfirm') }}</v-card-text>
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

.pm-raid-kind-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.pm-raid-kind-card {
  cursor: pointer;
  height: 100%;
}

.pm-raid-kind-card:hover {
  border-color: rgba(var(--v-theme-primary), 0.4);
}

@media (max-width: 600px) {
  .pm-raid-kind-grid {
    grid-template-columns: 1fr;
  }
}
</style>
