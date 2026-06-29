<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useOcPersonPicker } from '@/composables/useOcPersonPicker';
import { useOcWorkspaceCatalogInject } from '@/composables/useOcWorkspaceCatalog';
import { useUserStore } from '@/stores/apps/user';
import OcWorkspaceRuleDialog from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceRuleDialog.vue';
import {
  ocCreateRule,
  ocDeleteRule,
  ocListPoolFieldsForWorkspace,
  ocListRulesForWorkspace,
  ocListStateFlowsForWorkspace,
  ocUpdateRule,
} from '@/services/operationCoreService';
import {
  OC_CORE_WORK_ITEM_FIELDS,
  OC_POLICY_CONDITION_ALWAYS_CORE_KEYS,
} from '@/utils/ocFieldDefinitions';
import { resolveOcFieldDisplayLabel } from '@/utils/ocFormFieldLabels';
import { buildOcPersonPickerTitle } from '@/utils/ocPersonPicker';
import { moRuleConditionsToClauses, type OcConditionFieldOption } from '@/utils/ocConditionClauses';
import {
  buildRuleConditionFieldOptions,
  formatRuleScopeSummary,
  formatRuleThenSummary,
  formatRuleWhenSummary,
  type OcWorkspaceRuleCatalogContext,
  type OcWorkspaceRuleTrigger,
  type OcWorkspaceRuleType,
} from '@/utils/ocWorkspaceRules';
import type { OpField, OpRule, OpStateFlow } from '@/types/apps/operationCore';

