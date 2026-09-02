<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { pmDateInput } from '@/services/projectManagementService';
import type { PmProjectStatusPack, PmTraceFlag } from '@/types/apps/projectManagement';
import { diLifecycleChipColor } from '@/utils/diPageResource';

type StatusFilter = PmTraceFlag | 'all' | 'openScopeChange' | 'openGate' | 'failedGate';

const props = defineProps<{
  pack: PmProjectStatusPack | null;
  loading?: boolean;
}>();

const { t } = useAppI18n();
const activeFlag = ref<StatusFilter>('all');
const pendingAcks = computed(() => (props.pack?.acknowledgements?.items ?? []).filter((item) => item.pending));
const openObligations = computed(() => (props.pack?.obligations?.items ?? []).filter((item) => item.open));
const openAuditPacks = computed(() => (props.pack?.auditPacks?.items ?? []).filter((item) => item.open));
const openMeetingActions = computed(() => (props.pack?.meetingActions?.items ?? []).filter((item) => item.open));
const openStakeholders = computed(() => (props.pack?.stakeholders?.items ?? []).filter((item) => item.open));
const openProcessMaps = computed(() => (props.pack?.processMaps?.items ?? []).filter((item) => item.open));

const countCards = computed(() => {
  const c = props.pack?.counts;
  return [
    { flag: 'delayed' as const, value: c?.delayed ?? 0, color: 'error' },
    { flag: 'milestoneAtRisk' as const, value: c?.milestoneAtRisk ?? 0, color: 'warning' },
    { flag: 'drifted' as const, value: c?.drifted ?? 0, color: 'warning' },
    { flag: 'unbound' as const, value: c?.unboundLeaf ?? 0, color: 'default' },
    { flag: 'openWork' as const, value: c?.openWork ?? 0, color: 'info' },
    { flag: 'missingEvidence' as const, value: c?.missingEvidence ?? 0, color: 'info' },
    { flag: 'missingApproval' as const, value: c?.missingApproval ?? 0, color: 'warning' },
    { flag: 'openScopeChange' as const, value: c?.openScopeChange ?? 0, color: 'secondary' },
    { flag: 'openGate' as const, value: c?.openGate ?? 0, color: 'info' },
    { flag: 'failedGate' as const, value: c?.failedGate ?? 0, color: 'error' },
    { flag: 'openRisk' as const, value: c?.openRisk ?? 0, color: 'error' },
    { flag: 'openIssue' as const, value: c?.openIssue ?? 0, color: 'warning' },
    { flag: 'overloadedResource' as const, value: c?.overloadedResource ?? 0, color: 'error' },
    { flag: 'overBudget' as const, value: c?.overBudget ?? 0, color: 'error' },
    { flag: 'pendingAck' as const, value: c?.pendingAck ?? 0, color: 'warning' },
    { flag: 'overdueAck' as const, value: c?.overdueAck ?? 0, color: 'error' },
    { flag: 'openObligation' as const, value: c?.openObligation ?? 0, color: 'info' },
    { flag: 'overdueObligation' as const, value: c?.overdueObligation ?? 0, color: 'error' },
    { flag: 'unboundObligation' as const, value: c?.unboundObligation ?? 0, color: 'warning' },
    { flag: 'openAuditPack' as const, value: c?.openAuditPack ?? 0, color: 'info' },
    { flag: 'incompleteAuditPack' as const, value: c?.incompleteAuditPack ?? 0, color: 'warning' },
    { flag: 'overdueAuditPack' as const, value: c?.overdueAuditPack ?? 0, color: 'error' },
    { flag: 'openMeetingAction' as const, value: c?.openMeetingAction ?? 0, color: 'info' },
    { flag: 'overdueMeetingAction' as const, value: c?.overdueMeetingAction ?? 0, color: 'error' },
    { flag: 'unboundMeetingAction' as const, value: c?.unboundMeetingAction ?? 0, color: 'warning' },
    { flag: 'openStakeholder' as const, value: c?.openStakeholder ?? 0, color: 'info' },
    { flag: 'incompleteStakeholder' as const, value: c?.incompleteStakeholder ?? 0, color: 'warning' },
    { flag: 'overdueStakeholder' as const, value: c?.overdueStakeholder ?? 0, color: 'error' },
    { flag: 'openProcessMap' as const, value: c?.openProcessMap ?? 0, color: 'info' },
    { flag: 'incompleteProcessMap' as const, value: c?.incompleteProcessMap ?? 0, color: 'warning' },
  ];
});

const rows = computed(() => {
  const items = props.pack?.items ?? [];
  if (activeFlag.value === 'all') return items;
  if (activeFlag.value === 'openScopeChange') {
    return items.filter((row) =>
      (row.decisions || []).some((d) => d.kind === 'scopeChange' && d.status === 'open'),
    );
  }
  if (activeFlag.value === 'openGate' || activeFlag.value === 'failedGate') {
    const flag = activeFlag.value;
    return items.filter((row) => row.flags.includes(flag));
  }
  const flag = activeFlag.value;
  return items.filter((row) => row.flags.includes(flag));
});

