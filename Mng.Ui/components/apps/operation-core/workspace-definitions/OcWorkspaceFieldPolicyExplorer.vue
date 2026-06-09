<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useOcWorkspaceMetadataCacheReload } from '@/composables/useOcWorkspaceMetadataCacheReload';
import { useOcPersonPicker } from '@/composables/useOcPersonPicker';
import { useOcWorkspaceCatalogInject } from '@/composables/useOcWorkspaceCatalog';
import { useUserStore } from '@/stores/apps/user';
import OcWorkspacePolicyDialog from '@/components/apps/operation-core/workspace-definitions/OcWorkspacePolicyDialog.vue';
import {
  OC_DATASETS,
  ocExtractDgErrorMessage,
  ocGetDatasetRecordTitle,
  ocListGlobalWorkItemTypes,
  ocListPriorities,
  ocListStates,
  ocListWorkspaceScopedWorkItemTypes,
  ocUpdateWorkspace,
} from '@/services/operationCoreService';
import {
  OC_CORE_WORK_ITEM_FIELDS,
  OC_POLICY_CONDITION_ALWAYS_CORE_KEYS,
} from '@/utils/ocFieldDefinitions';
import { resolveOcFieldDisplayLabel } from '@/utils/ocFormFieldLabels';
import {
  buildPolicyConditionFieldOptions,
  collectPersonIdsFromWorkspacePolicies,
  collectPolicyReferencedCatalogIds,
  formatWorkspaceFieldPolicySummary,
  mergeCatalogTitleMaps,
  mergeFieldPoliciesIntoSettings,
  parseWorkspaceFieldPoliciesFromSettings,
  policiesForField,
  setPoliciesForField,
  workspacePolicyKindLabel,
  type OcPolicyValueResolveContext,
  type OcWorkspaceFieldPoliciesBlob,
  type OcWorkspaceFieldPolicy,
  type OcWorkspaceFieldPolicyKind,
} from '@/utils/ocWorkspaceFieldPolicies';
import { buildOcPersonPickerTitle } from '@/utils/ocPersonPicker';
import type { OpField, OpState } from '@/types/apps/operationCore';

const props = defineProps<{
  workspaceId: string;
  catalogFieldKeys: string[];
  poolFields: OpField[];
}>();

const { t } = useAppI18n();
const metaCache = useOcWorkspaceMetadataCacheReload(() => props.workspaceId);
const catalog = useOcWorkspaceCatalogInject();
const userStore = useUserStore();
const personPicker = useOcPersonPicker();
const personTitleById = ref<Map<string, string>>(new Map());
/** Politika özetleri — picker listesinden bağımsız tam katalog + eksik id fetch */
const stateTitleById = ref<Map<string, string>>(new Map());
const priorityTitleById = ref<Map<string, string>>(new Map());
const typeTitleById = ref<Map<string, string>>(new Map());
const boardTitleById = ref<Map<string, string>>(new Map());

const loading = ref(false);
const saving = ref(false);
const errorLocal = ref<string | null>(null);
const successLocal = ref<string | null>(null);

const policiesBlob = ref<OcWorkspaceFieldPoliciesBlob>({ policiesByField: {} });
const settingsBase = ref<Record<string, unknown>>({});

const selectedFieldKey = ref<string | null>(null);
const states = ref<OpState[]>([]);
const priorityItems = ref<{ value: string; title: string }[]>([]);
const typeItems = ref<{ value: string; title: string }[]>([]);
const boardItems = ref<{ value: string; title: string }[]>([]);

const policyDialog = ref(false);
const dialogKind = ref<OcWorkspaceFieldPolicyKind>('visibility');
const editingPolicy = ref<OcWorkspaceFieldPolicy | null>(null);
const deleteDialog = ref(false);
const deletePolicyTarget = ref<OcWorkspaceFieldPolicy | null>(null);

const fieldCatalog = computed(() =>
  props.catalogFieldKeys.map((key) => {
    const pool = props.poolFields.find((f) => f.key === key);
    const core = OC_CORE_WORK_ITEM_FIELDS.find((c) => c.key === key);
    const label =
      resolveOcFieldDisplayLabel(key, {
        poolLabel: pool?.label?.trim() || null,
        translate: t,
      }) || key;
    return {
      key,
      label,
      fieldType: pool?.fieldType ?? core?.fieldType,
      relationDataset: pool?.relationDatasetName ?? null,
      cardinality: pool?.cardinality,
      policyCount: policiesForField(policiesBlob.value, key).length,
    };
  })
);

