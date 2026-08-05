<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useScenarioStudioApi } from '@/composables/useScenarioStudioApi';
import {
  createEmptyCondition,
  createScenarioDefinition,
  type ScenarioAuditEntry,
  type ScenarioBehavior,
  type ScenarioCatalogItem,
  type ScenarioCondition,
  type ScenarioDefinitionV2,
  type ScenarioEditorMode,
  type ScenarioPreviewResponse,
  type ScenarioSampleObservation,
  type ScenarioVersion,
} from '@/types/apps/scenario';
import {
  ALARM_RULE_GROUP_BY_OPTIONS,
  ALARM_RULE_MATCH_KEY_OPTIONS,
  operatorSymbol,
} from '@/composables/useAlarmRuleFormCatalog';

const { t } = useAppI18n();
const api = useScenarioStudioApi();

const editorMode = ref<ScenarioEditorMode>('wizard');
const wizardStep = ref(1);
const behavior = ref<ScenarioBehavior>('correlation');
const name = ref('');
const severity = ref(5);
const enabled = ref(false);
const definition = ref<ScenarioDefinitionV2>(createScenarioDefinition());
const current = ref<ScenarioVersion | null>(null);
const catalog = ref<ScenarioCatalogItem[]>([]);
const catalogSearch = ref('');
const lookupScenarioId = ref('');
const lookupVersion = ref<number | null>(null);
const actionMessage = ref('');
const localError = ref('');
const simulationJson = ref(JSON.stringify([
  {
    kind: 'event',
    key: 'login_failed',
    dimensions: { userId: 'admin', srcIp: '192.168.1.50' },
    timestamp: new Date().toISOString(),
  },
], null, 2));
const simulationResult = ref<ScenarioPreviewResponse | null>(null);
const auditEntries = ref<ScenarioAuditEntry[]>([]);
const showAudit = ref(false);
const catalogLoaded = ref(false);

const behaviorItems = computed(() => [
  { value: 'threshold', title: t('alarmCenter.scenarioStudio.behaviors.threshold'), icon: 'mdi-gauge' },
  { value: 'correlation', title: t('alarmCenter.scenarioStudio.behaviors.correlation'), icon: 'mdi-vector-link' },
  { value: 'staleness', title: t('alarmCenter.scenarioStudio.behaviors.staleness'), icon: 'mdi-clock-alert-outline' },
  { value: 'sequence', title: t('alarmCenter.scenarioStudio.behaviors.sequence'), icon: 'mdi-format-list-numbered' },
]);
const sourceKindItems = computed(() => [
  { value: 'observation', title: t('alarmCenter.scenarioStudio.sourceKinds.observation') },
  { value: 'scheduled-staleness', title: t('alarmCenter.scenarioStudio.sourceKinds.staleness') },
  { value: 'scheduled-query', title: t('alarmCenter.scenarioStudio.sourceKinds.query') },
  { value: 'meta-correlation', title: t('alarmCenter.scenarioStudio.sourceKinds.meta') },
]);
const logicItems = computed(() => [
  { value: 'and', title: t('alarmCenter.scenarioStudio.logic.and') },
  { value: 'or', title: t('alarmCenter.scenarioStudio.logic.or') },
  { value: 'not', title: t('alarmCenter.scenarioStudio.logic.not') },
]);
const operatorItems = ['eq', 'neq', 'gt', 'gte', 'lt', 'lte', 'contains', 'exists'];
const aggregationItems = ['count', 'sum', 'avg', 'min', 'max'];
const matchKeyItems = ALARM_RULE_MATCH_KEY_OPTIONS.map((item) => item.value);
const groupByItems = ALARM_RULE_GROUP_BY_OPTIONS.map((item) => item.value);
const wizardSteps = computed(() => [
  t('alarmCenter.scenarioStudio.steps.source'),
  t('alarmCenter.scenarioStudio.steps.condition'),
  t('alarmCenter.scenarioStudio.steps.time'),
  t('alarmCenter.scenarioStudio.steps.grouping'),
  t('alarmCenter.scenarioStudio.steps.severity'),
  t('alarmCenter.scenarioStudio.steps.noise'),
  t('alarmCenter.scenarioStudio.steps.test'),
  t('alarmCenter.scenarioStudio.steps.publish'),
]);
const userScenarios = computed(() => filterCatalog('user'));
const productTemplates = computed(() => filterCatalog('product'));
const catalogScenarioIds = computed(() =>
  catalog.value.filter((item) => item.origin === 'user').map((item) => item.scenarioId),
);
const scheduleEditor = computed(() => definition.value.source.scheduleDefinition ?? {
  expression: '',
  timeZone: 'UTC',
  maxLookbackSeconds: 3600,
});

const rootCondition = computed(() => {
  const condition = definition.value.condition;
  return condition?.logic ? condition : null;
});
const leafCondition = computed(() => {
  const condition = definition.value.condition;
  return condition && !condition.logic ? condition : null;
});
const wizardCondition = computed(() => definition.value.condition ?? createEmptyCondition());
const wizardAggregation = computed(() =>
  definition.value.aggregation ?? { function: 'count', operator: 'gte', threshold: 1 },
);
const simpleCompatible = computed(() => {
  const d = definition.value;
  const root = d.condition;
  const conditionSimple = !root || (!root.logic && root.children.length === 0);
  return d.schemaVersion === 2
    && conditionSimple
    && !d.source.query
    && !d.source.schedule
    && !d.source.scheduleDefinition
    && d.source.dependsOnScenarioIds.length === 0
    && !d.hysteresis
    && (d.sequence?.steps ?? []).every((step) => !step.condition);
});
const canonicalJson = computed(() => JSON.stringify(definition.value, null, 2));
const humanSummary = computed(() => {
  const d = definition.value;
  const key = d.source.matchKey || t('alarmCenter.scenarioStudio.summary.anyKey');
  const group = d.groupBy.length
    ? d.groupBy.join(' + ')
    : t('alarmCenter.scenarioStudio.summary.allEvents');
  const cooldown = Math.round(d.dedup.cooldownSeconds / 60);
  if (d.sequence?.steps.length) {
    return t('alarmCenter.scenarioStudio.summary.sequence', {
      steps: d.sequence.steps.map((step) => `${step.minCount}× ${step.matchKey}`).join(' → '),
      group,
      severity: String(severity.value),
      cooldown: String(cooldown),
    });
  }
  if (d.source.kind === 'scheduled-staleness' || (d.window?.stalenessSeconds ?? 0) > 0) {
    return t('alarmCenter.scenarioStudio.summary.staleness', {
      key,
      minutes: String(Math.round((d.window?.stalenessSeconds ?? 0) / 60)),
      severity: String(severity.value),
      cooldown: String(cooldown),
    });
  }
  if (d.aggregation) {
    return t('alarmCenter.scenarioStudio.summary.aggregation', {
      key,
      function: d.aggregation.function,
      operator: operatorSymbol(d.aggregation.operator),
      threshold: String(d.aggregation.threshold),
      minutes: String(Math.round((d.window?.durationSeconds ?? 0) / 60)),
      group,
      severity: String(severity.value),
    });
  }
  const condition = leafCondition.value;
  return t('alarmCenter.scenarioStudio.summary.condition', {
    key,
    field: condition?.field || 'value',
    operator: operatorSymbol(condition?.operator || 'gte'),
    value: String(condition?.value ?? ''),
    severity: String(severity.value),
  });
});
const canEditDraft = computed(() =>
  !current.value || (current.value.status === 'draft' && !current.value.isReadOnly),
);
const editorLocked = computed(() => current.value != null && !canEditDraft.value);
const canPublish = computed(() =>
  current.value?.status === 'validated'
  && current.value.validation?.isValid === true
  && !current.value.isReadOnly,
);