const headers = computed(() => [
  { title: t('projectManagement.fields.wbsCode'), key: 'wbsCode', width: 90 },
  { title: t('projectManagement.fields.name'), key: 'wbsName', minWidth: 180 },
  { title: t('projectManagement.fields.workItem'), key: 'workItem', minWidth: 140 },
  { title: t('projectManagement.statusPack.documents'), key: 'documents', minWidth: 180 },
  { title: t('projectManagement.statusPack.decisions'), key: 'decisions', minWidth: 160 },
  { title: t('projectManagement.statusPack.flags'), key: 'flags', minWidth: 200 },
]);

function flagLabel(flag: string) {
  const key = `projectManagement.statusPack.flag.${flag}`;
  const label = t(key);
  return label === key ? flag : label;
}

function flagColor(flag: string) {
  if (flag === 'delayed' || flag === 'milestoneAtRisk' || flag === 'failedGate' || flag === 'openRisk' || flag === 'overloadedResource' || flag === 'overBudget' || flag === 'overdueAck' || flag === 'overdueObligation' || flag === 'overdueAuditPack' || flag === 'overdueMeetingAction' || flag === 'overdueStakeholder') return 'error';
  if (flag === 'drifted' || flag === 'missingApproval' || flag === 'openIssue' || flag === 'pendingAck' || flag === 'unboundObligation' || flag === 'incompleteAuditPack' || flag === 'unboundMeetingAction' || flag === 'incompleteStakeholder' || flag === 'incompleteProcessMap') return 'warning';
  if (flag === 'openWork' || flag === 'missingEvidence' || flag === 'openGate' || flag === 'openObligation' || flag === 'openAuditPack' || flag === 'openMeetingAction' || flag === 'openStakeholder' || flag === 'openProcessMap') return 'info';
  return 'default';
}

function gateStatusColor(status?: string | null) {
  if (status === 'passed') return 'success';
  if (status === 'failed') return 'error';
  if (status === 'waived') return 'warning';
  return 'info';
}

function gateStatusLabel(status?: string | null) {
  const key = `projectManagement.stageGate.status.${status || 'open'}`;
  const label = t(key);
  return label === key ? (status || 'open') : label;
}

function workItemHref(id: string) {
  return `/apps/operation-core/work-items/${encodeURIComponent(id)}/profile`;
}

function resourceHref(id: string) {
  return `/apps/document-intelligence/r/${encodeURIComponent(id)}`;
}

function budgetWbsName(wbsId: string) {
  const row = props.pack?.items.find((item) => item.wbsId === wbsId);
  return row?.wbsName || wbsId;
}

function toggleFlag(flag: Exclude<StatusFilter, 'all'>) {
  activeFlag.value = activeFlag.value === flag ? 'all' : flag;
}
</script>

