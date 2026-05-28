<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcFormPolicyDefaultValueInput from '@/components/apps/operation-core/workspace-definitions/OcFormPolicyDefaultValueInput.vue';
import OcWorkspaceFormTransitionRequirements from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceFormTransitionRequirements.vue';
import type { OpFormFieldBehavior, OpStateFlow } from '@/types/apps/operationCore';

export interface OcFormPolicyLayoutFieldItem {
  value: string;
  title: string;
  displayLabel?: string;
  fieldType?: string;
  relationDataset?: string | null;
  cardinality?: string;
}

const props = defineProps<{
  workspaceId: string;
  layoutFieldKeys: string[];
  layoutFieldItems: OcFormPolicyLayoutFieldItem[];
  defaultStateFlowId?: string;
  defaultTypeId?: string;
  stateFlows?: OpStateFlow[];
  typeItems?: { value: string; title: string }[];
  priorityItems?: { value: string; title: string }[];
  stateItems?: { value: string; title: string }[];
  boardItems?: { value: string; title: string }[];
}>();

const rulesTabLink = computed(() => {
  const qs = new URLSearchParams();
  if (props.workspaceId) qs.set('workspaceId', props.workspaceId);
  qs.set('tab', 'rules');
  return `/apps/operation-core/admin/workspace-definitions?${qs.toString()}`;
});

const fieldBehaviors = defineModel<Record<string, OpFormFieldBehavior>>('fieldBehaviors', {
  required: true,
});
const defaultValues = defineModel<Record<string, unknown>>('defaultValues', { required: true });

const { t } = useAppI18n();

const defaultBehavior = (): OpFormFieldBehavior => ({
  visible: true,
  required: false,
  readonly: false,
  masked: false,
});

const policyRows = computed(() =>
  props.layoutFieldKeys.map((key) => {
    const item = props.layoutFieldItems.find((i) => i.value === key);
    return {
      key,
      label: item?.displayLabel ?? item?.title ?? key,
      fieldType: item?.fieldType,
      relationDataset: item?.relationDataset ?? null,
      cardinality: item?.cardinality,
    };
  })
);

function ensureBehavior(key: string): OpFormFieldBehavior {
  if (!fieldBehaviors.value[key]) {
    fieldBehaviors.value = {
      ...fieldBehaviors.value,
      [key]: {
        ...defaultBehavior(),
        required: key === 'title' || key === 'typeId',
      },
    };
  }
  return fieldBehaviors.value[key];
}

function defaultModel(key: string): unknown {
  return defaultValues.value[key];
}

function setDefaultModel(key: string, value: unknown) {
  defaultValues.value = { ...defaultValues.value, [key]: value };
}
</script>

