<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  pmBindWbsEvidence,
  pmBindWbsReference,
  pmCreateDecision,
  pmListProjectDecisions,
  pmUpdateDecision,
} from '@/services/projectManagementService';
import type { DiResource } from '@/types/apps/documentIntelligence';
import type { PmDecision, PmWbsItem } from '@/types/apps/projectManagement';
import { diPageResourceLabel } from '@/utils/diPageResource';

export type PmLibraryBindKind = 'evidence' | 'reference' | 'decision';

const props = defineProps<{
  modelValue: boolean;
  projectId: string;
  resource: DiResource | null;
  wbs: PmWbsItem[];
  initialKind?: PmLibraryBindKind;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  bound: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const toast = useAppToast();

const kind = ref<PmLibraryBindKind>('evidence');
const query = ref('');
const selectedWbsId = ref<string | null>(null);
const selectedDecisionId = ref<string | null>(null);
const createDecision = ref(false);
const newDecisionTitle = ref('');
const decisions = ref<PmDecision[]>([]);
const decisionsLoading = ref(false);
const saving = ref(false);

const open = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value),
});

const resourceLabel = computed(() => (props.resource ? diPageResourceLabel(props.resource) : ''));

const kindItems = computed(() => [
  { value: 'evidence' as const, title: t('projectManagement.library.bind.evidence'), hint: t('projectManagement.library.bind.evidenceHint') },
  { value: 'reference' as const, title: t('projectManagement.library.bind.reference'), hint: t('projectManagement.library.bind.referenceHint') },
  { value: 'decision' as const, title: t('projectManagement.library.bind.decision'), hint: t('projectManagement.library.bind.decisionHint') },
]);

const kindHint = computed(() => kindItems.value.find((item) => item.value === kind.value)?.hint || '');

const filteredWbs = computed(() => {
  const q = query.value.trim().toLocaleLowerCase('tr');
  return props.wbs.filter((row) => {
    if (!q) return true;
    const hay = `${row.wbsCode || ''} ${row.name} ${row.workItemKey || ''}`.toLocaleLowerCase('tr');
    return hay.includes(q);
  });
});

const filteredDecisions = computed(() => {
  const q = query.value.trim().toLocaleLowerCase('tr');
  return decisions.value.filter((row) => {
    if (!q) return true;
    return `${row.title} ${row.status}`.toLocaleLowerCase('tr').includes(q);
  });
});

const canSubmit = computed(() => {
  if (!props.resource?.id) return false;
  if (kind.value === 'decision') {
    if (createDecision.value) return newDecisionTitle.value.trim().length > 0;
    return Boolean(selectedDecisionId.value);
  }
  const row = props.wbs.find((item) => item.id === selectedWbsId.value);
  return Boolean(row?.workItemId);
});

function wbsDisabled(row: PmWbsItem) {
  return !row.workItemId;
}

function resetForm() {
  kind.value = props.initialKind || 'evidence';
  query.value = '';
  selectedWbsId.value = null;
  selectedDecisionId.value = null;
  createDecision.value = false;
  newDecisionTitle.value = props.resource ? diPageResourceLabel(props.resource) : '';
}

async function loadDecisions() {
  if (!props.projectId) return;
  decisionsLoading.value = true;
  try {
    decisions.value = await pmListProjectDecisions(props.projectId);
  } catch (error) {
    decisions.value = [];
    panelError(error, 'projectManagement.errors.loadFailed');
  } finally {
    decisionsLoading.value = false;
  }
}

watch(
  () => props.modelValue,
  (isOpen) => {
    if (!isOpen) return;
    resetForm();
    if (kind.value === 'decision') void loadDecisions();
  },
);

watch(kind, (value) => {
  selectedWbsId.value = null;
  selectedDecisionId.value = null;
  createDecision.value = false;
  if (value === 'decision' && props.modelValue) void loadDecisions();
});