<template>
  <div>
    <p class="text-body-2 text-medium-emphasis mb-4">{{ t('projectManagement.statusPack.hint') }}</p>

    <div v-if="pack?.gates?.length" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip
        v-for="gate in pack.gates"
        :key="gate.id"
        size="small"
        :color="gateStatusColor(gate.status)"
        variant="tonal"
      >
        {{ gate.name }} · {{ gateStatusLabel(gate.status) }}
      </v-chip>
    </div>

    <div v-if="pack?.raidItems?.length" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip
        v-for="item in pack.raidItems"
        :key="item.id"
        size="small"
        :color="item.elevated ? 'error' : item.open ? 'warning' : 'default'"
        variant="tonal"
      >
        {{ item.title }}
      </v-chip>
    </div>

    <div v-if="pack?.capacity?.people?.length" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip
        v-for="person in pack.capacity.people"
        :key="person.key"
        size="small"
        :color="person.overloaded ? 'error' : 'default'"
        variant="tonal"
      >
        {{ person.name }} · {{ person.totalHours }}h
      </v-chip>
    </div>

    <div v-if="pack?.budget?.packages?.length" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip
        v-for="item in pack.budget.packages"
        :key="item.wbsId"
        size="small"
        :color="item.over ? 'error' : 'default'"
        variant="tonal"
      >
        {{ budgetWbsName(item.wbsId) }} · {{ item.actualAmount }}/{{ item.plannedAmount }} {{ item.currency }}
      </v-chip>
    </div>

    <div v-if="pendingAcks.length" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip
        v-for="item in pendingAcks"
        :key="item.id"
        size="small"
        :color="item.overdue ? 'error' : 'warning'"
        variant="tonal"
      >
        {{ item.personName }} · {{ item.title }}
      </v-chip>
    </div>

    <div v-if="openObligations.length" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip
        v-for="item in openObligations"
        :key="item.id"
        size="small"
        :color="item.overdue ? 'error' : item.unbound ? 'warning' : 'info'"
        variant="tonal"
      >
        {{ item.clauseRef || item.title }}
      </v-chip>
    </div>

    <div v-if="openAuditPacks.length" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip
        v-for="item in openAuditPacks"
        :key="item.id"
        size="small"
        :color="item.overdue ? 'error' : item.incomplete ? 'warning' : 'info'"
        variant="tonal"
      >
        {{ item.name }}
      </v-chip>
    </div>

    <div v-if="openMeetingActions.length" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip
        v-for="item in openMeetingActions"
        :key="item.id"
        size="small"
        :color="item.overdue ? 'error' : item.unbound ? 'warning' : 'info'"
        variant="tonal"
      >
        {{ item.title }}
      </v-chip>
    </div>

    <div v-if="openStakeholders.length" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip
        v-for="item in openStakeholders"
        :key="item.id"
        size="small"
        :color="item.overdue ? 'error' : item.incomplete ? 'warning' : 'info'"
        variant="tonal"
      >
        {{ item.name }}
      </v-chip>
    </div>

    <div v-if="openProcessMaps.length" class="d-flex flex-wrap ga-2 mb-4">
      <v-chip
        v-for="item in openProcessMaps"
        :key="item.id"
        size="small"
        :color="item.incomplete ? 'warning' : 'info'"
        variant="tonal"
      >
        {{ item.name }}
      </v-chip>
    </div>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-chip
        v-for="card in countCards"
        :key="card.flag"
        :color="card.color"
        :variant="activeFlag === card.flag ? 'flat' : 'tonal'"
        :disabled="!card.value"
        @click="card.value ? toggleFlag(card.flag) : undefined"
      >
        {{ flagLabel(card.flag) }} · {{ card.value }}
      </v-chip>
      <v-chip v-if="activeFlag !== 'all'" variant="outlined" @click="activeFlag = 'all'">
        {{ t('projectManagement.statusPack.showAll') }}
      </v-chip>
    </div>

    <v-data-table
      :headers="headers"
      :items="rows"
      :loading="loading"
      item-value="wbsId"
      density="comfortable"
      class="rounded-lg border"
      hide-default-footer
      :items-per-page="-1"
    >
      <template #item.wbsCode="{ item }">
        {{ item.wbsCode || '—' }}
      </template>
      <template #item.wbsName="{ item }">
        <div>
          <div>{{ item.wbsName }}</div>
          <div class="text-caption text-medium-emphasis">
            {{ item.percentComplete ?? 0 }}%
            <span v-if="item.plannedFinish"> · {{ pmDateInput(item.plannedFinish) }}</span>
          </div>
        </div>
      </template>
      <template #item.workItem="{ item }">
        <NuxtLink
          v-if="item.workItemId && item.workItemKey"
          :to="workItemHref(item.workItemId)"
          class="text-decoration-none"
        >
          <v-chip size="small" :color="item.workItemClosed ? 'success' : 'info'" variant="tonal">
            {{ item.workItemKey }}
          </v-chip>
        </NuxtLink>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
      <template #item.decisions="{ item }">
        <div v-if="item.decisions?.length" class="d-flex flex-wrap ga-1">
          <v-chip
            v-for="decision in item.decisions"
            :key="decision.id"
            size="x-small"
            :color="decision.kind === 'scopeChange' ? 'secondary' : 'default'"
            variant="tonal"
            :title="decision.title"
          >
            {{ decision.title }}
          </v-chip>
        </div>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
      <template #item.documents="{ item }">
        <div v-if="item.documents?.length" class="d-flex flex-wrap ga-1">
          <NuxtLink
            v-for="doc in item.documents"
            :key="doc.resourceId"
            :to="resourceHref(doc.resourceId)"
            class="text-decoration-none"
            :title="`${doc.name} · ${doc.relationType}`"
          >
            <v-chip size="small" :color="diLifecycleChipColor(doc.status)" variant="tonal">
              {{ doc.name }}
            </v-chip>
          </NuxtLink>
        </div>
        <span v-else class="text-medium-emphasis">—</span>
      </template>
      <template #item.flags="{ item }">
        <div class="d-flex flex-wrap ga-1">
          <v-chip v-for="flag in item.flags" :key="flag" size="x-small" :color="flagColor(flag)" variant="tonal">
            {{ flagLabel(flag) }}
          </v-chip>
          <span v-if="!item.flags?.length" class="text-medium-emphasis">—</span>
        </div>
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">{{ t('projectManagement.statusPack.empty') }}</div>
      </template>
    </v-data-table>
    <div v-if="pack?.generatedAt" class="text-caption text-medium-emphasis mt-2">
      {{ t('projectManagement.statusPack.generatedAt') }}: {{ pack.generatedAt }}
    </div>
  </div>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