<template>
  <div class="oc-form-field-policy-editor">
    <v-alert type="info" variant="tonal" density="compact" class="mb-4 rounded-lg">
      {{ t('operationCore.workspaceDefinitions.forms.fieldPoliciesInfo') }}
    </v-alert>

    <div v-if="policyRows.length" class="d-flex flex-wrap align-center gap-2 mb-3">
      <v-chip size="small" variant="outlined" prepend-icon="mdi-form-textbox">
        {{
          t('operationCore.workspaceDefinitions.forms.fieldPoliciesFieldCount', {
            count: policyRows.length,
          })
        }}
      </v-chip>
    </div>

    <h4 class="text-subtitle-2 font-weight-medium mb-1">
      {{ t('operationCore.workspaceDefinitions.forms.fieldPoliciesTitle') }}
    </h4>
    <p class="text-caption text-medium-emphasis mb-4">
      {{ t('operationCore.workspaceDefinitions.forms.fieldPoliciesHint') }}
    </p>

    <v-table v-if="policyRows.length" density="compact" class="oc-form-field-policy-table">
      <thead>
        <tr>
          <th class="oc-form-field-policy-table__col-field">
            {{ t('operationCore.workspaceDefinitions.forms.behaviorColField') }}
          </th>
          <th class="oc-form-field-policy-table__col-flag">
            {{ t('operationCore.workspaceDefinitions.forms.behaviorColVisible') }}
          </th>
          <th class="oc-form-field-policy-table__col-flag">
            {{ t('operationCore.workspaceDefinitions.forms.behaviorColReadonly') }}
          </th>
          <th class="oc-form-field-policy-table__col-flag">
            {{ t('operationCore.workspaceDefinitions.forms.behaviorColRequired') }}
          </th>
          <th class="oc-form-field-policy-table__col-flag">
            {{ t('operationCore.workspaceDefinitions.forms.behaviorColMasked') }}
          </th>
          <th class="oc-form-field-policy-table__col-default">
            {{ t('operationCore.workspaceDefinitions.forms.policyColDefault') }}
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in policyRows"
          :key="row.key"
          :class="{ 'oc-form-field-policy-table__row--hidden-field': !ensureBehavior(row.key).visible }"
        >
          <td class="text-body-2">
            <div class="font-weight-medium">{{ row.label }}</div>
            <div class="text-caption text-medium-emphasis">{{ row.key }}</div>
          </td>
          <td>
            <v-checkbox v-model="ensureBehavior(row.key).visible" density="compact" hide-details />
          </td>
          <td>
            <v-checkbox v-model="ensureBehavior(row.key).readonly" density="compact" hide-details />
          </td>
          <td>
            <v-checkbox v-model="ensureBehavior(row.key).required" density="compact" hide-details />
          </td>
          <td>
            <v-checkbox v-model="ensureBehavior(row.key).masked" density="compact" hide-details />
          </td>
          <td class="oc-form-field-policy-table__default-cell">
            <OcFormPolicyDefaultValueInput
              :model-value="defaultModel(row.key)"
              :field-key="row.key"
              :field-type="row.fieldType"
              :relation-dataset="row.relationDataset"
              :cardinality="row.cardinality"
              :workspace-id="workspaceId"
              :type-items="typeItems"
              :priority-items="priorityItems"
              :state-items="stateItems"
              :board-items="boardItems"
              @update:model-value="(v) => setDefaultModel(row.key, v)"
            />
          </td>
        </tr>
      </tbody>
    </v-table>

    <v-alert v-else type="info" variant="tonal" class="rounded-lg">
      {{ t('operationCore.workspaceDefinitions.forms.fieldPoliciesEmpty') }}
    </v-alert>

    <v-divider class="my-6" />

    <div class="d-flex flex-wrap align-center gap-2 mb-2">
      <h4 class="text-subtitle-2 font-weight-medium mb-0">
        {{ t('operationCore.workspaceDefinitions.forms.rulesSectionTitle') }}
      </h4>
    </div>
    <p class="text-caption text-medium-emphasis mb-3">
      {{ t('operationCore.workspaceDefinitions.forms.rulesFormLinkHint') }}
    </p>
    <v-btn
      :to="rulesTabLink"
      variant="tonal"
      color="primary"
      size="small"
      class="text-none mb-6"
      prepend-icon="mdi-format-list-checks"
    >
      {{ t('operationCore.workspaceDefinitions.forms.rulesOpenRulesTab') }}
    </v-btn>

    <OcWorkspaceFormTransitionRequirements
      :workspace-id="workspaceId"
      :default-state-flow-id="defaultStateFlowId"
      :state-flows="stateFlows ?? []"
      :layout-field-items="layoutFieldItems"
    />
  </div>
</template>

<style scoped>
.oc-form-field-policy-table :deep(th),
.oc-form-field-policy-table :deep(td) {
  vertical-align: middle;
}

.oc-form-field-policy-table__col-field {
  min-width: 140px;
}

.oc-form-field-policy-table__col-flag {
  width: 72px;
  text-align: center;
}

.oc-form-field-policy-table__col-default {
  min-width: 180px;
}

.oc-form-field-policy-table__default-cell {
  min-width: 160px;
}

.oc-form-field-policy-table__row--hidden-field {
  opacity: 0.72;
  background: rgba(var(--v-theme-on-surface), 0.03);
}
</style>
