<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type {
  AlarmRule,
  AlarmRuleType,
  AlarmRuleSavePayload,
  CreateAlarmRuleRequest,
  UpdateAlarmRuleRequest,
} from '@/types/apps/alarm';
import {
  ALARM_RULE_GROUP_BY_OPTIONS,
  ALARM_RULE_MATCH_KEY_OPTIONS,
  ALARM_RULE_TYPE_CARDS,
  defaultDedupTemplate,
  operatorSymbol,
  severityBand,
} from '@/composables/useAlarmRuleFormCatalog';

const props = defineProps<{
  modelValue: boolean;
  editingRule: AlarmRule | null;
  saving: boolean;
  saveError?: string | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [open: boolean];
  save: [payload: AlarmRuleSavePayload];
}>();

const { t } = useAppI18n();

const formError = ref<string | null>(null);
const useDefaultDedup = ref(true);
const groupByFields = ref<string[]>([]);

const form = ref({
  name: '',
  type: 'threshold' as AlarmRuleType,
  enabled: true,
  severity: 5,
  matchKey: '',
  operator: 'gt',
  threshold: 0,
  cooldownMinutes: 5,
  windowMinutes: 5,
  stalenessMinutes: 30,
  dedupKeyTemplate: '',
});

const isEdit = computed(() => props.editingRule != null);

const operatorItems = computed(() => [
  { title: '>', value: 'gt' },
  { title: '≥', value: 'gte' },
  { title: '<', value: 'lt' },
  { title: '≤', value: 'lte' },
  { title: '=', value: 'eq' },
]);

const matchKeyItems = computed(() =>
  ALARM_RULE_MATCH_KEY_OPTIONS.map((opt) => ({
    title: opt.value,
    value: opt.value,
    subtitle: opt.scenarioId
      ? `${opt.scenarioId} · ${t(opt.descriptionKey)}`
      : t(opt.descriptionKey),
  })),
);

const groupByItems = computed(() =>
  ALARM_RULE_GROUP_BY_OPTIONS.map((opt) => ({
    title: opt.value,
    value: opt.value,
    subtitle: t(opt.descriptionKey),
  })),
);

const severityLabelKey = computed(() => {
  const band = severityBand(form.value.severity);
  return `alarmCenter.rules.severityBand.${band}`;
});

const severityColor = computed(() => {
  const s = form.value.severity;
  if (s >= 8) return 'error';
  if (s >= 5) return 'warning';
  return 'info';
});

const showThresholdFields = computed(() => form.value.type === 'threshold');
const showCorrelationFields = computed(() => form.value.type === 'correlation');
const showScheduledFields = computed(() => form.value.type === 'scheduled');
const showWindowField = computed(() => form.value.type === 'correlation');
const isAdvancedRuleType = computed(
  () => form.value.type !== 'threshold' && form.value.type !== 'correlation' && form.value.type !== 'scheduled',
);

const defaultDedupTemplateForType = computed(() => defaultDedupTemplate(form.value.type));

const dedupExampleGroupKey = computed(() => {
  if (form.value.type !== 'correlation' || groupByFields.value.length === 0) {
    return t('alarmCenter.rules.dedupExampleAll');
  }
  const samples: Record<string, string> = {
    userId: 'admin',
    srcIp: '192.168.1.50',
    dstIp: '10.0.0.1',
    dstPort: '443',
    sourceHost: 'DC01',
    sourceType: 'ad',
  };
  return groupByFields.value.map((field) => samples[field] ?? field).join(' · ');
});

function renderDedupExample(template: string): string {
  const matchKey = form.value.matchKey.trim() || 'login_failed';
  return template
    .replaceAll('{ruleId}', t('alarmCenter.rules.dedupTokenRuleIdExample'))
    .replaceAll('{groupKey}', dedupExampleGroupKey.value)
    .replaceAll('{key}', matchKey);
}

const activeDedupTemplate = computed(() => {
  if (useDefaultDedup.value) return defaultDedupTemplateForType.value;
  return form.value.dedupKeyTemplate.trim() || defaultDedupTemplateForType.value;
});

const dedupExampleKey = computed(() => renderDedupExample(activeDedupTemplate.value));