function setBehavior(next: ScenarioBehavior) {
  behavior.value = next;
  definition.value = createScenarioDefinition(next);
}

function filterCatalog(origin: 'user' | 'product') {
  const query = catalogSearch.value.trim().toLocaleLowerCase();
  return catalog.value.filter((item) => {
    if (item.origin !== origin) return false;
    if (!query) return true;
    return [
      item.name,
      item.scenarioId,
      item.templateId,
      item.packageId,
      item.packageVersion,
    ].some((value) => value?.toLocaleLowerCase().includes(query));
  });
}

function statusColor(status: string): string {
  if (status === 'published') return 'success';
  if (status === 'validated') return 'info';
  if (status === 'archived') return 'default';
  return 'warning';
}

function inferBehavior(value: ScenarioDefinitionV2): ScenarioBehavior {
  if (value.sequence?.steps.length) return 'sequence';
  if (value.source.kind === 'scheduled-staleness' || (value.window?.stalenessSeconds ?? 0) > 0) return 'staleness';
  if (value.aggregation) return 'correlation';
  return 'threshold';
}

function selectBehavior(next: string) {
  setBehavior(next as ScenarioBehavior);
}

function setMode(next: ScenarioEditorMode) {
  if (next === 'wizard' && !simpleCompatible.value) {
    localError.value = t('alarmCenter.scenarioStudio.errors.notSimple');
    return;
  }
  editorMode.value = next;
  localError.value = '';
}

function ensureConditionRoot() {
  if (!definition.value.condition) definition.value.condition = createEmptyCondition();
}

function ensureConditionGroup() {
  const existing = definition.value.condition;
  if (existing?.logic) return;
  definition.value.condition = {
    logic: 'and',
    children: existing ? [existing] : [createEmptyCondition()],
    sustainedForSeconds: 0,
  };
}

function setConditionLogic(logic: 'and' | 'or' | 'not') {
  ensureConditionGroup();
  definition.value.condition!.logic = logic;
  if (logic === 'not' && definition.value.condition!.children.length > 1) {
    definition.value.condition!.children = [definition.value.condition!.children[0]];
  }
}

function selectConditionLogic(logic: string) {
  setConditionLogic(logic as 'and' | 'or' | 'not');
}

function addCondition() {
  ensureConditionGroup();
  definition.value.condition!.children.push(createEmptyCondition());
}

function removeCondition(index: number) {
  definition.value.condition?.children.splice(index, 1);
}

function ensureAggregation() {
  definition.value.aggregation ??= { function: 'count', operator: 'gte', threshold: 1 };
  definition.value.window ??= { durationSeconds: 300, stalenessSeconds: 0 };
}

function removeAggregation() {
  definition.value.aggregation = undefined;
}

function ensureWindow() {
  definition.value.window ??= { durationSeconds: 300, stalenessSeconds: 0 };
}

function ensureScheduleDefinition() {
  definition.value.source.scheduleDefinition ??= {
    expression: '0 */5 * * * *',
    timeZone: 'UTC',
    maxLookbackSeconds: 3600,
  };
}

function updateSourceKind(value: unknown) {
  definition.value.source.kind = String(value) as ScenarioDefinitionV2['source']['kind'];
  definition.value.source.query = undefined;
  definition.value.source.schedule = undefined;
  if (definition.value.source.kind === 'scheduled-query') ensureScheduleDefinition();
  if (definition.value.source.kind !== 'scheduled-query') {
    definition.value.source.scheduleDefinition = undefined;
  }
  if (definition.value.source.kind !== 'meta-correlation') {
    definition.value.source.dependsOnScenarioIds = [];
  }
}

function ensureHysteresis() {
  definition.value.hysteresis ??= {
    raiseThreshold: definition.value.aggregation?.threshold ?? 90,
    clearThreshold: Math.max(0, (definition.value.aggregation?.threshold ?? 90) - 10),
    minimumStateSeconds: 300,
  };
}

function removeHysteresis() {
  definition.value.hysteresis = undefined;
}

function updateWindowMinutes(value: unknown) {
  ensureWindow();
  definition.value.window!.durationSeconds = Number(value) * 60;
}

function updateStalenessMinutes(value: unknown) {
  ensureWindow();
  definition.value.window!.stalenessSeconds = Number(value) * 60;
}

function updateStalenessSeconds(value: unknown) {
  ensureWindow();
  definition.value.window!.stalenessSeconds = Number(value);
}

function updateSustainedMinutes(value: unknown) {
  ensureConditionRoot();
  definition.value.condition!.sustainedForSeconds = Number(value) * 60;
}

function ensureSequence() {
  definition.value.sequence ??= { steps: [] };
  if (!definition.value.sequence.steps.length) addSequenceStep();
}

function addSequenceStep() {
  definition.value.sequence ??= { steps: [] };
  definition.value.sequence.steps.push({
    matchKey: '',
    minCount: 1,
    withinSeconds: 300,
  });
}

function removeSequenceStep(index: number) {
  definition.value.sequence?.steps.splice(index, 1);
  if (!definition.value.sequence?.steps.length) definition.value.sequence = undefined;
}

function setMetadata(raw: string) {
  const next: Record<string, string> = {};
  for (const line of raw.split('\n')) {
    const separator = line.indexOf('=');
    if (separator < 1) continue;
    next[line.slice(0, separator).trim()] = line.slice(separator + 1).trim();
  }
  definition.value.metadata = next;
}

const metadataText = computed({
  get: () => Object.entries(definition.value.metadata).map(([key, value]) => `${key}=${value}`).join('\n'),
  set: setMetadata,
});

function loadVersion(item: ScenarioVersion) {
  current.value = item;
  name.value = item.name;
  severity.value = item.severity;
  enabled.value = item.enabled;
  definition.value = structuredClone(item.definition);
  definition.value.source.dependsOnScenarioIds ??= [];
  definition.value.source.maxChainDepth ??= 5;
  if (definition.value.source.kind === 'scheduled-query') ensureScheduleDefinition();
  definition.value.metadata ??= {};
  behavior.value = inferBehavior(item.definition);
  lookupScenarioId.value = item.scenarioId;
  lookupVersion.value = item.version;
  editorMode.value = simpleCompatible.value ? 'wizard' : 'advanced';
}

function resetNewScenario() {
  current.value = null;
  name.value = '';
  severity.value = 5;
  enabled.value = false;
  behavior.value = 'correlation';
  definition.value = createScenarioDefinition();
  lookupScenarioId.value = '';
  lookupVersion.value = null;
  editorMode.value = 'wizard';
  actionMessage.value = '';
}

function requestBody() {
  return {
    name: name.value.trim(),
    severity: severity.value,
    enabled: enabled.value,
    definition: definition.value,
  };
}