const selectedField = computed(() =>
  fieldCatalog.value.find((f) => f.key === selectedFieldKey.value) ?? null
);

const selectedPolicies = computed(() => {
  if (!selectedFieldKey.value) return [];
  return policiesForField(policiesBlob.value, selectedFieldKey.value);
});

const stateItems = computed(() =>
  states.value.map((s) => ({
    value: s.__dataId,
    title: s.name?.trim() || s.__dataId,
  }))
);

function resolveConditionFieldOption(key: string) {
  const fromCatalog = fieldCatalog.value.find((f) => f.key === key);
  if (fromCatalog) {
    return {
      key,
      label: fromCatalog.label,
      fieldType: fromCatalog.fieldType,
      relationDataset: fromCatalog.relationDataset ?? null,
      cardinality: fromCatalog.cardinality,
    };
  }
  const core = OC_CORE_WORK_ITEM_FIELDS.find((c) => c.key === key);
  return {
    key,
    label: resolveOcFieldDisplayLabel(key, { translate: t }),
    fieldType: core?.fieldType,
    relationDataset: null as string | null,
    cardinality: undefined as string | undefined,
  };
}

const conditionFieldOptions = computed(() => {
  const keys = new Set([
    ...fieldCatalog.value.map((f) => f.key),
    ...OC_POLICY_CONDITION_ALWAYS_CORE_KEYS,
  ]);
  return buildPolicyConditionFieldOptions(
    [...keys].map((key) => resolveConditionFieldOption(key))
  );
});

const fieldMetaByKey = computed(() => {
  const map = new Map<string, { fieldType?: string; cardinality?: string }>();
  for (const f of conditionFieldOptions.value) {
    map.set(f.key, { fieldType: f.fieldType, cardinality: f.cardinality });
  }
  for (const f of fieldCatalog.value) {
    if (!map.has(f.key)) {
      map.set(f.key, { fieldType: f.fieldType, cardinality: f.cardinality });
    }
  }
  return map;
});

const valueResolveContext = computed((): OcPolicyValueResolveContext => ({
  fieldLabelByKey: new Map(conditionFieldOptions.value.map((f) => [f.key, f.label])),
  fieldMetaByKey: fieldMetaByKey.value,
  stateTitleById: mergeCatalogTitleMaps(
    stateTitleById.value,
    stateItems.value
  ),
  priorityTitleById: mergeCatalogTitleMaps(
    priorityTitleById.value,
    priorityItems.value
  ),
  typeTitleById: mergeCatalogTitleMaps(typeTitleById.value, typeItems.value),
  boardTitleById: mergeCatalogTitleMaps(boardTitleById.value, boardItems.value),
  personTitleById: personTitleById.value,
}));

function seedCatalogTitleMapsFromItems() {
  stateTitleById.value = mergeCatalogTitleMaps(stateTitleById.value, stateItems.value);
  priorityTitleById.value = mergeCatalogTitleMaps(priorityTitleById.value, priorityItems.value);
  typeTitleById.value = mergeCatalogTitleMaps(typeTitleById.value, typeItems.value);
  boardTitleById.value = mergeCatalogTitleMaps(boardTitleById.value, boardItems.value);
}