const dedupTokenItems = computed(() => [
  { token: '{ruleId}', label: t('alarmCenter.rules.dedupTokenRuleId') },
  { token: '{key}', label: t('alarmCenter.rules.dedupTokenKey') },
  { token: '{groupKey}', label: t('alarmCenter.rules.dedupTokenGroupKey') },
]);

const groupByDisplay = computed(() => {
  if (groupByFields.value.length === 0) return t('alarmCenter.rules.previewAllEvents');
  return groupByFields.value.join(' + ');
});

const rulePreview = computed(() => {
  const f = form.value;
  const key = f.matchKey.trim() || t('alarmCenter.rules.previewMatchKeyPlaceholder');
  const severity = String(f.severity);
  const cooldown = String(f.cooldownMinutes);

  if (isAdvancedRuleType.value) {
    return t('alarmCenter.rules.previewSequence', { matchKey: key, severity, cooldown });
  }
  if (f.type === 'threshold') {
    return t('alarmCenter.rules.previewThreshold', {
      matchKey: key,
      operator: operatorSymbol(f.operator),
      threshold: String(f.threshold),
      severity,
      cooldown,
    });
  }
  if (f.type === 'correlation') {
    return t('alarmCenter.rules.previewCorrelation', {
      window: String(f.windowMinutes),
      matchKey: key,
      groupBy: groupByDisplay.value,
      threshold: String(f.threshold),
      severity,
      cooldown,
    });
  }
  return t('alarmCenter.rules.previewScheduled', {
    staleness: String(f.stalenessMinutes),
    matchKey: key,
    severity,
    cooldown,
  });
});

function resetForm() {
  form.value = {
    name: '',
    type: 'threshold',
    enabled: true,
    severity: 5,
    matchKey: '',
    operator: 'gt',
    threshold: 0,
    cooldownMinutes: 5,
    windowMinutes: 5,
    stalenessMinutes: 30,
    dedupKeyTemplate: '',
  };
  groupByFields.value = [];
  useDefaultDedup.value = true;
  formError.value = null;
}

function loadFromRule(row: AlarmRule) {
  form.value = {
    name: row.name,
    type: (row.type as AlarmRuleType) || 'threshold',
    enabled: row.enabled,
    severity: row.severity,
    matchKey: row.matchKey,
    operator: row.operator || 'gt',
    threshold: row.threshold,
    cooldownMinutes: row.cooldownMinutes,
    windowMinutes: row.windowMinutes,
    stalenessMinutes: row.stalenessMinutes || 30,
    dedupKeyTemplate: row.dedupKeyTemplate || '',
  };
  groupByFields.value = [...(row.groupByFields || [])];
  const def = defaultDedupTemplate((row.type as AlarmRuleType) || 'threshold');
  useDefaultDedup.value = !row.dedupKeyTemplate || row.dedupKeyTemplate === def;
  formError.value = null;
}

function selectType(type: AlarmRuleType) {
  if (isEdit.value) return;
  form.value.type = type;
  if (type === 'correlation' && form.value.threshold < 1) {
    form.value.threshold = 10;
  }
  if (type === 'scheduled' && form.value.stalenessMinutes < 1) {
    form.value.stalenessMinutes = 30;
  }
}

function validate(): string | null {
  if (!form.value.name.trim()) return t('alarmCenter.rules.validationName');
  if (!isEdit.value && !form.value.matchKey.trim()) return t('alarmCenter.rules.validationMatchKey');
  if (form.value.severity < 1 || form.value.severity > 10) return t('alarmCenter.rules.validationSeverity');
  if (form.value.type === 'correlation') {
    if (form.value.threshold < 1) return t('alarmCenter.rules.validationEventCount');
    if (form.value.windowMinutes < 1) return t('alarmCenter.rules.validationWindow');
  }
  if (form.value.type === 'scheduled' && form.value.stalenessMinutes < 1) {
    return t('alarmCenter.rules.validationStaleness');
  }
  if (form.value.cooldownMinutes < 0) return t('alarmCenter.rules.validationCooldown');
  return null;
}

