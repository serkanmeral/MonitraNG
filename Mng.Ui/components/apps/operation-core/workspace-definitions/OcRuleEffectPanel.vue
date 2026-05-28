<script setup lang="ts">
import { computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcPersonPickerAutocomplete from '@/components/apps/operation-core/OcPersonPickerAutocomplete.vue';
import OcFormPolicyDefaultValueInput from '@/components/apps/operation-core/workspace-definitions/OcFormPolicyDefaultValueInput.vue';
import type { OcConditionFieldOption } from '@/utils/ocConditionClauses';
import type {
  OcWorkspaceDefaultAction,
  OcWorkspaceRuleApplyMode,
  OcWorkspaceRuleType,
} from '@/utils/ocWorkspaceRules';

const props = defineProps<{
  ruleType: OcWorkspaceRuleType;
  applyMode: OcWorkspaceRuleApplyMode;
  errorMessage: string;
  defaultAction: OcWorkspaceDefaultAction;
  defaultField: string;
  defaultValue: unknown;
  assignee: string;
  conditionFieldItems: OcConditionFieldOption[];
  workspaceId: string;
  typeItems?: { value: string; title: string }[];
  priorityItems?: { value: string; title: string }[];
  stateItems?: { value: string; title: string }[];
  boardItems?: { value: string; title: string }[];
}>();

const emit = defineEmits<{
  'update:applyMode': [OcWorkspaceRuleApplyMode];
  'update:errorMessage': [string];
  'update:defaultAction': [OcWorkspaceDefaultAction];
  'update:defaultField': [string];
  'update:defaultValue': [unknown];
  'update:assignee': [string];
}>();

const { t } = useAppI18n();

const applyModeItems = computed(() => [
  { value: 'pre', title: t('operationCore.workspaceDefinitions.rules.applyModePre') },
  { value: 'post', title: t('operationCore.workspaceDefinitions.rules.applyModePost') },
]);

const defaultActionItems = computed(() => [
  { value: 'setField', title: t('operationCore.workspaceDefinitions.forms.rulesActionSetField') },
  { value: 'setAssignee', title: t('operationCore.workspaceDefinitions.forms.rulesActionSetAssignee') },
]);

const defaultFieldItems = computed(() =>
  props.conditionFieldItems.map((f) => ({ value: f.key, title: f.label }))
);

const defaultFieldMeta = computed(() =>
  props.conditionFieldItems.find((f) => f.key === props.defaultField)
);

watch(
  () => props.defaultAction,
  () => {
    emit('update:defaultValue', null);
    emit('update:assignee', '');
  }
);
</script>

<template>
  <div class="oc-rule-effect-panel">
    <template v-if="ruleType === 'validation'">
      <v-select
        :model-value="applyMode"
        :items="applyModeItems"
        item-title="title"
        item-value="value"
        :label="t('operationCore.workspaceDefinitions.rules.applyMode')"
        density="comfortable"
        class="mb-3"
        @update:model-value="emit('update:applyMode', $event)"
      />
      <v-textarea
        :model-value="errorMessage"
        :label="t('operationCore.workspaceDefinitions.forms.rulesErrorMessage')"
        rows="2"
        auto-grow
        density="comfortable"
        @update:model-value="emit('update:errorMessage', $event)"
      />
    </template>
    <template v-else>
      <v-select
        :model-value="defaultAction"
        :items="defaultActionItems"
        item-title="title"
        item-value="value"
        :label="t('operationCore.workspaceDefinitions.forms.rulesDefaultAction')"
        density="comfortable"
        class="mb-3"
        @update:model-value="emit('update:defaultAction', $event)"
      />
      <v-select
        v-if="defaultAction === 'setField'"
        :model-value="defaultField"
        :items="defaultFieldItems"
        item-title="title"
        item-value="value"
        :label="t('operationCore.workspaceDefinitions.forms.rulesDefaultField')"
        density="comfortable"
        class="mb-3"
        @update:model-value="emit('update:defaultField', $event)"
      />
      <OcPersonPickerAutocomplete
        v-if="defaultAction === 'setAssignee'"
        :model-value="assignee"
        density="comfortable"
        class="mb-3"
        @update:model-value="emit('update:assignee', $event)"
      />
      <OcFormPolicyDefaultValueInput
        v-else-if="defaultAction === 'setField' && defaultFieldMeta"
        :model-value="defaultValue"
        :field-key="defaultField"
        :field-type="defaultFieldMeta.fieldType"
        :relation-dataset="defaultFieldMeta.relationDataset"
        :cardinality="defaultFieldMeta.cardinality"
        :workspace-id="workspaceId"
        :type-items="typeItems"
        :priority-items="priorityItems"
        :state-items="stateItems"
        :board-items="boardItems"
        :field-label="t('operationCore.workspaceDefinitions.forms.rulesDefaultValue')"
        density="comfortable"
        control-variant="outlined"
        @update:model-value="emit('update:defaultValue', $event)"
      />
    </template>
  </div>
</template>
