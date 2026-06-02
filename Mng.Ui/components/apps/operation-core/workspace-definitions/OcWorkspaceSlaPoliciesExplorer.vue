<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useOcWorkspaceCatalogInject } from '@/composables/useOcWorkspaceCatalog';
import OcWorkspaceSlaPolicyDialog from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceSlaPolicyDialog.vue';
import {
  ocCreateSlaPolicy,
  ocDeleteSlaPolicy,
  ocExtractDgErrorMessage,
  ocListSlaPoliciesForWorkspace,
  ocUpdateSlaPolicy,
} from '@/services/operationCoreService';
import type { OpPriority, OpSlaPolicy, OpWorkItemType } from '@/types/apps/operationCore';
import {
  formatSlaScopeSummary,
  formatSlaTargetsSummary,
  slaPolicySpecificityScore,
} from '@/utils/ocSlaPolicies';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();
const catalog = useOcWorkspaceCatalogInject();

const loading = ref(true);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);
const activeOnly = ref(false);

const policies = ref<OpSlaPolicy[]>([]);
const types = ref<OpWorkItemType[]>([]);
const priorities = ref<OpPriority[]>([]);

const dialog = ref(false);
const editingPolicy = ref<OpSlaPolicy | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<OpSlaPolicy | null>(null);

const typeItems = computed(() => types.value.map((ty) => ({ value: ty.__dataId, title: ty.name })));
const priorityItems = computed(() => priorities.value.map((p) => ({ value: p.__dataId, title: p.name })));
const typeNameById = computed(() => new Map(types.value.map((ty) => [ty.__dataId, ty.name])));
const priorityNameById = computed(() => new Map(priorities.value.map((p) => [p.__dataId, p.name])));

const filteredPolicies = computed(() => {
  let list = policies.value;
  if (activeOnly.value) list = list.filter((p) => p.isActive !== false);
  return list;
});

const activeCount = computed(() => policies.value.filter((p) => p.isActive !== false).length);