function onSave() {
  formError.value = validate();
  if (formError.value) return;

  const dedup = useDefaultDedup.value ? undefined : form.value.dedupKeyTemplate.trim() || undefined;

  if (isEdit.value && props.editingRule) {
    const body: UpdateAlarmRuleRequest = {
      name: form.value.name.trim(),
      enabled: form.value.enabled,
      severity: form.value.severity,
      operator: form.value.operator,
      threshold: form.value.threshold,
      cooldownMinutes: form.value.cooldownMinutes,
      windowMinutes: form.value.windowMinutes,
      stalenessMinutes: form.value.stalenessMinutes,
      groupByFields: groupByFields.value,
      dedupKeyTemplate: dedup,
    };
    emit('save', { isEdit: true, id: props.editingRule.id, body });
    return;
  }

  const body: CreateAlarmRuleRequest = {
    name: form.value.name.trim(),
    type: form.value.type,
    severity: form.value.severity,
    matchKey: form.value.matchKey.trim(),
    operator: form.value.operator,
    threshold: form.value.threshold,
    cooldownMinutes: form.value.cooldownMinutes,
    windowMinutes: form.value.windowMinutes,
    stalenessMinutes: form.value.stalenessMinutes,
    groupByFields: groupByFields.value.length ? groupByFields.value : undefined,
    dedupKeyTemplate: dedup,
  };
  emit('save', { isEdit: false, body });
}

function closeDialog() {
  emit('update:modelValue', false);
}

function insertDedupToken(token: string) {
  useDefaultDedup.value = false;
  const current = form.value.dedupKeyTemplate.trim();
  form.value.dedupKeyTemplate = current ? `${current}${token}` : token;
}

watch(useDefaultDedup, (useDefault) => {
  if (useDefault) form.value.dedupKeyTemplate = '';
});

watch(
  () => [props.modelValue, props.editingRule] as const,
  ([open, rule]) => {
    if (!open) return;
    if (rule) loadFromRule(rule);
    else resetForm();
  },
);