function validateLocal(): boolean {
  localError.value = '';
  if (!name.value.trim()) localError.value = t('alarmCenter.scenarioStudio.errors.name');
  else if (!definition.value.source.matchKey.trim()) localError.value = t('alarmCenter.scenarioStudio.errors.matchKey');
  else if (severity.value < 1 || severity.value > 10) localError.value = t('alarmCenter.scenarioStudio.errors.severity');
  return !localError.value;
}

async function runAction(action: () => Promise<void>, successKey: string) {
  actionMessage.value = '';
  localError.value = '';
  try {
    await action();
    actionMessage.value = t(successKey);
  } catch (cause: any) {
    if (!api.error.value) {
      localError.value = cause?.message || t('alarmCenter.scenarioStudio.errors.request');
    }
  }
}

async function saveDraft() {
  if (!validateLocal()) return;
  await runAction(async () => {
    const saved = current.value
      ? await api.updateDraft(current.value.scenarioId, current.value.version, requestBody())
      : await api.createDraft(requestBody());
    loadVersion(saved);
    catalog.value = await api.listScenarios(true);
  }, 'alarmCenter.scenarioStudio.messages.saved');
}

async function loadScenario() {
  if (!lookupScenarioId.value.trim()) return;
  await runAction(async () => {
    loadVersion(await api.getScenario(lookupScenarioId.value.trim(), lookupVersion.value ?? undefined));
  }, 'alarmCenter.scenarioStudio.messages.loaded');
}

async function refreshCatalog(showSuccess = true) {
  await runAction(async () => {
    catalog.value = await api.listScenarios(true);
    catalogLoaded.value = true;
  }, showSuccess
    ? 'alarmCenter.scenarioStudio.messages.catalogRefreshed'
    : 'alarmCenter.scenarioStudio.messages.catalogLoaded');
  if (!showSuccess) actionMessage.value = '';
}

async function selectCatalogItem(item: ScenarioCatalogItem) {
  const version = item.origin === 'product'
    ? item.latestVersion
    : item.draftVersion ?? item.publishedVersion ?? item.latestVersion;
  await selectCatalogVersion(item, version);
}

async function selectCatalogVersion(item: ScenarioCatalogItem, version?: number) {
  if (version == null) return;
  lookupScenarioId.value = item.scenarioId;
  lookupVersion.value = version;
  await loadScenario();
}

async function cloneTemplate(item: ScenarioCatalogItem) {
  await runAction(async () => {
    const draft = await api.cloneTemplateToDraft(item.scenarioId, item.latestVersion);
    loadVersion(draft);
    catalog.value = await api.listScenarios(true);
  }, 'alarmCenter.scenarioStudio.messages.cloned');
}

async function cloneCurrentTemplate() {
  if (!current.value?.isReadOnly) return;
  await runAction(async () => {
    const draft = await api.cloneTemplateToDraft(current.value!.scenarioId, current.value!.version);
    loadVersion(draft);
    catalog.value = await api.listScenarios(true);
  }, 'alarmCenter.scenarioStudio.messages.cloned');
}

async function createNextDraft() {
  if (!current.value) return;
  await runAction(async () => {
    loadVersion(await api.createNextDraft(current.value!.scenarioId));
    catalog.value = await api.listScenarios(true);
  }, 'alarmCenter.scenarioStudio.messages.nextDraft');
}

async function validateDraft() {
  if (!current.value || current.value.isReadOnly) return;
  await runAction(async () => {
    const validation = await api.validate(current.value!.scenarioId, current.value!.version);
    current.value = { ...current.value!, status: validation.isValid ? 'validated' : 'draft', validation };
    catalog.value = await api.listScenarios(true);
  }, 'alarmCenter.scenarioStudio.messages.validated');
}

async function publishDraft() {
  if (!current.value || current.value.isReadOnly) return;
  await runAction(async () => {
    loadVersion(await api.publish(current.value!.scenarioId, current.value!.version));
    catalog.value = await api.listScenarios(true);
  },
    'alarmCenter.scenarioStudio.messages.published');
}

async function archiveVersion() {
  if (!current.value || current.value.isReadOnly) return;
  await runAction(async () => {
    loadVersion(await api.archive(current.value!.scenarioId, current.value!.version));
    catalog.value = await api.listScenarios(true);
  },
    'alarmCenter.scenarioStudio.messages.archived');
}

async function rollbackVersion() {
  if (!current.value || current.value.isReadOnly) return;
  await runAction(async () => {
    loadVersion(await api.rollback(current.value!.scenarioId, current.value!.version));
    catalog.value = await api.listScenarios(true);
  },
    'alarmCenter.scenarioStudio.messages.rolledBack');
}

async function runDefinitionCheck(kind: 'compile' | 'preview') {
  await runAction(async () => {
    simulationResult.value = await api[kind]({ definition: definition.value });
  }, kind === 'compile'
    ? 'alarmCenter.scenarioStudio.messages.compiled'
    : 'alarmCenter.scenarioStudio.messages.previewed');
}

async function simulate() {
  await runAction(async () => {
    const parsed = JSON.parse(simulationJson.value) as ScenarioSampleObservation[];
    if (!Array.isArray(parsed)) throw new Error(t('alarmCenter.scenarioStudio.errors.samplesArray'));
    simulationResult.value = await api.simulate({
      definition: definition.value,
      samples: parsed,
    });
  }, 'alarmCenter.scenarioStudio.messages.simulated');
}

async function loadAudit() {
  if (!current.value) return;
  await runAction(async () => {
    auditEntries.value = await api.audit(current.value!.scenarioId);
    showAudit.value = true;
  }, 'alarmCenter.scenarioStudio.messages.auditLoaded');
}

onMounted(() => {
  void refreshCatalog(false);
});
</script>