async function submit() {
  if (!props.resource?.id || !canSubmit.value) return;
  saving.value = true;
  try {
    if (kind.value === 'evidence' && selectedWbsId.value) {
      await pmBindWbsEvidence(selectedWbsId.value, props.resource.id);
      toast.push({
        title: t('projectManagement.notify.successTitle'),
        message: t('projectManagement.notify.evidenceBound'),
        severity: 'success',
      });
    } else if (kind.value === 'reference' && selectedWbsId.value) {
      await pmBindWbsReference(selectedWbsId.value, props.resource.id);
      toast.push({
        title: t('projectManagement.notify.successTitle'),
        message: t('projectManagement.notify.referenceBound'),
        severity: 'success',
      });
    } else if (kind.value === 'decision') {
      if (createDecision.value) {
        await pmCreateDecision(props.projectId, {
          title: newDecisionTitle.value.trim(),
          documentId: props.resource.id,
          kind: 'general',
          status: 'open',
        });
      } else if (selectedDecisionId.value) {
        await pmUpdateDecision(selectedDecisionId.value, { documentId: props.resource.id });
      }
      toast.push({
        title: t('projectManagement.notify.successTitle'),
        message: t('projectManagement.notify.decisionSaved'),
        severity: 'success',
      });
    }
    open.value = false;
    emit('bound');
  } catch (error) {
    panelError(error, 'projectManagement.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <v-dialog v-model="open" max-width="560">
    <v-card rounded="lg">
      <v-card-title class="text-subtitle-1 font-weight-bold">
        {{ t('projectManagement.library.bind.title') }}
      </v-card-title>
      <v-card-text class="d-flex flex-column ga-4">
        <div class="text-body-2">
          <span class="text-medium-emphasis">{{ t('projectManagement.library.bind.document') }}:</span>
          <strong class="ml-1">{{ resourceLabel }}</strong>
        </div>

        <v-btn-toggle v-model="kind" mandatory density="compact" color="primary" divided>
          <v-btn v-for="item in kindItems" :key="item.value" :value="item.value" class="text-none" size="small">
            {{ item.title }}
          </v-btn>
        </v-btn-toggle>
        <p class="text-caption text-medium-emphasis mb-0">{{ kindHint }}</p>

        <v-text-field
          v-model="query"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          :placeholder="kind === 'decision' ? t('projectManagement.library.bind.searchDecision') : t('projectManagement.library.bind.searchWbs')"
        />

        <template v-if="kind !== 'decision'">
          <v-list v-if="filteredWbs.length" density="compact" class="border rounded-lg pa-0" max-height="280" style="overflow: auto">
            <v-list-item
              v-for="row in filteredWbs"
              :key="row.id"
              :active="selectedWbsId === row.id"
              :disabled="wbsDisabled(row)"
              :title="`${row.wbsCode || '—'} · ${row.name}`"
              :subtitle="wbsDisabled(row) ? t('projectManagement.library.bind.needsWorkItem') : (row.workItemKey || t('projectManagement.library.bind.workBound'))"
              @click="selectedWbsId = row.id"
            >
              <template #append>
                <v-chip v-if="row.hasReference" size="x-small" variant="tonal" class="mr-1">{{ t('projectManagement.hasReference') }}</v-chip>
                <v-chip v-if="row.hasEvidence" size="x-small" variant="tonal">{{ t('projectManagement.hasEvidence') }}</v-chip>
              </template>
            </v-list-item>
          </v-list>
          <div v-else class="text-body-2 text-medium-emphasis py-4 text-center">
            {{ t('projectManagement.library.bind.emptyWbs') }}
          </div>
        </template>

        <template v-else>
          <v-checkbox
            v-model="createDecision"
            :label="t('projectManagement.library.bind.newDecision')"
            density="compact"
            hide-details
          />
          <v-text-field
            v-if="createDecision"
            v-model="newDecisionTitle"
            :label="t('projectManagement.library.bind.decisionTitle')"
            variant="outlined"
            density="comfortable"
            hide-details
          />
          <v-list
            v-else-if="filteredDecisions.length"
            density="compact"
            class="border rounded-lg pa-0"
            max-height="280"
            style="overflow: auto"
          >
            <v-list-item
              v-for="row in filteredDecisions"
              :key="row.id"
              :active="selectedDecisionId === row.id"
              :title="row.title"
              :subtitle="row.documentId ? t('projectManagement.library.bind.decisionHasDoc') : t('projectManagement.library.bind.decisionNoDoc')"
              @click="selectedDecisionId = row.id"
            />
          </v-list>
          <div v-else-if="!decisionsLoading" class="text-body-2 text-medium-emphasis py-4 text-center">
            {{ t('projectManagement.library.bind.emptyDecision') }}
          </div>
          <v-progress-linear v-if="decisionsLoading" indeterminate color="primary" />
        </template>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="open = false">{{ t('projectManagement.cancel') }}</v-btn>
        <v-btn color="primary" variant="flat" class="text-none" :loading="saving" :disabled="!canSubmit" @click="submit">
          {{ t('projectManagement.library.bind.submit') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
