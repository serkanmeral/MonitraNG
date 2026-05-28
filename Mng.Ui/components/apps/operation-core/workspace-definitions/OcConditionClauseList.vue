<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcFormPolicyDefaultValueInput from '@/components/apps/operation-core/workspace-definitions/OcFormPolicyDefaultValueInput.vue';
import {
  OC_CONDITION_OPERATORS_EQ_NE,
  OC_RULE_CONDITION_OPERATORS,
  newConditionClauseId,
  isValuelessConditionOperator,
  type OcConditionClause,
  type OcConditionFieldOption,
  type OcConditionOperator,
} from '@/utils/ocConditionClauses';

const props = withDefaults(
  defineProps<{
    workspaceId: string;
    conditionFields: OcConditionFieldOption[];
    typeItems?: { value: string; title: string }[];
    priorityItems?: { value: string; title: string }[];
    stateItems?: { value: string; title: string }[];
    boardItems?: { value: string; title: string }[];
    /** Politika: eq/ne. Kurallar: geniş set. */
    operators?: readonly OcConditionOperator[];
    /** policies | rules — i18n kökü */
    labelMode?: 'policies' | 'rules';
  }>(),
  {
    operators: () => OC_RULE_CONDITION_OPERATORS,
    labelMode: 'rules',
  }
);

const clauses = defineModel<OcConditionClause[]>({ required: true });

const { t } = useAppI18n();

const i18nRoot = computed(() =>
  props.labelMode === 'policies'
    ? 'operationCore.workspaceDefinitions.policies'
    : 'operationCore.workspaceDefinitions.rules'
);

const fieldSelectItems = computed(() =>
  props.conditionFields.map((f) => ({
    value: f.key,
    title: f.label,
  }))
);

const operatorItems = computed(() =>
  props.operators.map((op) => ({
    value: op,
    title: operatorLabel(op),
  }))
);

function operatorLabel(op: OcConditionOperator): string {
  if (props.labelMode === 'policies') {
    if (op === 'eq') return t('operationCore.workspaceDefinitions.policies.operatorEq');
    if (op === 'ne') return t('operationCore.workspaceDefinitions.policies.operatorNe');
  }
  const key = `operator${op.charAt(0).toUpperCase()}${op.slice(1)}` as
    | 'operatorEq'
    | 'operatorNe'
    | 'operatorEmpty'
    | 'operatorNotEmpty'
    | 'operatorGt'
    | 'operatorLt';
  const path = `${i18nRoot.value}.${key}`;
  const translated = t(path);
  return translated !== path ? translated : op;
}

function fieldMeta(fieldKey: string): OcConditionFieldOption | undefined {
  return props.conditionFields.find((f) => f.key === fieldKey);
}

function addClause() {
  const firstKey = props.conditionFields[0]?.key ?? 'stateId';
  clauses.value = [
    ...clauses.value,
    {
      id: newConditionClauseId(),
      fieldKey: firstKey,
      operator: props.operators[0] ?? 'eq',
      value: null,
    },
  ];
}

function removeClause(id: string) {
  clauses.value = clauses.value.filter((c) => c.id !== id);
}

function updateClause(id: string, patch: Partial<OcConditionClause>) {
  clauses.value = clauses.value.map((c) => (c.id === id ? { ...c, ...patch } : c));
}

function onFieldChange(clause: OcConditionClause, fieldKey: string) {
  updateClause(clause.id, { fieldKey, value: null });
}

function onOperatorChange(clause: OcConditionClause, operator: OcConditionOperator) {
  const patch: Partial<OcConditionClause> = { operator };
  if (isValuelessConditionOperator(operator)) patch.value = null;
  updateClause(clause.id, patch);
}

function onValueChange(clause: OcConditionClause, value: unknown) {
  updateClause(clause.id, { value });
}

function showValueInput(clause: OcConditionClause): boolean {
  return !isValuelessConditionOperator(clause.operator);
}
</script>

<template>
  <div class="oc-condition-clause-list">
    <p class="text-caption text-medium-emphasis mb-2">
      {{ t(`${i18nRoot}.conditionsAndHint`) }}
    </p>

    <v-alert
      v-if="!conditionFields.length"
      type="warning"
      variant="tonal"
      density="compact"
      class="mb-3"
    >
      {{ t(`${i18nRoot}.noConditionFields`) }}
    </v-alert>

    <div
      v-for="(clause, index) in clauses"
      :key="clause.id"
      class="oc-condition-clause-list__row pa-3 mb-3 rounded-lg border"
    >
      <div class="d-flex align-center justify-space-between mb-2">
        <span class="text-caption font-weight-medium text-medium-emphasis">
          {{ t(`${i18nRoot}.conditionRowLabel`, { index: index + 1 }) }}
        </span>
        <v-btn
          icon="mdi-close"
          size="x-small"
          variant="text"
          :aria-label="t(`${i18nRoot}.removeCondition`)"
          @click="removeClause(clause.id)"
        />
      </div>

      <v-row dense class="oc-condition-clause-list__fields">
        <v-col cols="12" sm="5">
          <v-select
            :model-value="clause.fieldKey"
            :items="fieldSelectItems"
            item-title="title"
            item-value="value"
            :label="t(`${i18nRoot}.conditionField`)"
            variant="outlined"
            density="comfortable"
            hide-details
            @update:model-value="onFieldChange(clause, $event)"
          />
        </v-col>
        <v-col cols="12" sm="3">
          <v-select
            :model-value="clause.operator"
            :items="operatorItems"
            item-title="title"
            item-value="value"
            :label="t(`${i18nRoot}.conditionOperator`)"
            variant="outlined"
            density="comfortable"
            hide-details
            @update:model-value="onOperatorChange(clause, $event)"
          />
        </v-col>
        <v-col v-if="showValueInput(clause)" cols="12" sm="4">
          <OcFormPolicyDefaultValueInput
            v-if="fieldMeta(clause.fieldKey)"
            :model-value="clause.value"
            :field-key="clause.fieldKey"
            :field-type="fieldMeta(clause.fieldKey)?.fieldType"
            :relation-dataset="fieldMeta(clause.fieldKey)?.relationDataset"
            :cardinality="fieldMeta(clause.fieldKey)?.cardinality"
            :workspace-id="workspaceId"
            :type-items="typeItems"
            :priority-items="priorityItems"
            :state-items="stateItems"
            :board-items="boardItems"
            :field-label="t(`${i18nRoot}.conditionValue`)"
            density="comfortable"
            control-variant="outlined"
            @update:model-value="onValueChange(clause, $event)"
          />
        </v-col>
      </v-row>
    </div>

    <v-btn
      variant="tonal"
      color="primary"
      size="small"
      class="text-none"
      prepend-icon="mdi-plus"
      :disabled="!conditionFields.length"
      @click="addClause"
    >
      {{ t(`${i18nRoot}.addCondition`) }}
    </v-btn>
  </div>
</template>

<style scoped>
.oc-condition-clause-list__row {
  border-color: rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