function registerTitlesFromPolicy(policy: OcWorkspaceFieldPolicy, targetFieldKey: string) {
  const register = (fieldKey: string, value: unknown) => {
    const ids: string[] = [];
    if (Array.isArray(value)) {
      for (const v of value) {
        const s = v != null ? String(v).trim() : '';
        if (s) ids.push(s);
      }
    } else if (value != null && value !== '') {
      const s = String(value).trim();
      if (s) ids.push(s);
    }
    for (const id of ids) {
      if (fieldKey === 'stateId') {
        const title = stateItems.value.find((s) => s.value === id)?.title;
        if (title && title !== id) stateTitleById.value = new Map(stateTitleById.value).set(id, title);
      } else if (fieldKey === 'typeId') {
        const title = typeItems.value.find((x) => x.value === id)?.title;
        if (title && title !== id) typeTitleById.value = new Map(typeTitleById.value).set(id, title);
      } else if (fieldKey === 'priorityId') {
        const title = priorityItems.value.find((p) => p.value === id)?.title;
        if (title && title !== id) priorityTitleById.value = new Map(priorityTitleById.value).set(id, title);
      } else if (fieldKey === 'boardId') {
        const title = boardItems.value.find((b) => b.value === id)?.title;
        if (title && title !== id) boardTitleById.value = new Map(boardTitleById.value).set(id, title);
      }
    }
  };

  if (policy.kind === 'defaultValue') register(targetFieldKey, policy.value);
  for (const clause of policy.conditions?.clauses ?? []) {
    register(clause.fieldKey, clause.value);
  }
}

async function loadFullCatalogTitleMaps() {
  const [allStates, allPriorities, globalTypes, scopedTypes, boards] = await Promise.all([
    ocListStates(),
    ocListPriorities(),
    ocListGlobalWorkItemTypes(),
    props.workspaceId ? ocListWorkspaceScopedWorkItemTypes(props.workspaceId) : Promise.resolve([]),
    props.workspaceId ? Promise.resolve(catalog.boards.value) : Promise.resolve([]),
  ]);

  const typeSeen = new Set<string>();
  const typeMap = new Map<string, string>();
  for (const t of [...globalTypes, ...scopedTypes]) {
    if (!t.__dataId || typeSeen.has(t.__dataId)) continue;
    typeSeen.add(t.__dataId);
    typeMap.set(t.__dataId, t.name?.trim() || t.__dataId);
  }

  stateTitleById.value = new Map(
    allStates.map((s) => [s.__dataId, s.name?.trim() || s.__dataId])
  );
  priorityTitleById.value = new Map(
    allPriorities.map((p) => [p.__dataId, p.name?.trim() || p.__dataId])
  );
  typeTitleById.value = typeMap;
  boardTitleById.value = new Map(
    boards.map((b) => [b.__dataId, b.name?.trim() || b.__dataId])
  );
}

async function enrichMissingCatalogTitles(blob: OcWorkspaceFieldPoliciesBlob) {
  const refs = collectPolicyReferencedCatalogIds(blob);
  const tasks: Promise<void>[] = [];

  for (const id of refs.stateIds) {
    if (stateTitleById.value.has(id) && stateTitleById.value.get(id) !== id) continue;
    tasks.push(
      ocGetDatasetRecordTitle(OC_DATASETS.states, id).then((title) => {
        if (title) stateTitleById.value = new Map(stateTitleById.value).set(id, title);
      })
    );
  }
  for (const id of refs.typeIds) {
    if (typeTitleById.value.has(id) && typeTitleById.value.get(id) !== id) continue;
    tasks.push(
      ocGetDatasetRecordTitle(OC_DATASETS.workItemTypes, id).then((title) => {
        if (title) typeTitleById.value = new Map(typeTitleById.value).set(id, title);
      })
    );
  }
  for (const id of refs.priorityIds) {
    if (priorityTitleById.value.has(id) && priorityTitleById.value.get(id) !== id) continue;
    tasks.push(
      ocGetDatasetRecordTitle(OC_DATASETS.priorities, id).then((title) => {
        if (title) priorityTitleById.value = new Map(priorityTitleById.value).set(id, title);
      })
    );
  }
  for (const id of refs.boardIds) {
    if (boardTitleById.value.has(id) && boardTitleById.value.get(id) !== id) continue;
    tasks.push(
      ocGetDatasetRecordTitle(OC_DATASETS.boards, id).then((title) => {
        if (title) boardTitleById.value = new Map(boardTitleById.value).set(id, title);
      })
    );
  }

  if (tasks.length) await Promise.all(tasks);
}