<template>
  <div class="scenario-studio">
    <v-alert v-if="localError || api.error.value" type="error" variant="tonal" closable class="mb-4"
      @click:close="localError = ''; api.clearError()">
      {{ localError || api.error.value }}
    </v-alert>
    <v-alert v-if="actionMessage" type="success" variant="tonal" closable class="mb-4"
      @click:close="actionMessage = ''">
      {{ actionMessage }}
    </v-alert>

    <v-card variant="outlined" class="rounded-lg mb-4">
      <v-card-title class="d-flex align-center flex-wrap gap-2">
        <div>
          <div>{{ t('alarmCenter.scenarioStudio.catalog.title') }}</div>
          <div class="text-caption text-medium-emphasis font-weight-regular">
            {{ t('alarmCenter.scenarioStudio.catalog.subtitle') }}
          </div>
        </div>
        <v-spacer />
        <v-btn variant="text" prepend-icon="mdi-plus" @click="resetNewScenario">
          {{ t('alarmCenter.scenarioStudio.catalog.newScenario') }}
        </v-btn>
        <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="api.pending.value" @click="refreshCatalog()">
          {{ t('alarmCenter.scenarioStudio.catalog.refresh') }}
        </v-btn>
      </v-card-title>
      <v-card-text>
        <v-text-field v-model="catalogSearch" prepend-inner-icon="mdi-magnify"
          :label="t('alarmCenter.scenarioStudio.catalog.search')" variant="outlined"
          density="compact" clearable hide-details class="mb-4" />
        <v-row>
          <v-col cols="12" lg="6">
            <div class="d-flex align-center gap-2 mb-2">
              <v-icon icon="mdi-account-edit-outline" color="primary" />
              <div class="text-subtitle-1 font-weight-bold">
                {{ t('alarmCenter.scenarioStudio.catalog.userTitle') }}
              </div>
              <v-chip size="x-small" variant="tonal">{{ userScenarios.length }}</v-chip>
            </div>
            <v-list border rounded="lg" lines="three" class="studio-catalog-list">
              <v-list-item v-for="item in userScenarios" :key="item.scenarioId"
                :active="current?.scenarioId === item.scenarioId"
                prepend-icon="mdi-shield-edit-outline" @click="selectCatalogItem(item)">
                <v-list-item-title class="font-weight-medium">{{ item.name }}</v-list-item-title>
                <v-list-item-subtitle>
                  {{ item.scenarioId }} · v{{ item.latestVersion }}
                </v-list-item-subtitle>
                <div class="d-flex flex-wrap gap-1 mt-1">
                  <v-chip size="x-small" color="primary" variant="tonal">
                    {{ t('alarmCenter.scenarioStudio.catalog.userBadge') }}
                  </v-chip>
                  <v-chip size="x-small" :color="statusColor(item.latestStatus)" variant="tonal">
                    {{ item.latestStatus }}
                  </v-chip>
                  <v-chip v-if="item.draftVersion" size="x-small" variant="outlined"
                    @click.stop="selectCatalogVersion(item, item.draftVersion)">
                    {{ t('alarmCenter.scenarioStudio.catalog.draftVersion', { version: String(item.draftVersion) }) }}
                  </v-chip>
                  <v-chip v-if="item.publishedVersion" size="x-small" color="success" variant="outlined"
                    @click.stop="selectCatalogVersion(item, item.publishedVersion)">
                    {{ t('alarmCenter.scenarioStudio.catalog.publishedVersion', { version: String(item.publishedVersion) }) }}
                  </v-chip>
                </div>
                <template #append>
                  <v-btn size="small" variant="text" @click.stop="selectCatalogItem(item)">
                    {{ t('alarmCenter.scenarioStudio.load') }}
                  </v-btn>
                </template>
              </v-list-item>
              <v-list-item v-if="catalogLoaded && !userScenarios.length"
                prepend-icon="mdi-information-outline"
                :title="t('alarmCenter.scenarioStudio.catalog.userEmpty')" />
            </v-list>
          </v-col>

          <v-col cols="12" lg="6">
            <div class="d-flex align-center gap-2 mb-2">
              <v-icon icon="mdi-package-variant-closed" color="secondary" />
              <div class="text-subtitle-1 font-weight-bold">
                {{ t('alarmCenter.scenarioStudio.catalog.templateTitle') }}
              </div>
              <v-chip size="x-small" variant="tonal">{{ productTemplates.length }}</v-chip>
            </div>
            <v-list border rounded="lg" lines="three" class="studio-catalog-list">
              <v-list-item v-for="item in productTemplates" :key="item.scenarioId"
                :active="current?.scenarioId === item.scenarioId"
                prepend-icon="mdi-file-lock-outline" @click="selectCatalogItem(item)">
                <v-list-item-title class="font-weight-medium">{{ item.name }}</v-list-item-title>
                <v-list-item-subtitle>
                  {{ item.templateId || item.scenarioId }} · {{ item.packageId }}@{{ item.packageVersion }}
                </v-list-item-subtitle>
                <div class="d-flex flex-wrap gap-1 mt-1">
                  <v-chip size="x-small" color="secondary" variant="tonal">
                    {{ t('alarmCenter.scenarioStudio.catalog.productBadge') }}
                  </v-chip>
                  <v-chip size="x-small" prepend-icon="mdi-lock" variant="outlined">
                    {{ t('alarmCenter.scenarioStudio.catalog.readOnly') }}
                  </v-chip>
                  <v-chip size="x-small" :color="statusColor(item.latestStatus)" variant="tonal">
                    {{ item.latestStatus }}
                  </v-chip>
                </div>
                <template #append>
                  <div class="d-flex flex-column gap-1">
                    <v-btn size="small" variant="text" @click.stop="selectCatalogItem(item)">
                      {{ t('alarmCenter.scenarioStudio.catalog.inspect') }}
                    </v-btn>
                    <v-btn size="small" color="primary" variant="tonal"
                      @click.stop="cloneTemplate(item)">
                      {{ t('alarmCenter.scenarioStudio.catalog.clone') }}
                    </v-btn>
                  </div>
                </template>
              </v-list-item>
              <v-list-item v-if="catalogLoaded && !productTemplates.length"
                prepend-icon="mdi-information-outline"
                :title="t('alarmCenter.scenarioStudio.catalog.templateEmpty')" />
            </v-list>
            <v-alert type="info" variant="tonal" density="compact" class="mt-2">
              {{ t('alarmCenter.scenarioStudio.catalog.templateHint') }}
            </v-alert>
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <v-card variant="outlined" class="rounded-lg mb-4">
      <v-card-text>
        <v-row dense align="center">
          <v-col cols="12" md="4">
            <v-text-field v-model="lookupScenarioId" :label="t('alarmCenter.scenarioStudio.lookupId')"
              variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col cols="6" md="2">
            <v-text-field v-model.number="lookupVersion" type="number"
              :label="t('alarmCenter.scenarioStudio.lookupVersion')" variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col cols="6" md="2">
            <v-btn block variant="tonal" :loading="api.pending.value" @click="loadScenario">
              {{ t('alarmCenter.scenarioStudio.load') }}
            </v-btn>
          </v-col>
          <v-col cols="12" md="4" class="d-flex justify-md-end gap-2">
            <v-chip v-if="current" color="primary" variant="tonal">
              v{{ current.version }} · {{ current.status }}
            </v-chip>
            <v-chip v-if="current?.origin === 'product'" color="secondary" variant="tonal">
              {{ t('alarmCenter.scenarioStudio.catalog.productBadge') }}
            </v-chip>
            <v-btn v-if="current?.isReadOnly" color="primary" variant="tonal"
              @click="cloneCurrentTemplate">
              {{ t('alarmCenter.scenarioStudio.catalog.clone') }}
            </v-btn>
            <v-btn v-if="current && !current.isReadOnly && current.status !== 'draft'"
              variant="outlined" @click="createNextDraft">
              {{ t('alarmCenter.scenarioStudio.newVersion') }}
            </v-btn>
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <v-alert v-if="current?.isReadOnly" type="info" variant="tonal" class="mb-4" icon="mdi-lock-outline">
      {{ t('alarmCenter.scenarioStudio.catalog.readOnlyHint') }}
    </v-alert>

    <v-card variant="outlined" class="rounded-lg mb-4">
      <v-card-text>
        <v-row dense>
          <v-col cols="12" md="7">
            <v-text-field v-model="name" :label="t('alarmCenter.scenarioStudio.name')"
              :disabled="!canEditDraft" variant="outlined" hide-details="auto" />
          </v-col>
          <v-col cols="6" md="3">
            <v-text-field v-model.number="severity" type="number" min="1" max="10"
              :label="t('alarmCenter.scenarioStudio.severity')" :disabled="!canEditDraft"
              variant="outlined" hide-details="auto" />
          </v-col>
          <v-col cols="6" md="2">
            <v-switch v-model="enabled" :label="t('alarmCenter.scenarioStudio.enabled')"
              :disabled="!canEditDraft" color="primary" hide-details />
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <v-alert type="info" variant="tonal" class="mb-4" icon="mdi-text-box-check-outline">
      <div class="text-caption text-medium-emphasis">{{ t('alarmCenter.scenarioStudio.humanSummary') }}</div>
      <div class="font-weight-medium">{{ humanSummary }}</div>
    </v-alert>

    <v-tabs :model-value="editorMode" color="primary" class="mb-3">
      <v-tab value="wizard" @click="setMode('wizard')">
        {{ t('alarmCenter.scenarioStudio.modeWizard') }}
      </v-tab>
      <v-tab value="advanced" @click="setMode('advanced')">
        {{ t('alarmCenter.scenarioStudio.modeAdvanced') }}
      </v-tab>
    </v-tabs>

    <v-card v-if="editorMode === 'wizard'" variant="outlined" class="rounded-lg mb-4"
      :class="{ 'studio-editor--locked': editorLocked }">
      <v-card-text>
        <div class="d-flex flex-wrap gap-2 mb-5">
          <v-chip v-for="(step, index) in wizardSteps" :key="step"
            :color="wizardStep === index + 1 ? 'primary' : undefined"
            :variant="wizardStep === index + 1 ? 'flat' : 'outlined'"
            @click="wizardStep = index + 1">
            {{ index + 1 }}. {{ step }}
          </v-chip>
        </div>

        <v-window v-model="wizardStep">
          <v-window-item :value="1">
            <div class="text-h6 mb-1">{{ t('alarmCenter.scenarioStudio.steps.source') }}</div>
            <div class="text-body-2 text-medium-emphasis mb-4">{{ t('alarmCenter.scenarioStudio.help.source') }}</div>
            <v-row dense>
              <v-col v-for="item in behaviorItems" :key="item.value" cols="12" sm="6" md="3">
                <v-card variant="outlined" class="h-100 studio-choice"
                  :class="{ 'studio-choice--active': behavior === item.value }"
                  @click="selectBehavior(item.value)">
                  <v-card-text>
                    <v-icon :icon="item.icon" color="primary" class="mb-2" />
                    <div class="font-weight-bold">{{ item.title }}</div>
                  </v-card-text>
                </v-card>
              </v-col>
              <v-col cols="12" md="6">
                <v-combobox v-model="definition.source.matchKey" :items="matchKeyItems"
                  :label="t('alarmCenter.scenarioStudio.matchKey')" variant="outlined" />
              </v-col>
              <v-col cols="12" md="6">
                <v-select v-model="definition.source.observationKind"
                  :items="['event', 'metric']" :label="t('alarmCenter.scenarioStudio.observationKind')"
                  variant="outlined" />
              </v-col>
            </v-row>
          </v-window-item>

          <v-window-item :value="2">
            <div class="text-h6 mb-1">{{ t('alarmCenter.scenarioStudio.steps.condition') }}</div>
            <div class="text-body-2 text-medium-emphasis mb-4">{{ t('alarmCenter.scenarioStudio.help.condition') }}</div>
            <v-row v-if="behavior === 'threshold'" dense>
              <v-col cols="12" md="4">
                <v-text-field v-model="wizardCondition.field"
                  :label="t('alarmCenter.scenarioStudio.field')" variant="outlined" />
              </v-col>
              <v-col cols="6" md="4">
                <v-select v-model="wizardCondition.operator" :items="operatorItems"
                  :label="t('alarmCenter.scenarioStudio.operator')" variant="outlined" />
              </v-col>
              <v-col cols="6" md="4">
                <v-text-field v-model="wizardCondition.value"
                  :label="t('alarmCenter.scenarioStudio.value')" variant="outlined" />
              </v-col>
            </v-row>
            <v-row v-else-if="behavior === 'correlation'" dense>
              <v-col cols="6">
                <v-select v-model="wizardAggregation.function" :items="aggregationItems"
                  :label="t('alarmCenter.scenarioStudio.aggregation')" variant="outlined" />
              </v-col>
              <v-col cols="6">
                <v-text-field v-model.number="wizardAggregation.threshold" type="number" min="1"
                  :label="t('alarmCenter.scenarioStudio.count')" variant="outlined" />
              </v-col>
            </v-row>
            <div v-else-if="behavior === 'sequence'">
              <v-card v-for="(step, index) in definition.sequence?.steps || []" :key="index"
                variant="tonal" class="mb-3">
                <v-card-text>
                  <v-row dense align="center">
                    <v-col cols="12" md="5">
                      <v-combobox v-model="step.matchKey" :items="matchKeyItems"
                        :label="`${t('alarmCenter.scenarioStudio.sequenceStep')} ${index + 1}`"
                        variant="outlined" hide-details />
                    </v-col>
                    <v-col cols="5" md="3">
                      <v-text-field v-model.number="step.minCount" type="number" min="1"
                        :label="t('alarmCenter.scenarioStudio.count')" variant="outlined" hide-details />
                    </v-col>
                    <v-col cols="5" md="3">
                      <v-text-field :model-value="step.withinSeconds / 60" type="number" min="1"
                        :label="t('alarmCenter.scenarioStudio.windowMinutes')" variant="outlined" hide-details
                        @update:model-value="step.withinSeconds = Number($event) * 60" />
                    </v-col>
                    <v-col cols="2" md="1">
                      <v-btn icon="mdi-delete-outline" variant="text" color="error"
                        @click="removeSequenceStep(index)" />
                    </v-col>
                  </v-row>
                </v-card-text>
              </v-card>
              <v-btn variant="tonal" prepend-icon="mdi-plus" @click="addSequenceStep">
                {{ t('alarmCenter.scenarioStudio.addStep') }}
              </v-btn>
            </div>
            <v-alert v-else type="info" variant="tonal">
              {{ t('alarmCenter.scenarioStudio.help.stalenessCondition') }}
            </v-alert>
          </v-window-item>

          <v-window-item :value="3">
            <div class="text-h6 mb-1">{{ t('alarmCenter.scenarioStudio.steps.time') }}</div>
            <div class="text-body-2 text-medium-emphasis mb-4">{{ t('alarmCenter.scenarioStudio.help.time') }}</div>
            <v-row dense>
              <v-col v-if="definition.window" cols="12" md="6">
                <v-text-field :model-value="definition.window.durationSeconds / 60"
                  type="number" min="1" :label="t('alarmCenter.scenarioStudio.windowMinutes')"
                  variant="outlined" @update:model-value="updateWindowMinutes" />
              </v-col>
              <v-col v-if="behavior === 'staleness' && definition.window" cols="12" md="6">
                <v-text-field :model-value="definition.window.stalenessSeconds / 60"
                  type="number" min="1" :label="t('alarmCenter.scenarioStudio.stalenessMinutes')"
                  variant="outlined" @update:model-value="updateStalenessMinutes" />
              </v-col>
              <v-col v-if="definition.condition" cols="12" md="6">
                <v-text-field :model-value="definition.condition.sustainedForSeconds / 60"
                  type="number" min="0" :label="t('alarmCenter.scenarioStudio.sustainedMinutes')"
                  variant="outlined" @update:model-value="updateSustainedMinutes" />
              </v-col>
            </v-row>
          </v-window-item>

          <v-window-item :value="4">
            <div class="text-h6 mb-1">{{ t('alarmCenter.scenarioStudio.steps.grouping') }}</div>
            <div class="text-body-2 text-medium-emphasis mb-4">{{ t('alarmCenter.scenarioStudio.help.grouping') }}</div>
            <v-combobox v-model="definition.groupBy" :items="groupByItems" multiple chips closable-chips
              :label="t('alarmCenter.scenarioStudio.groupBy')" variant="outlined" />
          </v-window-item>

          <v-window-item :value="5">
            <div class="text-h6 mb-1">{{ t('alarmCenter.scenarioStudio.steps.severity') }}</div>
            <div class="text-body-2 text-medium-emphasis mb-4">{{ t('alarmCenter.scenarioStudio.help.severity') }}</div>
            <v-slider v-model="severity" min="1" max="10" step="1" show-ticks="always"
              thumb-label="always" color="primary" />
          </v-window-item>

          <v-window-item :value="6">
            <div class="text-h6 mb-1">{{ t('alarmCenter.scenarioStudio.steps.noise') }}</div>
            <div class="text-body-2 text-medium-emphasis mb-4">{{ t('alarmCenter.scenarioStudio.help.noise') }}</div>
            <v-row dense>
              <v-col cols="12" md="7">
                <v-text-field v-model="definition.dedup.keyTemplate"
                  :label="t('alarmCenter.scenarioStudio.dedupTemplate')" variant="outlined" />
              </v-col>
              <v-col cols="12" md="5">
                <v-text-field :model-value="definition.dedup.cooldownSeconds / 60"
                  type="number" min="0" :label="t('alarmCenter.scenarioStudio.cooldownMinutes')"
                  variant="outlined" @update:model-value="definition.dedup.cooldownSeconds = Number($event) * 60" />
              </v-col>
            </v-row>
          </v-window-item>

          <v-window-item :value="7">
            <div class="text-h6 mb-1">{{ t('alarmCenter.scenarioStudio.steps.test') }}</div>
            <div class="text-body-2 text-medium-emphasis mb-4">{{ t('alarmCenter.scenarioStudio.help.test') }}</div>
            <v-btn color="primary" variant="tonal" prepend-icon="mdi-flask-outline" @click="simulate">
              {{ t('alarmCenter.scenarioStudio.simulate') }}
            </v-btn>
          </v-window-item>

          <v-window-item :value="8">
            <div class="text-h6 mb-1">{{ t('alarmCenter.scenarioStudio.steps.publish') }}</div>
            <div class="text-body-2 text-medium-emphasis mb-4">{{ t('alarmCenter.scenarioStudio.help.publish') }}</div>
            <v-switch v-model="enabled" color="primary" :label="t('alarmCenter.scenarioStudio.enabled')" />
            <v-btn color="primary" @click="saveDraft">{{ t('alarmCenter.scenarioStudio.saveDraft') }}</v-btn>
          </v-window-item>
        </v-window>

        <div class="d-flex justify-space-between mt-5">
          <v-btn variant="text" :disabled="wizardStep === 1" @click="wizardStep--">
            {{ t('alarmCenter.scenarioStudio.previous') }}
          </v-btn>
          <v-btn variant="tonal" :disabled="wizardStep === 8" @click="wizardStep++">
            {{ t('alarmCenter.scenarioStudio.next') }}
          </v-btn>
        </div>
      </v-card-text>
    </v-card>

    <div v-else :class="{ 'studio-editor--locked': editorLocked }">
      <v-alert v-if="!simpleCompatible" type="warning" variant="tonal" class="mb-4">
        {{ t('alarmCenter.scenarioStudio.advancedOnly') }}
      </v-alert>

      <v-card variant="outlined" class="rounded-lg mb-4">
        <v-card-title>{{ t('alarmCenter.scenarioStudio.nodes.source') }}</v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="12" md="4">
              <v-select :model-value="definition.source.kind" :items="sourceKindItems"
                :label="t('alarmCenter.scenarioStudio.sourceKind')" variant="outlined"
                @update:model-value="updateSourceKind" />
            </v-col>
            <v-col cols="12" md="4">
              <v-combobox v-model="definition.source.matchKey" :items="matchKeyItems"
                :label="t('alarmCenter.scenarioStudio.matchKey')" variant="outlined" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model="definition.source.observationKind"
                :label="t('alarmCenter.scenarioStudio.observationKind')" variant="outlined" />
            </v-col>
            <template v-if="definition.source.kind === 'scheduled-query'">
              <v-col cols="12">
                <v-alert type="info" variant="tonal" density="compact">
                  {{ t('alarmCenter.scenarioStudio.scheduleDeclarativeHint') }}
                </v-alert>
              </v-col>
              <v-col cols="12" md="5">
                <v-text-field v-model="scheduleEditor.expression"
                  :label="t('alarmCenter.scenarioStudio.scheduleExpression')" variant="outlined" />
              </v-col>
              <v-col cols="12" md="3">
                <v-text-field v-model="scheduleEditor.timeZone"
                  :label="t('alarmCenter.scenarioStudio.timeZone')" variant="outlined" />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field v-model.number="scheduleEditor.maxLookbackSeconds" type="number" min="1" max="604800"
                  :label="t('alarmCenter.scenarioStudio.maxLookbackSeconds')" variant="outlined" />
              </v-col>
            </template>
            <template v-if="definition.source.kind === 'meta-correlation'">
              <v-col cols="12" md="8">
                <v-combobox v-model="definition.source.dependsOnScenarioIds"
                  :items="catalogScenarioIds" multiple chips closable-chips
                  :label="t('alarmCenter.scenarioStudio.dependsOnScenarios')" variant="outlined" />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field v-model.number="definition.source.maxChainDepth" type="number" min="1" max="20"
                  :label="t('alarmCenter.scenarioStudio.maxChainDepth')" variant="outlined" />
              </v-col>
              <v-col cols="12">
                <v-alert type="warning" variant="tonal" density="compact">
                  {{ t('alarmCenter.scenarioStudio.metaCapabilityHint') }}
                </v-alert>
              </v-col>
            </template>
          </v-row>
        </v-card-text>
      </v-card>

      <v-card variant="outlined" class="rounded-lg mb-4">
        <v-card-title class="d-flex align-center">
          {{ t('alarmCenter.scenarioStudio.nodes.conditions') }}
          <v-spacer />
          <v-btn size="small" variant="tonal" prepend-icon="mdi-plus" @click="addCondition">
            {{ t('alarmCenter.scenarioStudio.addCondition') }}
          </v-btn>
        </v-card-title>
        <v-card-text>
          <div v-if="rootCondition" class="d-flex gap-2 mb-3">
            <v-btn v-for="logic in logicItems" :key="logic.value" size="small"
              :variant="rootCondition.logic === logic.value ? 'flat' : 'outlined'"
              :color="rootCondition.logic === logic.value ? 'primary' : undefined"
              @click="selectConditionLogic(logic.value)">
              {{ logic.title }}
            </v-btn>
          </div>
          <v-card v-for="(condition, index) in rootCondition?.children || (leafCondition ? [leafCondition] : [])"
            :key="index" variant="tonal" class="mb-3">
            <v-card-text>
              <v-row dense align="center">
                <v-col cols="12" md="3">
                  <v-text-field v-model="condition.field" :label="t('alarmCenter.scenarioStudio.field')"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6" md="2">
                  <v-select v-model="condition.operator" :items="operatorItems"
                    :label="t('alarmCenter.scenarioStudio.operator')" variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6" md="3">
                  <v-text-field v-model="condition.value" :label="t('alarmCenter.scenarioStudio.value')"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="10" md="3">
                  <v-text-field v-model.number="condition.sustainedForSeconds" type="number" min="0"
                    :label="t('alarmCenter.scenarioStudio.sustainedSeconds')"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="2" md="1">
                  <v-btn v-if="rootCondition" icon="mdi-delete-outline" variant="text" color="error"
                    @click="removeCondition(index)" />
                </v-col>
              </v-row>
            </v-card-text>
          </v-card>
          <v-alert v-if="!definition.condition" type="info" variant="tonal">
            {{ t('alarmCenter.scenarioStudio.noConditions') }}
          </v-alert>
        </v-card-text>
      </v-card>

      <v-card variant="outlined" class="rounded-lg mb-4">
        <v-card-title class="d-flex align-center">
          {{ t('alarmCenter.scenarioStudio.nodes.aggregation') }}
          <v-spacer />
          <v-btn v-if="!definition.aggregation" size="small" variant="tonal" @click="ensureAggregation">
            {{ t('alarmCenter.scenarioStudio.addAggregation') }}
          </v-btn>
          <v-btn v-else size="small" variant="text" color="error" @click="removeAggregation">
            {{ t('alarmCenter.scenarioStudio.remove') }}
          </v-btn>
        </v-card-title>
        <v-card-text v-if="definition.aggregation">
          <v-row dense>
            <v-col cols="6" md="3">
              <v-select v-model="definition.aggregation.function" :items="aggregationItems"
                :label="t('alarmCenter.scenarioStudio.aggregation')" variant="outlined" />
            </v-col>
            <v-col cols="6" md="3">
              <v-text-field v-model="definition.aggregation.field" :label="t('alarmCenter.scenarioStudio.field')"
                variant="outlined" />
            </v-col>
            <v-col cols="6" md="2">
              <v-select v-model="definition.aggregation.operator" :items="operatorItems"
                :label="t('alarmCenter.scenarioStudio.operator')" variant="outlined" />
            </v-col>
            <v-col cols="6" md="2">
              <v-text-field v-model.number="definition.aggregation.threshold" type="number"
                :label="t('alarmCenter.scenarioStudio.value')" variant="outlined" />
            </v-col>
            <v-col cols="12" md="2">
              <v-text-field :model-value="(definition.window?.durationSeconds || 0) / 60"
                type="number" :label="t('alarmCenter.scenarioStudio.windowMinutes')" variant="outlined"
                @focus="ensureWindow"
                @update:model-value="updateWindowMinutes" />
            </v-col>
            <v-col cols="12">
              <v-combobox v-model="definition.groupBy" :items="groupByItems" multiple chips closable-chips
                :label="t('alarmCenter.scenarioStudio.groupBy')" variant="outlined" />
            </v-col>
          </v-row>
        </v-card-text>
      </v-card>

      <v-card variant="outlined" class="rounded-lg mb-4">
        <v-card-title class="d-flex align-center">
          {{ t('alarmCenter.scenarioStudio.nodes.sequence') }}
          <v-spacer />
          <v-btn size="small" variant="tonal" prepend-icon="mdi-plus" @click="addSequenceStep">
            {{ t('alarmCenter.scenarioStudio.addStep') }}
          </v-btn>
        </v-card-title>
        <v-card-text>
          <v-card v-for="(step, index) in definition.sequence?.steps || []" :key="index"
            variant="tonal" class="mb-3">
            <v-card-text>
              <v-row dense align="center">
                <v-col cols="12" md="5">
                  <v-combobox v-model="step.matchKey" :items="matchKeyItems"
                    :label="`${t('alarmCenter.scenarioStudio.sequenceStep')} ${index + 1}`"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="5" md="3">
                  <v-text-field v-model.number="step.minCount" type="number" min="1"
                    :label="t('alarmCenter.scenarioStudio.count')" variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="5" md="3">
                  <v-text-field :model-value="step.withinSeconds / 60" type="number" min="1"
                    :label="t('alarmCenter.scenarioStudio.windowMinutes')" variant="outlined" density="compact"
                    hide-details @update:model-value="step.withinSeconds = Number($event) * 60" />
                </v-col>
                <v-col cols="2" md="1">
                  <v-btn icon="mdi-delete-outline" variant="text" color="error" @click="removeSequenceStep(index)" />
                </v-col>
              </v-row>
            </v-card-text>
          </v-card>
          <v-alert v-if="!definition.sequence?.steps.length" type="info" variant="tonal">
            {{ t('alarmCenter.scenarioStudio.noSequence') }}
          </v-alert>
        </v-card-text>
      </v-card>

      <v-card variant="outlined" class="rounded-lg mb-4">
        <v-card-title>{{ t('alarmCenter.scenarioStudio.nodes.output') }}</v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="12" md="5">
              <v-text-field v-model="definition.dedup.keyTemplate"
                :label="t('alarmCenter.scenarioStudio.dedupTemplate')" variant="outlined" />
            </v-col>
            <v-col cols="12" md="3">
              <v-text-field v-model.number="definition.dedup.cooldownSeconds" type="number" min="0"
                :label="t('alarmCenter.scenarioStudio.cooldownSeconds')" variant="outlined" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field :model-value="definition.window?.stalenessSeconds || 0" type="number" min="0"
                :label="t('alarmCenter.scenarioStudio.stalenessSeconds')" variant="outlined"
                @focus="ensureWindow"
                @update:model-value="updateStalenessSeconds" />
            </v-col>
            <v-col cols="12">
              <v-divider class="mb-4" />
              <div class="d-flex align-center mb-3">
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('alarmCenter.scenarioStudio.hysteresis.title') }}
                  </div>
                  <div class="text-caption text-medium-emphasis">
                    {{ t('alarmCenter.scenarioStudio.hysteresis.hint') }}
                  </div>
                </div>
                <v-spacer />
                <v-btn v-if="!definition.hysteresis" variant="tonal" size="small"
                  prepend-icon="mdi-swap-vertical" @click="ensureHysteresis">
                  {{ t('alarmCenter.scenarioStudio.hysteresis.enable') }}
                </v-btn>
                <v-btn v-else variant="text" color="error" size="small" @click="removeHysteresis">
                  {{ t('alarmCenter.scenarioStudio.remove') }}
                </v-btn>
              </div>
              <v-row v-if="definition.hysteresis" dense>
                <v-col cols="12" md="4">
                  <v-text-field v-model.number="definition.hysteresis.raiseThreshold" type="number"
                    :label="t('alarmCenter.scenarioStudio.hysteresis.raiseThreshold')" variant="outlined" />
                </v-col>
                <v-col cols="12" md="4">
                  <v-text-field v-model.number="definition.hysteresis.clearThreshold" type="number"
                    :label="t('alarmCenter.scenarioStudio.hysteresis.clearThreshold')" variant="outlined" />
                </v-col>
                <v-col cols="12" md="4">
                  <v-text-field v-model.number="definition.hysteresis.minimumStateSeconds" type="number"
                    min="0" max="604800"
                    :label="t('alarmCenter.scenarioStudio.hysteresis.minimumStateSeconds')" variant="outlined" />
                </v-col>
              </v-row>
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="metadataText" :label="t('alarmCenter.scenarioStudio.metadata')"
                :hint="t('alarmCenter.scenarioStudio.metadataHint')" persistent-hint variant="outlined" rows="3" />
            </v-col>
          </v-row>
        </v-card-text>
      </v-card>
    </div>

    <v-card v-if="current?.validation?.diagnostics.length" variant="outlined" class="rounded-lg mb-4">
      <v-card-title>{{ t('alarmCenter.scenarioStudio.diagnostics.validationTitle') }}</v-card-title>
      <v-list lines="three">
        <v-list-item v-for="diagnostic in current.validation.diagnostics"
          :key="`${diagnostic.code}-${diagnostic.path}`"
          :prepend-icon="diagnostic.severity === 'error' ? 'mdi-alert-circle' : 'mdi-information'"
          :base-color="diagnostic.severity === 'error' ? 'error' : 'warning'"
          :title="diagnostic.code" :subtitle="diagnostic.message">
          <template #append>
            <div class="d-flex flex-column align-end gap-1">
              <v-chip size="x-small" :color="diagnostic.severity === 'error' ? 'error' : 'warning'">
                {{ diagnostic.severity }}
              </v-chip>
              <code v-if="diagnostic.path" class="text-caption">{{ diagnostic.path }}</code>
            </div>
          </template>
        </v-list-item>
      </v-list>
    </v-card>

    <v-card variant="outlined" class="rounded-lg mb-4">
      <v-card-title>{{ t('alarmCenter.scenarioStudio.simulationTitle') }}</v-card-title>
      <v-card-text>
        <v-textarea v-model="simulationJson" :label="t('alarmCenter.scenarioStudio.sampleJson')"
          variant="outlined" rows="8" class="studio-mono" />
        <div class="d-flex flex-wrap gap-2">
          <v-btn variant="outlined" @click="runDefinitionCheck('compile')">
            {{ t('alarmCenter.scenarioStudio.compile') }}
          </v-btn>
          <v-btn variant="outlined" @click="runDefinitionCheck('preview')">
            {{ t('alarmCenter.scenarioStudio.preview') }}
          </v-btn>
          <v-btn color="primary" prepend-icon="mdi-play" @click="simulate">
            {{ t('alarmCenter.scenarioStudio.simulate') }}
          </v-btn>
        </div>
        <v-alert v-if="simulationResult && !simulationResult.supported"
          type="warning" variant="tonal" class="mt-3">
          {{ t('alarmCenter.scenarioStudio.diagnostics.capabilityUnsupported') }}
        </v-alert>
        <v-list v-if="simulationResult" class="mt-3" lines="three">
          <v-list-item v-for="match in simulationResult.matches" :key="match.sampleIndex"
            :prepend-icon="match.matched ? 'mdi-check-circle' : 'mdi-close-circle'"
            :base-color="match.matched ? 'success' : undefined"
            :title="t('alarmCenter.scenarioStudio.sampleResult', { index: String(match.sampleIndex + 1) })"
            :subtitle="match.explanation">
            <template #append>
              <v-chip size="small" :color="match.matched ? 'success' : undefined">
                {{ match.matched ? t('alarmCenter.scenarioStudio.matched') : t('alarmCenter.scenarioStudio.notMatched') }}
              </v-chip>
            </template>
          </v-list-item>
          <v-list-item v-for="diagnostic in simulationResult.diagnostics"
            :key="`${diagnostic.code}-${diagnostic.path}`"
            :prepend-icon="diagnostic.severity === 'error' ? 'mdi-alert-circle' : 'mdi-information'"
            :base-color="diagnostic.severity === 'error' ? 'error' : 'warning'"
            :title="diagnostic.code" :subtitle="diagnostic.message">
            <template #append>
              <div class="d-flex flex-column align-end gap-1">
                <v-chip size="x-small" :color="diagnostic.severity === 'error' ? 'error' : 'warning'">
                  {{ diagnostic.severity }}
                </v-chip>
                <code v-if="diagnostic.path" class="text-caption">{{ diagnostic.path }}</code>
              </div>
            </template>
          </v-list-item>
        </v-list>
      </v-card-text>
    </v-card>

    <v-expansion-panels class="mb-4">
      <v-expansion-panel>
        <v-expansion-panel-title>{{ t('alarmCenter.scenarioStudio.canonicalJson') }}</v-expansion-panel-title>
        <v-expansion-panel-text>
          <pre class="studio-json">{{ canonicalJson }}</pre>
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>

    <v-card variant="outlined" class="rounded-lg">
      <v-card-actions class="flex-wrap pa-4 gap-2">
        <v-btn color="primary" :disabled="!canEditDraft" :loading="api.pending.value" @click="saveDraft">
          {{ t('alarmCenter.scenarioStudio.saveDraft') }}
        </v-btn>
        <v-btn variant="tonal" :disabled="!current || current.isReadOnly || current.status !== 'draft'"
          @click="validateDraft">
          {{ t('alarmCenter.scenarioStudio.validate') }}
        </v-btn>
        <v-btn color="success" variant="tonal" :disabled="!canPublish" @click="publishDraft">
          {{ t('alarmCenter.scenarioStudio.publish') }}
        </v-btn>
        <v-btn variant="outlined" :disabled="!current" @click="loadAudit">
          {{ t('alarmCenter.scenarioStudio.audit') }}
        </v-btn>
        <v-spacer />
        <v-btn variant="text" :disabled="current?.isReadOnly || current?.status !== 'published'"
          @click="rollbackVersion">
          {{ t('alarmCenter.scenarioStudio.rollback') }}
        </v-btn>
        <v-btn color="error" variant="text" :disabled="current?.isReadOnly || current?.status !== 'published'"
          @click="archiveVersion">
          {{ t('alarmCenter.scenarioStudio.archive') }}
        </v-btn>
      </v-card-actions>
    </v-card>

    <v-dialog v-model="showAudit" max-width="720">
      <v-card>
        <v-card-title>{{ t('alarmCenter.scenarioStudio.auditTitle') }}</v-card-title>
        <v-card-text>
          <v-timeline density="compact" side="end">
            <v-timeline-item v-for="entry in auditEntries" :key="entry.id" dot-color="primary" size="small">
              <div class="font-weight-medium">{{ entry.action }} · v{{ entry.version }}</div>
              <div class="text-caption text-medium-emphasis">{{ new Date(entry.timestamp).toLocaleString() }}</div>
            </v-timeline-item>
          </v-timeline>
          <v-alert v-if="!auditEntries.length" type="info" variant="tonal">
            {{ t('alarmCenter.scenarioStudio.auditEmpty') }}
          </v-alert>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="showAudit = false">{{ t('alarmCenter.scenarioStudio.close') }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.studio-choice {
  cursor: pointer;
}
.studio-choice--active {
  border-color: rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-primary), 0.06);
}
.studio-catalog-list {
  max-height: 360px;
  overflow-y: auto;
}
.studio-editor--locked {
  pointer-events: none;
  opacity: 0.78;
}
.studio-json {
  overflow: auto;
  max-height: 520px;
  margin: 0;
  padding: 16px;
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.05);
  font: 0.8125rem/1.5 ui-monospace, 'Cascadia Code', Consolas, monospace;
}
.studio-mono :deep(textarea) {
  font-family: ui-monospace, 'Cascadia Code', Consolas, monospace;
}
</style>