watch(
  () => props.saveError,
  (err) => {
    if (err) formError.value = err;
  },
);
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="760"
    persistent
    scrollable
    @update:model-value="emit('update:modelValue', $event)"
  >
    <v-card class="rounded-lg ac-alarm-rule-form">
      <v-card-title class="d-flex align-start gap-3 pa-5 pb-3">
        <v-avatar color="primary" variant="tonal" size="40" rounded="lg">
          <v-icon :icon="isEdit ? 'mdi-pencil' : 'mdi-plus'" size="22" />
        </v-avatar>
        <div class="min-w-0">
          <div class="text-h6 font-weight-bold">
            {{ isEdit ? t('alarmCenter.rules.editTitle') : t('alarmCenter.rules.createTitle') }}
          </div>
          <div class="text-body-2 text-medium-emphasis mt-1">
            {{ isEdit ? t('alarmCenter.rules.editSubtitle') : t('alarmCenter.rules.createSubtitle') }}
          </div>
        </div>
      </v-card-title>

      <v-divider />

      <v-card-text class="pa-5">
        <v-alert
          v-if="formError"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          closable
          @click:close="formError = null"
        >
          {{ formError }}
        </v-alert>

        <v-alert type="info" variant="tonal" density="compact" class="mb-5 ac-rule-preview" icon="mdi-eye-outline">
          <div class="text-caption text-medium-emphasis mb-1">{{ t('alarmCenter.rules.previewTitle') }}</div>
          <div class="text-body-2">{{ rulePreview }}</div>
        </v-alert>

        <!-- Section: Basic -->
        <div class="ac-form-section mb-5">
          <div class="ac-form-section__title">{{ t('alarmCenter.rules.sectionBasic') }}</div>
          <div class="ac-form-section__desc">{{ t('alarmCenter.rules.sectionBasicDesc') }}</div>
          <v-row dense class="mt-3">
            <v-col cols="12" md="8">
              <v-text-field
                v-model="form.name"
                :label="t('alarmCenter.rules.fieldName')"
                :placeholder="t('alarmCenter.rules.fieldNamePlaceholder')"
                variant="outlined"
                density="comfortable"
                hide-details="auto"
                prepend-inner-icon="mdi-tag-outline"
              />
            </v-col>
            <v-col cols="12" md="4" class="d-flex align-center">
              <v-switch
                v-model="form.enabled"
                :label="t('alarmCenter.rules.fieldEnabled')"
                color="primary"
                hide-details
                density="comfortable"
              />
            </v-col>
          </v-row>
        </div>

        <!-- Section: Type -->
        <div class="ac-form-section mb-5">
          <div class="ac-form-section__title">{{ t('alarmCenter.rules.sectionDetection') }}</div>
          <div class="ac-form-section__desc">{{ t('alarmCenter.rules.sectionDetectionDesc') }}</div>
          <v-alert
            v-if="isEdit"
            type="warning"
            variant="tonal"
            density="compact"
            class="mt-3 mb-0"
          >
            {{ t('alarmCenter.rules.typeLockedHint') }}
          </v-alert>
          <v-alert
            v-if="isAdvancedRuleType"
            type="info"
            variant="tonal"
            density="compact"
            class="mt-3 mb-0"
          >
            {{ t('alarmCenter.rules.sequenceEditHint') }}
          </v-alert>
          <v-row v-if="!isAdvancedRuleType" dense class="mt-3">
            <v-col v-for="card in ALARM_RULE_TYPE_CARDS" :key="card.type" cols="12" md="4">
              <v-card
                variant="outlined"
                class="ac-type-card h-100"
                :class="{
                  'ac-type-card--active': form.type === card.type,
                  'ac-type-card--disabled': isEdit && form.type !== card.type,
                }"
                :ripple="!isEdit"
                @click="selectType(card.type)"
              >
                <v-card-text class="pa-4">
                  <v-icon :icon="card.icon" size="28" color="primary" class="mb-2" />
                  <div class="text-subtitle-2 font-weight-bold">{{ t(card.titleKey) }}</div>
                  <div class="text-caption text-medium-emphasis mt-1">{{ t(card.subtitleKey) }}</div>
                </v-card-text>
              </v-card>
            </v-col>
          </v-row>
          <v-row dense :class="isAdvancedRuleType ? 'mt-3' : 'mt-2'">
            <v-col cols="12">
              <v-combobox
                v-model="form.matchKey"
                :items="matchKeyItems"
                item-title="title"
                item-value="value"
                :label="t('alarmCenter.rules.fieldMatchKey')"
                :hint="t('alarmCenter.rules.fieldMatchKeyHint')"
                :placeholder="t('alarmCenter.rules.fieldMatchKeyPlaceholder')"
                :disabled="isEdit"
                variant="outlined"
                density="comfortable"
                persistent-hint
                hide-details="auto"
                prepend-inner-icon="mdi-key-outline"
                clearable
              >
                <template #item="{ props: itemProps, item }">
                  <v-list-item v-bind="itemProps" :subtitle="item.raw.subtitle" />
                </template>
              </v-combobox>
            </v-col>
          </v-row>
        </div>

        <!-- Section: Condition -->
        <div v-if="!isAdvancedRuleType" class="ac-form-section mb-5">
          <div class="ac-form-section__title">{{ t('alarmCenter.rules.sectionCondition') }}</div>
          <div class="ac-form-section__desc">{{ t('alarmCenter.rules.sectionConditionDesc') }}</div>
          <v-row dense class="mt-3">
            <template v-if="showThresholdFields">
              <v-col cols="12" md="4">
                <v-select
                  v-model="form.operator"
                  :items="operatorItems"
                  item-title="title"
                  item-value="value"
                  :label="t('alarmCenter.rules.fieldOperator')"
                  variant="outlined"
                  density="comfortable"
                  hide-details="auto"
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field
                  v-model.number="form.threshold"
                  type="number"
                  :label="t('alarmCenter.rules.fieldThreshold')"
                  :hint="t('alarmCenter.rules.fieldThresholdHint')"
                  persistent-hint
                  variant="outlined"
                  density="comfortable"
                  hide-details="auto"
                />
              </v-col>
            </template>
            <template v-if="showCorrelationFields">
              <v-col cols="12" md="4">
                <v-text-field
                  v-model.number="form.threshold"
                  type="number"
                  min="1"
                  :label="t('alarmCenter.rules.fieldEventCount')"
                  :hint="t('alarmCenter.rules.fieldEventCountHint')"
                  persistent-hint
                  variant="outlined"
                  density="comfortable"
                  hide-details="auto"
                />
              </v-col>
              <v-col cols="12" md="8">
                <v-combobox
                  v-model="groupByFields"
                  :items="groupByItems"
                  item-title="title"
                  item-value="value"
                  :label="t('alarmCenter.rules.fieldGroupBy')"
                  :hint="t('alarmCenter.rules.fieldGroupByHint')"
                  :placeholder="t('alarmCenter.rules.fieldGroupByPlaceholder')"
                  variant="outlined"
                  density="comfortable"
                  persistent-hint
                  hide-details="auto"
                  multiple
                  chips
                  closable-chips
                  clearable
                >
                  <template #item="{ props: itemProps, item }">
                    <v-list-item v-bind="itemProps" :subtitle="item.raw.subtitle" />
                  </template>
                </v-combobox>
              </v-col>
            </template>
            <template v-if="showScheduledFields">
              <v-col cols="12" md="6">
                <v-text-field
                  v-model.number="form.stalenessMinutes"
                  type="number"
                  min="1"
                  :label="t('alarmCenter.rules.fieldStaleness')"
                  :hint="t('alarmCenter.rules.fieldStalenessHint')"
                  persistent-hint
                  variant="outlined"
                  density="comfortable"
                  hide-details="auto"
                  prepend-inner-icon="mdi-timer-sand"
                />
              </v-col>
            </template>
            <v-col v-if="showWindowField" cols="12" md="4">
              <v-text-field
                v-model.number="form.windowMinutes"
                type="number"
                min="1"
                :label="t('alarmCenter.rules.fieldWindow')"
                :hint="t('alarmCenter.rules.fieldWindowHint')"
                persistent-hint
                variant="outlined"
                density="comfortable"
                hide-details="auto"
              />
            </v-col>
          </v-row>
        </div>

        <!-- Section: Behavior -->
        <div class="ac-form-section mb-4">
          <div class="ac-form-section__title">{{ t('alarmCenter.rules.sectionBehavior') }}</div>
          <div class="ac-form-section__desc">{{ t('alarmCenter.rules.sectionBehaviorDesc') }}</div>
          <v-row dense class="mt-3">
            <v-col cols="12">
              <div class="d-flex align-center gap-4 flex-wrap">
                <v-slider
                  v-model="form.severity"
                  :min="1"
                  :max="10"
                  :step="1"
                  show-ticks="always"
                  tick-size="2"
                  color="primary"
                  class="flex-grow-1 ac-severity-slider"
                  hide-details
                />
                <v-chip :color="severityColor" variant="tonal" size="small" class="flex-shrink-0">
                  {{ form.severity }} · {{ t(severityLabelKey) }}
                </v-chip>
              </div>
              <div class="text-caption text-medium-emphasis mt-1">{{ t('alarmCenter.rules.fieldSeverityHint') }}</div>
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model.number="form.cooldownMinutes"
                type="number"
                min="0"
                :label="t('alarmCenter.rules.fieldCooldown')"
                :hint="t('alarmCenter.rules.fieldCooldownHint')"
                persistent-hint
                variant="outlined"
                density="comfortable"
                hide-details="auto"
                prepend-inner-icon="mdi-timer-outline"
              />
            </v-col>
            <v-col cols="12">
              <v-alert type="info" variant="tonal" density="compact" class="mb-0">
                <div class="text-subtitle-2 font-weight-medium mb-1">
                  {{ t('alarmCenter.rules.cooldownVsDedupTitle') }}
                </div>
                <div class="text-body-2">{{ t('alarmCenter.rules.cooldownVsDedupBody') }}</div>
              </v-alert>
            </v-col>
          </v-row>
        </div>

        <!-- Section: Dedup -->
        <div class="ac-form-section ac-dedup-section">
          <div class="d-flex align-center gap-2 mb-1">
            <v-icon icon="mdi-merge" size="20" color="primary" />
            <div class="ac-form-section__title">{{ t('alarmCenter.rules.sectionDedup') }}</div>
          </div>
          <div class="ac-form-section__desc">{{ t('alarmCenter.rules.sectionDedupDesc') }}</div>

          <v-alert type="info" variant="tonal" density="compact" class="mt-3 mb-4" icon="mdi-information-outline">
            <div class="text-body-2">{{ t('alarmCenter.rules.dedupPlainExplain') }}</div>
          </v-alert>

          <v-switch
            v-model="useDefaultDedup"
            :label="t('alarmCenter.rules.useDefaultDedup')"
            :hint="t('alarmCenter.rules.useDefaultDedupHint')"
            persistent-hint
            color="primary"
            density="comfortable"
            hide-details="auto"
            class="mb-4"
          />

          <v-card v-if="useDefaultDedup" variant="tonal" color="primary" class="mb-3 rounded-lg">
            <v-card-text class="pa-4">
              <div class="text-caption text-medium-emphasis mb-1">{{ t('alarmCenter.rules.dedupDefaultTemplateLabel') }}</div>
              <code class="ac-dedup-code">{{ defaultDedupTemplateForType }}</code>
              <div class="text-caption text-medium-emphasis mt-3 mb-1">{{ t('alarmCenter.rules.dedupExampleLabel') }}</div>
              <code class="ac-dedup-code ac-dedup-code--example">{{ dedupExampleKey }}</code>
              <div class="text-caption text-medium-emphasis mt-2">{{ t('alarmCenter.rules.dedupExampleExplain') }}</div>
            </v-card-text>
          </v-card>

          <template v-else>
            <v-text-field
              v-model="form.dedupKeyTemplate"
              :label="t('alarmCenter.rules.fieldDedupTemplate')"
              :placeholder="defaultDedupTemplateForType"
              :hint="t('alarmCenter.rules.fieldDedupHint')"
              persistent-hint
              variant="outlined"
              density="comfortable"
              hide-details="auto"
              class="mb-2"
            />
            <div class="text-caption text-medium-emphasis mb-2">{{ t('alarmCenter.rules.dedupTokenHint') }}</div>
            <div class="d-flex flex-wrap gap-2 mb-3">
              <v-chip
                v-for="item in dedupTokenItems"
                :key="item.token"
                size="small"
                variant="outlined"
                prepend-icon="mdi-plus-circle-outline"
                class="ac-dedup-token-chip"
                @click="insertDedupToken(item.token)"
              >
                <span class="font-weight-medium">{{ item.token }}</span>
                <span class="text-medium-emphasis ml-1">— {{ item.label }}</span>
              </v-chip>
            </div>
            <v-card variant="outlined" class="rounded-lg">
              <v-card-text class="pa-3">
                <div class="text-caption text-medium-emphasis mb-1">{{ t('alarmCenter.rules.dedupExampleLabel') }}</div>
                <code class="ac-dedup-code ac-dedup-code--example">{{ dedupExampleKey }}</code>
              </v-card-text>
            </v-card>
          </template>
        </div>
      </v-card-text>

      <v-divider />

      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" :disabled="saving" @click="closeDialog">{{ t('alarmCenter.rules.cancel') }}</v-btn>
        <v-btn color="primary" variant="flat" :loading="saving" @click="onSave">{{ t('alarmCenter.rules.save') }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.ac-form-section__title {
  font-size: 0.9375rem;
  font-weight: 600;
  line-height: 1.3;
}

.ac-form-section__desc {
  font-size: 0.8125rem;
  color: rgba(var(--v-theme-on-surface), 0.62);
  margin-top: 2px;
}

.ac-type-card {
  cursor: pointer;
  transition: border-color 0.15s ease, background-color 0.15s ease;
}

.ac-type-card--active {
  border-color: rgb(var(--v-theme-primary)) !important;
  background: rgba(var(--v-theme-primary), 0.06);
}

.ac-type-card--disabled {
  opacity: 0.45;
  pointer-events: none;
}

.ac-type-card:not(.ac-type-card--active):not(.ac-type-card--disabled):hover {
  border-color: rgba(var(--v-theme-primary), 0.45);
}

.ac-severity-slider {
  min-width: 200px;
  max-width: 420px;
}

.ac-rule-preview {
  border-inline-start: 3px solid rgb(var(--v-theme-primary));
}

.ac-dedup-section {
  margin-top: 8px;
  padding-top: 20px;
  border-top: 1px dashed rgba(var(--v-theme-on-surface), 0.12);
}

.ac-dedup-code {
  display: block;
  font-family: ui-monospace, 'Cascadia Code', 'Consolas', monospace;
  font-size: 0.8125rem;
  line-height: 1.45;
  word-break: break-all;
  padding: 8px 10px;
  border-radius: 6px;
  background: rgba(var(--v-theme-on-surface), 0.06);
}

.ac-dedup-code--example {
  background: rgba(var(--v-theme-primary), 0.08);
}

.ac-dedup-token-chip {
  cursor: pointer;
}
</style>