async function resolvePersonTitlesForPolicySummaries() {
  const ids = collectPersonIdsFromWorkspacePolicies(policiesBlob.value, fieldMetaByKey.value);
  if (!ids.length) {
    personTitleById.value = new Map();
    return;
  }
  await personPicker.ensureSelectedIds(ids);
  const map = new Map<string, string>();
  for (const id of ids) {
    const fromPicker = personPicker.items.value.find((i) => i.value === id);
    if (fromPicker?.title && fromPicker.title !== id) {
      map.set(id, fromPicker.title);
      continue;
    }
    const user = userStore.getUserById(id);
    if (user) {
      map.set(id, buildOcPersonPickerTitle(user));
      continue;
    }
    try {
      await userStore.fetchUserById(id);
      const fetched = userStore.getUserById(id);
      map.set(id, fetched ? buildOcPersonPickerTitle(fetched) : id);
    } catch {
      map.set(id, id);
    }
  }
  personTitleById.value = map;
}

async function refreshPolicySummaryTitles(blob: OcWorkspaceFieldPoliciesBlob) {
  seedCatalogTitleMapsFromItems();
  await enrichMissingCatalogTitles(blob);
  await resolvePersonTitlesForPolicySummaries();
}

const summaryLabels = computed(() => ({
  kindVisibility: t('operationCore.workspaceDefinitions.policies.kindVisibility'),
  kindReadonly: t('operationCore.workspaceDefinitions.policies.kindReadonly'),
  kindDefaultValue: t('operationCore.workspaceDefinitions.policies.kindDefaultValue'),
  scopeAlways: t('operationCore.workspaceDefinitions.policies.policyScopeAlways'),
  scopeConditional: t('operationCore.workspaceDefinitions.policies.policyScopeConditional'),
  alwaysVisible: t('operationCore.workspaceDefinitions.policies.summaryAlwaysVisible'),
  alwaysHidden: t('operationCore.workspaceDefinitions.policies.summaryAlwaysHidden'),
  conditionalVisible: t('operationCore.workspaceDefinitions.policies.summaryConditionalVisible'),
  conditionalHidden: t('operationCore.workspaceDefinitions.policies.summaryConditionalHidden'),
  alwaysReadonly: t('operationCore.workspaceDefinitions.policies.summaryAlwaysReadonly'),
  alwaysEditable: t('operationCore.workspaceDefinitions.policies.summaryAlwaysEditable'),
  conditionalReadonly: t('operationCore.workspaceDefinitions.policies.summaryConditionalReadonly'),
  conditionalEditable: t('operationCore.workspaceDefinitions.policies.summaryConditionalEditable'),
  defaultValueAlways: t('operationCore.workspaceDefinitions.policies.summaryDefaultAlways'),
  defaultValueConditional: t('operationCore.workspaceDefinitions.policies.summaryDefaultConditional'),
  operatorEq: t('operationCore.workspaceDefinitions.policies.operatorEq'),
  operatorNe: t('operationCore.workspaceDefinitions.policies.operatorNe'),
  andJoin: t('operationCore.workspaceDefinitions.policies.andJoin'),
  emptyConditions: t('operationCore.workspaceDefinitions.policies.summaryEmptyConditions'),
}));

const addPolicyMenuItems = computed(() => [
  {
    kind: 'visibility' as const,
    title: t('operationCore.workspaceDefinitions.policies.addVisibility'),
    subtitle: t('operationCore.workspaceDefinitions.policies.addVisibilityHint'),
    icon: 'mdi-eye-settings-outline',
    color: 'primary',
  },
  {
    kind: 'readonly' as const,
    title: t('operationCore.workspaceDefinitions.policies.addReadonly'),
    subtitle: t('operationCore.workspaceDefinitions.policies.addReadonlyHint'),
    icon: 'mdi-lock-outline',
    color: 'warning',
  },
  {
    kind: 'defaultValue' as const,
    title: t('operationCore.workspaceDefinitions.policies.addDefaultValue'),
    subtitle: t('operationCore.workspaceDefinitions.policies.addDefaultValueHint'),
    icon: 'mdi-form-dropdown',
    color: 'secondary',
  },
]);

const howItWorksSteps = computed(() => [
  {
    icon: 'mdi-form-textbox',
    color: 'primary',
    title: t('operationCore.workspaceDefinitions.policies.howStep1Title'),
    body: t('operationCore.workspaceDefinitions.policies.howStep1Body'),
  },
  {
    icon: 'mdi-tune-variant',
    color: 'secondary',
    title: t('operationCore.workspaceDefinitions.policies.howStep2Title'),
    body: t('operationCore.workspaceDefinitions.policies.howStep2Body'),
  },
  {
    icon: 'mdi-eye-check-outline',
    color: 'success',
    title: t('operationCore.workspaceDefinitions.policies.howStep3Title'),
    body: t('operationCore.workspaceDefinitions.policies.howStep3Body'),
  },
]);