const props = defineProps<{
  workspaceId: string;
  catalogFieldKeys: string[];
  poolFields: OpField[];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const personPicker = useOcPersonPicker();
const catalog = useOcWorkspaceCatalogInject();
const userStore = useUserStore();

const loading = ref(false);
const saving = ref(false);
const deleting = ref(false);
const errorLocal = ref<string | null>(null);

const rules = ref<OpRule[]>([]);
const stateFlows = ref<OpStateFlow[]>([]);
const typeItems = ref<{ value: string; title: string }[]>([]);
const boardItems = ref<{ value: string; title: string }[]>([]);
const stateItems = ref<{ value: string; title: string }[]>([]);
const priorityItems = ref<{ value: string; title: string }[]>([]);

const personTitleById = ref<Map<string, string>>(new Map());
const typeTitleById = ref<Map<string, string>>(new Map());
const boardTitleById = ref<Map<string, string>>(new Map());
const stateTitleById = ref<Map<string, string>>(new Map());

const selectedTrigger = ref<'all' | OcWorkspaceRuleTrigger>('all');
const filterTypeId = ref('');
const filterRuleType = ref<'all' | OcWorkspaceRuleType>('all');
const activeOnly = ref(false);

const ruleDialog = ref(false);
const editingRule = ref<OpRule | null>(null);
const deleteDialog = ref(false);
const deleteTarget = ref<OpRule | null>(null);
const successLocal = ref<string | null>(null);

const howItWorksSteps = computed(() => [
  {
    icon: 'mdi-lightning-bolt-outline',
    color: 'primary',
    title: t('operationCore.workspaceDefinitions.rules.howStep1Title'),
    body: t('operationCore.workspaceDefinitions.rules.howStep1Body'),
  },
  {
    icon: 'mdi-target',
    color: 'secondary',
    title: t('operationCore.workspaceDefinitions.rules.howStep2Title'),
    body: t('operationCore.workspaceDefinitions.rules.howStep2Body'),
  },
  {
    icon: 'mdi-play-circle-outline',
    color: 'success',
    title: t('operationCore.workspaceDefinitions.rules.howStep3Title'),
    body: t('operationCore.workspaceDefinitions.rules.howStep3Body'),
  },
]);

const triggerNavItems = computed(() => [
  {
    key: 'all' as const,
    label: t('operationCore.workspaceDefinitions.rules.filterAll'),
    icon: 'mdi-format-list-bulleted',
  },
  {
    key: 'WorkItemCreated' as const,
    label: t('operationCore.workspaceDefinitions.rules.triggerCreated'),
    icon: 'mdi-plus-circle-outline',
  },
  {
    key: 'WorkItemTransition' as const,
    label: t('operationCore.workspaceDefinitions.rules.triggerTransition'),
    icon: 'mdi-transit-connection-variant',
  },
  {
    key: 'WorkItemUpdated' as const,
    label: t('operationCore.workspaceDefinitions.rules.triggerUpdated'),
    icon: 'mdi-pencil-outline',
  },
]);

const conditionFields = computed((): OcConditionFieldOption[] => {
  const fromCatalog = props.catalogFieldKeys.map((key) => {
    const pool = props.poolFields.find((f) => f.key === key);
    const core = OC_CORE_WORK_ITEM_FIELDS.find((c) => c.key === key);
    return {
      key,
      label:
        resolveOcFieldDisplayLabel(key, {
          poolLabel: pool?.label?.trim() || null,
          translate: t,
        }) || key,
      fieldType: pool?.fieldType ?? core?.fieldType,
      relationDataset: pool?.relationDatasetName ?? null,
      cardinality: pool?.cardinality,
    };
  });
  for (const key of OC_POLICY_CONDITION_ALWAYS_CORE_KEYS) {
    if (!fromCatalog.some((f) => f.key === key)) {
      const core = OC_CORE_WORK_ITEM_FIELDS.find((c) => c.key === key);
      fromCatalog.push({
        key,
        label: resolveOcFieldDisplayLabel(key, { translate: t }) || key,
        fieldType: core?.fieldType,
        relationDataset: null,
        cardinality: core?.cardinality,
      });
    }
  }
  return buildRuleConditionFieldOptions(fromCatalog);
});

const fieldLabelByKey = computed(() => {
  const m = new Map<string, string>();
  for (const f of conditionFields.value) m.set(f.key, f.label);
  return m;
});

const transitionItems = computed(() => {
  const seen = new Set<string>();
  const out: { value: string; title: string }[] = [];
  for (const flow of stateFlows.value) {
    for (const tr of flow.transitions ?? []) {
      const key = tr.transitionKey?.trim();
      if (!key || seen.has(key)) continue;
      seen.add(key);
      out.push({
        value: key,
        title: `${flow.name} · ${tr.label?.trim() || key}`,
      });
    }
  }
  return out.sort((a, b) => a.title.localeCompare(b.title));
});

const catalogContext = computed((): OcWorkspaceRuleCatalogContext => ({
  fieldLabelByKey: fieldLabelByKey.value,
  typeTitleById: typeTitleById.value,
  boardTitleById: boardTitleById.value,
  stateTitleById: stateTitleById.value,
  personTitleById: personTitleById.value,
  operatorLabels: {
    eq: t('operationCore.workspaceDefinitions.rules.operatorEq'),
    ne: t('operationCore.workspaceDefinitions.rules.operatorNe'),
    empty: t('operationCore.workspaceDefinitions.rules.operatorEmpty'),
    notEmpty: t('operationCore.workspaceDefinitions.rules.operatorNotEmpty'),
    gt: t('operationCore.workspaceDefinitions.rules.operatorGt'),
    lt: t('operationCore.workspaceDefinitions.rules.operatorLt'),
  },
  andJoin: t('operationCore.workspaceDefinitions.policies.andJoin'),
}));

function ruleCountForTrigger(key: 'all' | OcWorkspaceRuleTrigger): number {
  if (key === 'all') return rules.value.length;
  return rules.value.filter((r) => r.trigger === key).length;
}

const filteredRules = computed(() => {
  let list = rules.value;
  if (selectedTrigger.value !== 'all') {
    list = list.filter((r) => r.trigger === selectedTrigger.value);
  }
  if (filterTypeId.value) {
    list = list.filter((r) => r.typeId === filterTypeId.value);
  }
  if (filterRuleType.value !== 'all') {
    list = list.filter((r) => (r.ruleType?.toLowerCase() ?? 'default') === filterRuleType.value);
  }
  if (activeOnly.value) {
    list = list.filter((r) => r.isActive !== false);
  }
  return [...list].sort((a, b) => {
    const pa = a.priority ?? 100;
    const pb = b.priority ?? 100;
    if (pb !== pa) return pb - pa;
    return (a.name ?? '').localeCompare(b.name ?? '', undefined, { sensitivity: 'base' });
  });
});

const activeCount = computed(() => rules.value.filter((r) => r.isActive !== false).length);

function ruleTypeLabel(ruleType: string): string {
  const key = ruleType?.toLowerCase();
  if (key === 'validation') return t('operationCore.workspaceDefinitions.rules.ruleTypeValidation');
  if (key === 'default') return t('operationCore.workspaceDefinitions.rules.ruleTypeDefault');
  if (key === 'automation') return t('operationCore.workspaceDefinitions.rules.ruleTypeAutomation');
  return ruleType;
}

function ruleTypeChipColor(ruleType: string): string {
  const key = ruleType?.toLowerCase();
  if (key === 'validation') return 'warning';
  if (key === 'automation') return 'info';
  return 'primary';
}

function triggerLabel(trigger: string): string {
  if (trigger === 'WorkItemCreated') return t('operationCore.workspaceDefinitions.rules.triggerCreated');
  if (trigger === 'WorkItemTransition') return t('operationCore.workspaceDefinitions.rules.triggerTransition');
  if (trigger === 'WorkItemUpdated') return t('operationCore.workspaceDefinitions.rules.triggerUpdated');
  return trigger;
}

const tableHeaders = computed(() => [
  { title: t('operationCore.workspaceDefinitions.rules.colName'), key: 'name', sortable: true },
  { title: t('operationCore.workspaceDefinitions.rules.colType'), key: 'ruleType', sortable: true },
  { title: t('operationCore.workspaceDefinitions.rules.priority'), key: 'priority', sortable: true, width: 88 },
  { title: t('operationCore.workspaceDefinitions.rules.colTrigger'), key: 'trigger', sortable: false },
  { title: t('operationCore.workspaceDefinitions.rules.colScope'), key: 'scope', sortable: false },
  { title: t('operationCore.workspaceDefinitions.rules.colWhen'), key: 'when', sortable: false },
  { title: t('operationCore.workspaceDefinitions.rules.colThen'), key: 'then', sortable: false },
  { title: t('operationCore.workspaceDefinitions.rules.colStatus'), key: 'isActive', sortable: true },
  { title: t('operationCore.workspaceDefinitions.rules.colActions'), key: 'actions', sortable: false, align: 'end' as const },
]);

async function resolvePersonTitles(ids: string[]) {
  if (!ids.length) {
    personTitleById.value = new Map();
    return;
  }
  await personPicker.ensureSelectedIds(ids);
  const map = new Map<string, string>();
  await Promise.all(
    ids.map(async (id) => {
      const fromPicker = personPicker.items.value.find((i) => i.value === id);
      if (fromPicker?.title && fromPicker.title !== id) {
        map.set(id, fromPicker.title);
        return;
      }
      const user = userStore.getUserById(id);
      if (user) {
        map.set(id, buildOcPersonPickerTitle(user));
        return;
      }
      try {
        await userStore.fetchUserById(id);
        const fetched = userStore.getUserById(id);
        map.set(id, fetched ? buildOcPersonPickerTitle(fetched) : id);
      } catch {
        map.set(id, id);
      }
    })
  );
  personTitleById.value = map;
}

function collectPersonIdsFromRules(list: OpRule[]): string[] {
  const ids = new Set<string>();
  for (const rule of list) {
    for (const clause of moRuleConditionsToClauses(rule.conditions)) {
      if (clause.fieldKey === 'assignee' && clause.value) ids.add(String(clause.value));
    }
    const actions = Array.isArray(rule.actions) ? rule.actions : [];
    for (const raw of actions) {
      if (!raw || typeof raw !== 'object') continue;
      const a = raw as Record<string, unknown>;
      if (String(a.type ?? a.Type).toLowerCase() === 'setassignee') {
        const id = String(a.assignee ?? a.Assignee ?? a.value ?? a.Value ?? '');
        if (id) ids.add(id);
      }
    }
  }
  return [...ids];
}

function seedTitleMaps(
  types: { value: string; title: string }[],
  boards: { value: string; title: string }[],
  states: { value: string; title: string }[]
) {
  typeTitleById.value = new Map(types.map((i) => [i.value, i.title]));
  boardTitleById.value = new Map(boards.map((i) => [i.value, i.title]));
  stateTitleById.value = new Map(states.map((i) => [i.value, i.title]));
}

async function loadAll() {
  if (!props.workspaceId) {
    rules.value = [];
    return;
  }
  loading.value = true;
  errorLocal.value = null;
  try {
    const [ruleRows, flows] = await Promise.all([
      ocListRulesForWorkspace(props.workspaceId),
      ocListStateFlowsForWorkspace(props.workspaceId),
      catalog.whenReady(),
    ]);
    const types = catalog.types.value;
    const boards = catalog.boards.value;
    const states = catalog.states.value;
    const priorities = catalog.priorities.value;
    rules.value = ruleRows;
    stateFlows.value = flows;
    typeItems.value = types.map((x) => ({ value: x.__dataId, title: x.name }));
    boardItems.value = boards.map((b) => ({ value: b.__dataId, title: b.name }));
    stateItems.value = states.map((s) => ({ value: s.__dataId, title: s.name }));
    priorityItems.value = priorities.map((p) => ({ value: p.__dataId, title: p.name }));
    seedTitleMaps(typeItems.value, boardItems.value, stateItems.value);
    await resolvePersonTitles(collectPersonIdsFromRules(ruleRows));
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.forms.rulesLoadError');
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
  editingRule.value = null;
  ruleDialog.value = true;
}

function openEdit(rule: OpRule) {
  editingRule.value = rule;
  ruleDialog.value = true;
}

async function onSaveRule(payload: Record<string, unknown>) {
  if (!props.workspaceId) return;
  saving.value = true;
  errorLocal.value = null;
  try {
    if (editingRule.value?.__dataId) {
      await ocUpdateRule(editingRule.value.__dataId, payload);
    } else {
      await ocCreateRule(payload);
    }
    ruleDialog.value = false;
    editingRule.value = null;
    await loadAll();
    successLocal.value = t('operationCore.workspaceDefinitions.rules.saveSuccess');
  } catch (e: unknown) {
    errorLocal.value = panelError(
      e,
      editingRule.value
        ? 'operationCore.workspaceDefinitions.rules.updateError'
        : 'operationCore.workspaceDefinitions.forms.rulesCreateError',
    );
  } finally {
    saving.value = false;
  }
}

function openDelete(rule: OpRule) {
  deleteTarget.value = rule;
  deleteDialog.value = true;
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  deleting.value = true;
  errorLocal.value = null;
  try {
    await ocDeleteRule(deleteTarget.value.__dataId);
    deleteDialog.value = false;
    deleteTarget.value = null;
    await loadAll();
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.workspaceDefinitions.forms.rulesDeleteError');
  } finally {
    deleting.value = false;
  }
}
</script>

<template>
  <div class="oc-ws-rules-explorer pa-4 pa-md-5">
    <div class="oc-ws-rules-explorer__hero mb-5">
      <div class="d-flex flex-wrap align-start justify-space-between gap-4 mb-4">
        <div class="flex-grow-1 min-width-0" style="max-width: 720px">
          <h3 class="text-h6 font-weight-bold mb-2">
            {{ t('operationCore.workspaceDefinitions.rules.pageTitle') }}
          </h3>
          <p class="text-body-2 text-medium-emphasis mb-0">
            {{ t('operationCore.workspaceDefinitions.rules.pageSubtitle') }}
          </p>
        </div>
        <v-btn
          color="primary"
          rounded="lg"
          size="large"
          class="text-none flex-shrink-0"
          :disabled="!workspaceId || saving"
          @click="openCreate"
        >
          <v-icon icon="mdi-plus" start />
          {{ t('operationCore.workspaceDefinitions.rules.addRule') }}
        </v-btn>
      </div>

      <v-row dense>
        <v-col v-for="(step, idx) in howItWorksSteps" :key="idx" cols="12" md="4">
          <v-card variant="outlined" rounded="lg" class="h-100 oc-ws-rules-explorer__how-card">
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

    <div v-if="!loading && rules.length" class="d-flex flex-wrap ga-3 mb-4">
      <v-chip variant="tonal" color="primary" class="text-none">
        <v-icon icon="mdi-format-list-checks" start size="16" />
        {{ t('operationCore.workspaceDefinitions.rules.statsTotal', { count: rules.length }) }}
      </v-chip>
      <v-chip variant="tonal" color="success" class="text-none">
        <v-icon icon="mdi-check-circle-outline" start size="16" />
        {{ t('operationCore.workspaceDefinitions.rules.statsActive', { count: activeCount }) }}
      </v-chip>
    </div>

    <v-row class="oc-ws-rules-explorer__grid" dense>
      <v-col cols="12" md="4" lg="3">
        <v-card variant="outlined" rounded="lg" class="h-100">
          <v-card-title class="text-subtitle-1 font-weight-bold py-3 px-4">
            {{ t('operationCore.workspaceDefinitions.rules.triggerCatalogTitle') }}
          </v-card-title>
          <p class="text-caption text-medium-emphasis px-4 pb-2 mb-0">
            {{ t('operationCore.workspaceDefinitions.rules.triggerCatalogHint') }}
          </p>
          <v-divider />
          <v-list density="compact" nav class="py-1">
            <v-list-item
              v-for="item in triggerNavItems"
              :key="item.key"
              :active="selectedTrigger === item.key"
              rounded="lg"
              class="mx-2 my-1"
              @click="selectedTrigger = item.key"
            >
              <template #prepend>
                <v-icon :icon="item.icon" size="small" />
              </template>
              <v-list-item-title class="text-body-2">{{ item.label }}</v-list-item-title>
              <template #append>
                <v-chip size="x-small" variant="tonal">
                  {{ ruleCountForTrigger(item.key) }}
                </v-chip>
              </template>
            </v-list-item>
          </v-list>

          <v-divider class="my-2" />
          <div class="px-4 pb-4">
            <div class="text-caption font-weight-medium text-medium-emphasis mb-2">
              {{ t('operationCore.workspaceDefinitions.rules.filterPanelTitle') }}
            </div>
            <v-select
              v-model="filterTypeId"
              :items="[{ value: '', title: t('operationCore.workspaceDefinitions.rules.filterTypeAll') }, ...typeItems]"
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.rules.filterType')"
              density="compact"
              clearable
              hide-details
              variant="outlined"
              class="mb-3"
            />
            <v-select
              v-model="filterRuleType"
              :items="[
                { value: 'all', title: t('operationCore.workspaceDefinitions.rules.filterRuleTypeAll') },
                { value: 'validation', title: t('operationCore.workspaceDefinitions.rules.ruleTypeValidation') },
                { value: 'default', title: t('operationCore.workspaceDefinitions.rules.ruleTypeDefault') },
                { value: 'automation', title: t('operationCore.workspaceDefinitions.rules.ruleTypeAutomation') },
              ]"
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.rules.filterRuleType')"
              density="compact"
              hide-details
              variant="outlined"
              class="mb-3"
            />
            <v-switch
              v-model="activeOnly"
              color="primary"
              density="compact"
              hide-details
              :label="t('operationCore.workspaceDefinitions.rules.filterActiveOnly')"
            />
          </div>
        </v-card>
      </v-col>

      <v-col cols="12" md="8" lg="9">
        <v-card
          v-if="!loading && !filteredRules.length && !rules.length"
          variant="outlined"
          rounded="lg"
          class="oc-ws-rules-explorer__empty text-center pa-8 pa-md-12"
        >
          <v-avatar color="primary" variant="tonal" size="72" rounded="lg" class="mb-4">
            <v-icon icon="mdi-format-list-checks" size="36" />
          </v-avatar>
          <h4 class="text-h6 font-weight-bold mb-2">
            {{ t('operationCore.workspaceDefinitions.rules.emptyTitle') }}
          </h4>
          <p class="text-body-2 text-medium-emphasis mx-auto mb-6" style="max-width: 480px">
            {{ t('operationCore.workspaceDefinitions.rules.emptyBody') }}
          </p>
          <v-btn color="primary" rounded="lg" size="large" class="text-none" @click="openCreate">
            <v-icon icon="mdi-plus" start />
            {{ t('operationCore.workspaceDefinitions.rules.emptyCta') }}
          </v-btn>
        </v-card>

        <v-card v-else variant="outlined" rounded="lg" class="h-100">
          <v-card-title class="d-flex flex-wrap align-center gap-2 py-3 px-4">
            <span class="text-subtitle-1 font-weight-bold">
              {{ t('operationCore.workspaceDefinitions.rules.listTitle') }}
            </span>
            <v-spacer />
            <v-btn
              icon
              variant="text"
              size="small"
              :loading="loading"
              :disabled="!workspaceId"
              @click="void loadAll()"
            >
              <v-icon icon="mdi-refresh" />
            </v-btn>
          </v-card-title>
          <v-divider />

          <v-card-text class="pa-0">
            <v-alert
              v-if="!loading && rules.length && !filteredRules.length"
              type="info"
              variant="tonal"
              density="comfortable"
              class="ma-4 rounded-lg"
            >
              {{ t('operationCore.workspaceDefinitions.rules.filterNoResults') }}
            </v-alert>

            <v-data-table
              v-if="filteredRules.length"
              :headers="tableHeaders"
              :items="filteredRules"
              item-value="__dataId"
              density="comfortable"
              hide-default-footer
              class="oc-ws-rules-table"
              :row-props="({ item }) => ({ class: item.isActive === false ? 'oc-ws-rules-table__row--inactive' : '' })"
            >
              <template #[`item.name`]="{ item }">
                <div class="py-1">
                  <div class="text-body-2 font-weight-medium">{{ item.name }}</div>
                  <div
                    v-if="item.description"
                    class="text-caption text-medium-emphasis text-truncate"
                    style="max-width: 200px"
                  >
                    {{ item.description }}
                  </div>
                </div>
              </template>
              <template #[`item.ruleType`]="{ item }">
                <v-chip
                  size="small"
                  variant="tonal"
                  :color="ruleTypeChipColor(item.ruleType)"
                  class="text-none"
                >
                  {{ ruleTypeLabel(item.ruleType) }}
                </v-chip>
              </template>
              <template #[`item.priority`]="{ item }">
                <span class="text-body-2 tabular-nums">{{ item.priority ?? 100 }}</span>
              </template>
              <template #[`item.trigger`]="{ item }">
                <div class="d-flex align-center gap-1 text-body-2">
                  <v-icon icon="mdi-lightning-bolt-outline" size="16" class="text-medium-emphasis" />
                  {{ triggerLabel(item.trigger) }}
                </div>
              </template>
              <template #[`item.scope`]="{ item }">
                <span class="text-body-2">
                  {{
                    formatRuleScopeSummary(item, catalogContext) === '—'
                      ? t('operationCore.workspaceDefinitions.rules.scopeAny')
                      : formatRuleScopeSummary(item, catalogContext)
                  }}
                </span>
              </template>
              <template #[`item.when`]="{ item }">
                <span class="text-body-2">
                  {{
                    moRuleConditionsToClauses(item.conditions).length
                      ? formatRuleWhenSummary(item, catalogContext)
                      : t('operationCore.workspaceDefinitions.rules.whenAlwaysShort')
                  }}
                </span>
              </template>
              <template #[`item.then`]="{ item }">
                <span class="text-body-2">{{ formatRuleThenSummary(item, catalogContext) }}</span>
              </template>
              <template #[`item.isActive`]="{ item }">
                <v-chip
                  size="small"
                  variant="tonal"
                  :color="item.isActive !== false ? 'success' : 'default'"
                  class="text-none"
                >
                  <v-icon
                    :icon="item.isActive !== false ? 'mdi-check-circle-outline' : 'mdi-pause-circle-outline'"
                    start
                    size="14"
                  />
                  {{
                    item.isActive !== false
                      ? t('operationCore.workspaceDefinitions.rules.activeYes')
                      : t('operationCore.workspaceDefinitions.rules.activeNo')
                  }}
                </v-chip>
              </template>
              <template #[`item.actions`]="{ item }">
                <div class="d-flex justify-end ga-1">
                  <v-tooltip :text="t('operationCore.workspaceDefinitions.rules.editTooltip')" location="top">
                    <template #activator="{ props: tipProps }">
                      <v-btn
                        v-bind="tipProps"
                        icon="mdi-pencil-outline"
                        variant="text"
                        size="small"
                        @click="openEdit(item)"
                      />
                    </template>
                  </v-tooltip>
                  <v-tooltip :text="t('operationCore.workspaceDefinitions.rules.deleteTooltip')" location="top">
                    <template #activator="{ props: tipProps }">
                      <v-btn
                        v-bind="tipProps"
                        icon="mdi-delete-outline"
                        variant="text"
                        size="small"
                        color="error"
                        @click="openDelete(item)"
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

    <p v-if="!loading" class="text-caption text-medium-emphasis mt-4 mb-0">
      {{ t('operationCore.workspaceDefinitions.rules.technicalFootnote') }}
    </p>

    <OcWorkspaceRuleDialog
      v-model="ruleDialog"
      :rule="editingRule"
      :workspace-id="workspaceId"
      :condition-fields="conditionFields"
      :type-items="typeItems"
      :board-items="boardItems"
      :state-items="stateItems"
      :transition-items="transitionItems"
      :priority-items="priorityItems"
      :catalog-context="catalogContext"
      :saving="saving"
      @save="onSaveRule"
    />

    <v-dialog v-model="deleteDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="d-flex align-center gap-2 pt-5 px-5">
          <v-icon icon="mdi-alert-circle-outline" color="warning" />
          {{ t('operationCore.workspaceDefinitions.rules.deleteTitle') }}
        </v-card-title>
        <v-card-text class="px-5 pb-2">
          <p class="mb-2">{{ t('operationCore.workspaceDefinitions.rules.deleteBody') }}</p>
          <v-chip v-if="deleteTarget" variant="tonal" class="text-none font-weight-medium">
            {{ deleteTarget.name }}
          </v-chip>
        </v-card-text>
        <v-card-actions class="pa-4 px-5">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteDialog = false">
            {{ t('operationCore.definitions.cancel') }}
          </v-btn>
          <v-btn color="error" variant="flat" rounded="lg" class="text-none" :loading="deleting" @click="confirmDelete">
            {{ t('operationCore.definitions.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.oc-ws-rules-explorer__how-card {
  transition: border-color 0.15s ease;
}

.oc-ws-rules-explorer__how-card:hover {
  border-color: rgba(var(--v-theme-primary), 0.35);
}

.oc-ws-rules-explorer__empty {
  border-style: dashed;
}

:deep(.oc-ws-rules-table__row--inactive) {
  opacity: 0.55;
}
</style>