const headers = computed(() => [
  { title: t('operationCore.workspaceDefinitions.sla.colName'), key: 'name', sortable: true },
  { title: t('operationCore.workspaceDefinitions.sla.colScope'), key: 'scope', sortable: false },
  { title: t('operationCore.workspaceDefinitions.sla.colTargets'), key: 'targets', sortable: false },
  { title: t('operationCore.workspaceDefinitions.sla.colPolicyPriority'), key: 'priority', sortable: true, width: 96 },
  { title: t('operationCore.workspaceDefinitions.sla.colStatus'), key: 'isActive', sortable: true, width: 96 },
  { title: t('operationCore.workspaceDefinitions.sla.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

const howItWorksSteps = computed(() => [
  {
    icon: 'mdi-timer-outline',
    color: 'primary',
    title: t('operationCore.workspaceDefinitions.sla.howStep1Title'),
    body: t('operationCore.workspaceDefinitions.sla.howStep1Body'),
  },
  {
    icon: 'mdi-target',
    color: 'secondary',
    title: t('operationCore.workspaceDefinitions.sla.howStep2Title'),
    body: t('operationCore.workspaceDefinitions.sla.howStep2Body'),
  },
  {
    icon: 'mdi-flag-checkered',
    color: 'success',
    title: t('operationCore.workspaceDefinitions.sla.howStep3Title'),
    body: t('operationCore.workspaceDefinitions.sla.howStep3Body'),
  },
]);

function scopeSummary(policy: OpSlaPolicy): string {
  return formatSlaScopeSummary(
    policy,
    typeNameById.value,
    priorityNameById.value,
    t('operationCore.workspaceDefinitions.sla.scopeAny')
  );
}

function targetsSummary(policy: OpSlaPolicy): string {
  return formatSlaTargetsSummary(
    policy,
    t('operationCore.workspaceDefinitions.sla.responseTarget'),
    t('operationCore.workspaceDefinitions.sla.resolveTarget')
  );
}

function specificityLabel(policy: OpSlaPolicy): string {
  const score = slaPolicySpecificityScore(policy);
  if (score >= 3) return t('operationCore.workspaceDefinitions.sla.specificityFull');
  if (score === 2) return t('operationCore.workspaceDefinitions.sla.specificityType');
  if (score === 1) return t('operationCore.workspaceDefinitions.sla.specificityPriority');
  return t('operationCore.workspaceDefinitions.sla.specificityDefault');
}

async function loadAll() {
  if (!props.workspaceId) {
    policies.value = [];
    return;
  }
  loading.value = true;
  errorLocal.value = null;
  try {
    const [policyRows] = await Promise.all([
      ocListSlaPoliciesForWorkspace(props.workspaceId),
      catalog.whenReady(),
    ]);
    policies.value = policyRows;
    types.value = catalog.types.value;
    priorities.value = catalog.priorities.value;
  } catch (e) {
    errorLocal.value = ocExtractDgErrorMessage(e, t('operationCore.workspaceDefinitions.sla.loadError'));
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.workspaceId,
  () => {
    void loadAll();
  },
  { immediate: true }
);

function openCreate() {
  editingPolicy.value = null;
  dialog.value = true;
}

function openEdit(policy: OpSlaPolicy) {
  editingPolicy.value = policy;
  dialog.value = true;
}

function confirmDelete(policy: OpSlaPolicy) {
  deleteTarget.value = policy;
  deleteDialog.value = true;
}

async function savePolicy(payload: Record<string, unknown>) {
  saving.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    if (editingPolicy.value?.__dataId) {
      await ocUpdateSlaPolicy(editingPolicy.value.__dataId, payload);
    } else {
      await ocCreateSlaPolicy(payload);
    }
    dialog.value = false;
    successLocal.value = t('operationCore.workspaceDefinitions.sla.saveSuccess');
    await loadAll();
  } catch (e) {
    errorLocal.value = ocExtractDgErrorMessage(e, t('operationCore.workspaceDefinitions.sla.saveError'));
  } finally {
    saving.value = false;
  }
}

async function deletePolicy() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await ocDeleteSlaPolicy(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    successLocal.value = t('operationCore.workspaceDefinitions.sla.deleteSuccess');
    await loadAll();
  } catch (e) {
    errorLocal.value = ocExtractDgErrorMessage(e, t('operationCore.workspaceDefinitions.sla.deleteError'));
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="oc-ws-sla-tab pa-4 pa-md-6">
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>
    <v-alert v-if="successLocal" type="success" variant="tonal" class="mb-4" closable @click:close="successLocal = null">
      {{ successLocal }}
    </v-alert>

    <div class="mb-6">
      <h3 class="text-h6 font-weight-bold mb-1">{{ t('operationCore.workspaceDefinitions.sla.pageTitle') }}</h3>
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('operationCore.workspaceDefinitions.sla.pageSubtitle') }}
      </p>
    </div>

    <v-row class="mb-6">
      <v-col v-for="(step, idx) in howItWorksSteps" :key="idx" cols="12" md="4">
        <v-card variant="outlined" rounded="lg" class="h-100 pa-4">
          <v-avatar :color="step.color" variant="tonal" size="40" rounded="lg" class="mb-3">
            <v-icon :icon="step.icon" />
          </v-avatar>
          <div class="text-subtitle-2 font-weight-bold mb-1">{{ step.title }}</div>
          <p class="text-body-2 text-medium-emphasis mb-0">{{ step.body }}</p>
        </v-card>
      </v-col>
    </v-row>

    <div class="d-flex flex-wrap align-center ga-3 mb-4">
      <v-chip size="small" variant="outlined">
        {{ t('operationCore.workspaceDefinitions.sla.statsTotal', { count: policies.length }) }}
      </v-chip>
      <v-chip size="small" variant="outlined" color="success">
        {{ t('operationCore.workspaceDefinitions.sla.statsActive', { count: activeCount }) }}
      </v-chip>
      <v-spacer />
      <v-switch
        v-model="activeOnly"
        :label="t('operationCore.workspaceDefinitions.sla.filterActiveOnly')"
        density="compact"
        hide-details
        color="primary"
      />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">
        {{ t('operationCore.workspaceDefinitions.sla.addPolicy') }}
      </v-btn>
    </div>

    <v-card v-if="loading" variant="outlined" rounded="lg" class="pa-8 text-center">
      <v-progress-circular indeterminate color="primary" />
    </v-card>

    <v-card
      v-else-if="!policies.length"
      variant="outlined"
      rounded="lg"
      class="text-center pa-8 pa-md-12"
    >
      <v-icon icon="mdi-clock-check-outline" size="48" color="primary" class="mb-3" />
      <h4 class="text-h6 font-weight-bold mb-2">{{ t('operationCore.workspaceDefinitions.sla.emptyTitle') }}</h4>
      <p class="text-body-2 text-medium-emphasis mx-auto mb-4" style="max-width: 480px">
        {{ t('operationCore.workspaceDefinitions.sla.emptyBody') }}
      </p>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">
        {{ t('operationCore.workspaceDefinitions.sla.emptyCta') }}
      </v-btn>
    </v-card>

    <v-card v-else variant="outlined" rounded="lg">
      <v-data-table
        :headers="headers"
        :items="filteredPolicies"
        item-value="__dataId"
        density="comfortable"
        hide-default-footer
      >
        <template #[`item.name`]="{ item }">
          <div>
            <div class="font-weight-medium">{{ item.name }}</div>
            <div v-if="item.description" class="text-caption text-medium-emphasis text-truncate" style="max-width: 220px">
              {{ item.description }}
            </div>
          </div>
        </template>
        <template #[`item.scope`]="{ item }">
          <div>
            <span class="text-body-2">{{ scopeSummary(item) }}</span>
            <v-chip size="x-small" variant="tonal" class="ms-2">{{ specificityLabel(item) }}</v-chip>
          </div>
        </template>
        <template #[`item.targets`]="{ item }">
          <span class="text-body-2">{{ targetsSummary(item) }}</span>
        </template>
        <template #[`item.priority`]="{ item }">
          <span class="tabular-nums">{{ item.priority ?? 100 }}</span>
        </template>
        <template #[`item.isActive`]="{ item }">
          <v-chip size="small" :color="item.isActive !== false ? 'success' : 'default'" variant="tonal">
            {{
              item.isActive !== false
                ? t('operationCore.workspaceDefinitions.sla.activeYes')
                : t('operationCore.workspaceDefinitions.sla.activeNo')
            }}
          </v-chip>
        </template>
        <template #[`item.actions`]="{ item }">
          <div class="d-flex justify-end ga-1">
            <v-btn icon="mdi-pencil-outline" size="small" variant="text" @click="openEdit(item)" />
            <v-btn icon="mdi-delete-outline" size="small" variant="text" color="error" @click="confirmDelete(item)" />
          </div>
        </template>
      </v-data-table>
    </v-card>

    <p class="text-caption text-medium-emphasis mt-4 mb-0">
      {{ t('operationCore.workspaceDefinitions.sla.technicalFootnote') }}
    </p>

    <OcWorkspaceSlaPolicyDialog
      v-model="dialog"
      :policy="editingPolicy"
      :workspace-id="workspaceId"
      :type-items="typeItems"
      :priority-items="priorityItems"
      :saving="saving"
      @save="savePolicy"
    />

    <v-dialog v-model="deleteDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title>{{ t('operationCore.workspaceDefinitions.sla.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('operationCore.workspaceDefinitions.sla.deleteBody') }}</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ t('operationCore.workspaceDefinitions.sla.cancel') }}</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="deletePolicy">
            {{ t('operationCore.workspaceDefinitions.sla.deleteConfirm') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.tabular-nums {
  font-variant-numeric: tabular-nums;
}
</style>