const totalPolicyCount = computed(() => {
  const byField = policiesBlob.value.policiesByField ?? {};
  return Object.keys(byField).reduce(
    (sum, key) => sum + policiesForField(policiesBlob.value, key).length,
    0
  );
});

const fieldsWithPoliciesCount = computed(() => {
  const byField = policiesBlob.value.policiesByField ?? {};
  return Object.keys(byField).filter(
    (key) => policiesForField(policiesBlob.value, key).length > 0
  ).length;
});

const policyTableHeaders = computed(() => [
  { title: t('operationCore.workspaceDefinitions.policies.colKind'), key: 'kind', width: 140 },
  { title: t('operationCore.workspaceDefinitions.policies.colScope'), key: 'summary' },
  { title: t('operationCore.workspaceDefinitions.policies.colActions'), key: 'actions', align: 'end' as const },
]);

function policyKindColor(kind: OcWorkspaceFieldPolicyKind): string {
  if (kind === 'readonly') return 'warning';
  if (kind === 'defaultValue') return 'secondary';
  return 'primary';
}

function openAddPolicy(kind: OcWorkspaceFieldPolicyKind) {
  dialogKind.value = kind;
  editingPolicy.value = null;
  policyDialog.value = true;
}

function openEditPolicy(policy: OcWorkspaceFieldPolicy) {
  dialogKind.value = policy.kind;
  editingPolicy.value = policy;
  policyDialog.value = true;
}

async function onPolicySaved(policy: OcWorkspaceFieldPolicy) {
  const key = selectedFieldKey.value;
  if (!key) return;
  const current = policiesForField(policiesBlob.value, key);
  const idx = current.findIndex((p) => p.id === policy.id);
  const next =
    idx >= 0 ? current.map((p, i) => (i === idx ? policy : p)) : [...current, policy];
  const blob = setPoliciesForField(policiesBlob.value, key, next);
  policiesBlob.value = blob;
  registerTitlesFromPolicy(policy, key);
  await refreshPolicySummaryTitles(blob);
  await persistBlob(blob);
}

function confirmDelete(policy: OcWorkspaceFieldPolicy) {
  deletePolicyTarget.value = policy;
  deleteDialog.value = true;
}

async function doDelete() {
  const key = selectedFieldKey.value;
  const target = deletePolicyTarget.value;
  deleteDialog.value = false;
  deletePolicyTarget.value = null;
  if (!key || !target) return;
  const next = policiesForField(policiesBlob.value, key).filter((p) => p.id !== target.id);
  await persistBlob(setPoliciesForField(policiesBlob.value, key, next));
}

watch(
  () => props.catalogFieldKeys,
  (keys) => {
    if (!keys.length) {
      selectedFieldKey.value = null;
      return;
    }
    if (!selectedFieldKey.value || !keys.includes(selectedFieldKey.value)) {
      selectedFieldKey.value = keys[0] ?? null;
    }
  },
  { immediate: true }
);

async function loadData() {
  if (!props.workspaceId) return;
  loading.value = true;
  errorLocal.value = null;
  try {
    await catalog.whenReady();
    await loadFullCatalogTitleMaps();
    const ws = catalog.workspace.value;
    const stateRows = catalog.states.value;
    const priorities = catalog.priorities.value;
    const types = catalog.types.value;
    const boards = catalog.boards.value;
    states.value = stateRows;
    priorityItems.value = priorities.map((p) => ({
      value: p.__dataId,
      title: p.name?.trim() || p.__dataId,
    }));
    typeItems.value = types.map((x) => ({
      value: x.__dataId,
      title: x.name?.trim() || x.__dataId,
    }));
    boardItems.value = boards.map((b) => ({
      value: b.__dataId,
      title: b.name?.trim() || b.__dataId,
    }));
    seedCatalogTitleMapsFromItems();
    const settings =
      ws?.settings && typeof ws.settings === 'object' && !Array.isArray(ws.settings)
        ? (ws.settings as Record<string, unknown>)
        : {};
    settingsBase.value = { ...settings };
    policiesBlob.value = parseWorkspaceFieldPoliciesFromSettings(settings);
    await refreshPolicySummaryTitles(policiesBlob.value);
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.policies.loadError')
    );
  } finally {
    loading.value = false;
  }
}

async function persistBlob(blob: OcWorkspaceFieldPoliciesBlob) {
  if (!props.workspaceId) return;
  saving.value = true;
  errorLocal.value = null;
  successLocal.value = null;
  try {
    const settings = mergeFieldPoliciesIntoSettings(settingsBase.value, blob);
    await ocUpdateWorkspace(props.workspaceId, { settings });
    settingsBase.value = settings;
    policiesBlob.value = blob;
    await refreshPolicySummaryTitles(blob);
    await metaCache.applySaveSuccess(
      (msg) => {
        successLocal.value = msg;
      },
      t('operationCore.workspaceDefinitions.saveSuccess')
    );
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.policies.saveError')
    );
  } finally {
    saving.value = false;
  }
}

watch(
  () => [policiesBlob.value, fieldMetaByKey.value] as const,
  () => {
    void refreshPolicySummaryTitles(policiesBlob.value);
  },
  { deep: true }
);

watch(
  () => props.workspaceId,
  () => {
    void loadData();
  },
  { immediate: true }
);
</script>

<template>
  <div class="oc-ws-field-policy-explorer pa-4 pa-md-5">
    <div class="oc-ws-field-policy-explorer__hero mb-5">
      <div class="mb-4" style="max-width: 720px">
        <h3 class="text-h6 font-weight-bold mb-2">
          {{ t('operationCore.workspaceDefinitions.policies.pageTitle') }}
        </h3>
        <p class="text-body-2 text-medium-emphasis mb-0">
          {{ t('operationCore.workspaceDefinitions.policies.pageSubtitle') }}
        </p>
      </div>

      <v-row dense>
        <v-col v-for="(step, idx) in howItWorksSteps" :key="idx" cols="12" md="4">
          <v-card variant="outlined" rounded="lg" class="h-100 oc-ws-field-policy-explorer__how-card">
            <v-card-text class="pa-4">
              <div class="d-flex align-start gap-3">
                <v-avatar :color="step.color" variant="tonal" size="40" rounded="lg">
                  <v-icon :icon="step.icon" size="22" />
                </v-avatar>
                <div>
                  <div class="text-body-1 font-weight-bold mb-1">{{ step.title }}</div>
                  <p class="text-body-2 text-medium-emphasis mb-0">{{ step.body }}</p>
                </div>
              </div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </div>

    <v-alert type="info" variant="tonal" density="compact" class="mb-4 rounded-lg">
      {{ t('operationCore.workspaceDefinitions.policies.vsRulesBanner') }}
    </v-alert>

    <v-alert
      v-if="errorLocal"
      type="error"
      variant="tonal"
      class="mb-4 rounded-lg"
      closable
      @click:close="errorLocal = null"
    >
      {{ errorLocal }}
    </v-alert>
    <v-alert
      v-if="successLocal"
      type="success"
      variant="tonal"
      class="mb-4 rounded-lg"
      closable
      @click:close="successLocal = null"
    >
      {{ successLocal }}
    </v-alert>

    <v-progress-linear
      v-if="loading || saving"
      indeterminate
      color="primary"
      class="mb-4 rounded-pill"
    />

    <div v-if="!loading && fieldCatalog.length && totalPolicyCount" class="d-flex flex-wrap ga-3 mb-4">
      <v-chip variant="tonal" color="primary" class="text-none">
        <v-icon icon="mdi-shield-account-outline" start size="16" />
        {{ t('operationCore.workspaceDefinitions.policies.statsTotal', { count: totalPolicyCount }) }}
      </v-chip>
      <v-chip variant="tonal" color="secondary" class="text-none">
        <v-icon icon="mdi-form-textbox" start size="16" />
        {{
          t('operationCore.workspaceDefinitions.policies.statsFields', {
            count: fieldsWithPoliciesCount,
          })
        }}
      </v-chip>
    </div>

    <v-card
      v-if="!loading && !fieldCatalog.length"
      variant="outlined"
      rounded="lg"
      class="oc-ws-field-policy-explorer__empty text-center pa-8 pa-md-12"
    >
      <v-avatar color="warning" variant="tonal" size="72" rounded="lg" class="mb-4">
        <v-icon icon="mdi-database-off-outline" size="36" />
      </v-avatar>
      <h4 class="text-h6 font-weight-bold mb-2">
        {{ t('operationCore.workspaceDefinitions.policies.emptyCatalogTitle') }}
      </h4>
      <p class="text-body-2 text-medium-emphasis mx-auto mb-0" style="max-width: 480px">
        {{ t('operationCore.workspaceDefinitions.policies.emptyCatalog') }}
      </p>
    </v-card>

    <v-row v-else-if="fieldCatalog.length" class="oc-ws-field-policy-explorer__grid" dense>
      <v-col cols="12" md="4" lg="3">
        <v-card variant="outlined" rounded="lg" class="h-100">
          <v-card-title class="text-subtitle-1 font-weight-bold py-3 px-4">
            {{ t('operationCore.workspaceDefinitions.policies.fieldCatalogTitle') }}
          </v-card-title>
          <p class="text-caption text-medium-emphasis px-4 pb-2 mb-0">
            {{ t('operationCore.workspaceDefinitions.policies.fieldCatalogHint') }}
          </p>
          <v-divider />
          <v-list density="compact" nav class="py-1">
            <v-list-item
              v-for="field in fieldCatalog"
              :key="field.key"
              :active="selectedFieldKey === field.key"
              rounded="lg"
              class="mx-2 my-1"
              @click="selectedFieldKey = field.key"
            >
              <template #prepend>
                <v-icon size="small" icon="mdi-form-textbox" />
              </template>
              <v-list-item-title class="text-body-2 font-weight-medium">
                {{ field.label }}
              </v-list-item-title>
              <v-list-item-subtitle class="text-caption">
                {{ field.key }}
              </v-list-item-subtitle>
              <template v-if="field.policyCount" #append>
                <v-chip size="x-small" variant="tonal" color="primary">
                  {{ field.policyCount }}
                </v-chip>
              </template>
            </v-list-item>
          </v-list>
        </v-card>
      </v-col>

      <v-col cols="12" md="8" lg="9">
        <v-card variant="outlined" rounded="lg" class="h-100">
          <v-card-title class="d-flex flex-wrap align-center gap-2 py-3 px-4">
            <div>
              <div class="text-subtitle-1 font-weight-bold">
                {{
                  selectedField
                    ? t('operationCore.workspaceDefinitions.policies.selectedFieldTitle', {
                        field: selectedField.label,
                      })
                    : t('operationCore.workspaceDefinitions.policies.selectField')
                }}
              </div>
              <div v-if="selectedField" class="text-caption text-medium-emphasis">
                {{ selectedField.key }}
              </div>
            </div>
            <v-spacer />
            <v-menu location="bottom end">
              <template #activator="{ props: menuProps }">
                <v-btn
                  v-bind="menuProps"
                  color="primary"
                  variant="flat"
                  rounded="lg"
                  class="text-none"
                  prepend-icon="mdi-plus"
                  :disabled="!selectedFieldKey || saving"
                >
                  {{ t('operationCore.workspaceDefinitions.policies.addPolicy') }}
                </v-btn>
              </template>
              <v-list density="comfortable" min-width="280">
                <v-list-item
                  v-for="item in addPolicyMenuItems"
                  :key="item.kind"
                  :prepend-icon="item.icon"
                  @click="openAddPolicy(item.kind)"
                >
                  <v-list-item-title>{{ item.title }}</v-list-item-title>
                  <v-list-item-subtitle>{{ item.subtitle }}</v-list-item-subtitle>
                </v-list-item>
              </v-list>
            </v-menu>
          </v-card-title>
          <v-divider />

          <v-card-text class="pa-4">
            <v-card
              v-if="!selectedPolicies.length"
              variant="outlined"
              rounded="lg"
              class="oc-ws-field-policy-explorer__field-empty text-center pa-6 pa-md-8 mb-4"
            >
              <p class="text-body-2 text-medium-emphasis mb-4">
                {{ t('operationCore.workspaceDefinitions.policies.noPoliciesForField') }}
              </p>
              <div class="d-flex flex-wrap justify-center ga-3">
                <v-btn
                  v-for="item in addPolicyMenuItems"
                  :key="item.kind"
                  variant="tonal"
                  :color="item.color"
                  rounded="lg"
                  class="text-none"
                  :prepend-icon="item.icon"
                  :disabled="saving"
                  @click="openAddPolicy(item.kind)"
                >
                  {{ item.title }}
                </v-btn>
              </div>
            </v-card>

            <v-data-table
              v-else
              :headers="policyTableHeaders"
              :items="selectedPolicies"
              item-key="id"
              density="comfortable"
              hide-default-footer
              class="oc-ws-policy-table"
            >
              <template #[`item.kind`]="{ item }">
                <v-chip size="small" variant="tonal" :color="policyKindColor(item.kind)" class="text-none">
                  {{ workspacePolicyKindLabel(item.kind, summaryLabels) }}
                </v-chip>
              </template>
              <template #[`item.summary`]="{ item }">
                <span class="text-body-2">
                  {{
                    selectedFieldKey
                      ? formatWorkspaceFieldPolicySummary(
                          item,
                          selectedFieldKey,
                          valueResolveContext,
                          summaryLabels
                        )
                      : ''
                  }}
                </span>
              </template>
              <template #[`item.actions`]="{ item }">
                <div class="d-flex justify-end ga-1">
                  <v-tooltip :text="t('operationCore.workspaceDefinitions.policies.editTooltip')" location="top">
                    <template #activator="{ props: tipProps }">
                      <v-btn
                        v-bind="tipProps"
                        icon="mdi-pencil-outline"
                        size="small"
                        variant="text"
                        :disabled="saving"
                        @click="openEditPolicy(item)"
                      />
                    </template>
                  </v-tooltip>
                  <v-tooltip :text="t('operationCore.workspaceDefinitions.policies.deleteTooltip')" location="top">
                    <template #activator="{ props: tipProps }">
                      <v-btn
                        v-bind="tipProps"
                        icon="mdi-delete-outline"
                        size="small"
                        variant="text"
                        color="error"
                        :disabled="saving"
                        @click="confirmDelete(item)"
                      />
                    </template>
                  </v-tooltip>
                </div>
              </template>
            </v-data-table>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <p v-if="!loading && fieldCatalog.length" class="text-caption text-medium-emphasis mt-4 mb-0">
      {{ t('operationCore.workspaceDefinitions.policies.technicalFootnote') }}
    </p>

    <OcWorkspacePolicyDialog
      v-if="selectedField"
      v-model="policyDialog"
      :kind="dialogKind"
      :target-field="selectedField"
      :policy="editingPolicy"
      :workspace-id="workspaceId"
      :condition-fields="conditionFieldOptions"
      :value-resolve-context="valueResolveContext"
      :type-items="typeItems"
      :priority-items="priorityItems"
      :state-items="stateItems"
      :board-items="boardItems"
      :saving="saving"
      @save="onPolicySaved"
    />

    <v-dialog v-model="deleteDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="d-flex align-center gap-2 pt-5 px-5">
          <v-icon icon="mdi-alert-circle-outline" color="warning" />
          {{ t('operationCore.workspaceDefinitions.policies.deletePolicyTitle') }}
        </v-card-title>
        <v-card-text class="px-5 pb-2">
          <p class="mb-2">{{ t('operationCore.workspaceDefinitions.policies.deletePolicyBody') }}</p>
        </v-card-text>
        <v-card-actions class="pa-4 px-5">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialog = false">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn color="error" variant="flat" rounded="lg" class="text-none" :loading="saving" @click="doDelete">
            {{ t('operationCore.definitions.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.oc-ws-field-policy-explorer__how-card {
  transition: border-color 0.15s ease;
}

.oc-ws-field-policy-explorer__how-card:hover {
  border-color: rgba(var(--v-theme-primary), 0.35);
}

.oc-ws-field-policy-explorer__empty,
.oc-ws-field-policy-explorer__field-empty {
  border-style: dashed;
}

.oc-ws-field-policy-explorer__grid {
  min-height: 320px;
}
</style>
