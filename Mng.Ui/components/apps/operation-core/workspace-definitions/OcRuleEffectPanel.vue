<script setup lang="ts">
import { computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcPersonPickerAutocomplete from '@/components/apps/operation-core/OcPersonPickerAutocomplete.vue';
import OcFormPolicyDefaultValueInput from '@/components/apps/operation-core/workspace-definitions/OcFormPolicyDefaultValueInput.vue';
import type { OcConditionFieldOption } from '@/utils/ocConditionClauses';
import type {
  OcWorkspaceAutomationAction,
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
  automationAction: OcWorkspaceAutomationAction;
  watcher: string;
  templateKey: string;
  recipients: string;
  activitySummary: string;
  activityType: string;
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
  'update:automationAction': [OcWorkspaceAutomationAction];
  'update:watcher': [string];
  'update:templateKey': [string];
  'update:recipients': [string];
  'update:activitySummary': [string];
  'update:activityType': [string];
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

const automationActionItems = computed(() => [
  { value: 'createActivity', title: t('operationCore.workspaceDefinitions.rules.automationCreateActivity') },
  { value: 'addWatcher', title: t('operationCore.workspaceDefinitions.rules.automationAddWatcher') },
  { value: 'createNotification', title: t('operationCore.workspaceDefinitions.rules.automationCreateNotification') },
  {
    value: 'sendEmailViaMngNotifiers',
    title: t('operationCore.workspaceDefinitions.rules.automationSendEmail'),
  },
]);

const defaultFieldItems = computed(() =>
  props.conditionFieldItems.map((f) => ({ value: f.key, title: f.label }))
);

const defaultFieldMeta = computed(() =>
  props.conditionFieldItems.find((f) => f.key === props.defaultField)
);

const showTemplateFields = computed(
  () =>
    props.automationAction === 'createNotification' ||
    props.automationAction === 'sendEmailViaMngNotifiers'
);

watch(
  () => props.defaultAction,
  () => {
    emit('update:defaultValue', null);
    emit('update:assignee', '');
  }
);

watch(
  () => props.automationAction,
  () => {
    emit('update:watcher', '');
    emit('update:templateKey', '');
    emit('update:recipients', '');
    emit('update:activitySummary', '');
    emit('update:activityType', 'RuleAction');
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

    <template v-else-if="ruleType === 'automation'">
      <v-alert type="info" variant="tonal" density="compact" class="mb-4 rounded-lg">
        {{ t('operationCore.workspaceDefinitions.rules.automationHint') }}
      </v-alert>
      <v-select
        :model-value="automationAction"
        :items="automationActionItems"
        item-title="title"
        item-value="value"
        :label="t('operationCore.workspaceDefinitions.rules.automationAction')"
        density="comfortable"
        class="mb-3"
        @update:model-value="emit('update:automationAction', $event)"
      />
      <OcPersonPickerAutocomplete
        v-if="automationAction === 'addWatcher'"
        :model-value="watcher"
        density="comfortable"
        class="mb-3"
        @update:model-value="emit('update:watcher', $event)"
      />
      <template v-if="showTemplateFields">
        <v-text-field
          :model-value="templateKey"
          :label="t('operationCore.workspaceDefinitions.rules.automationTemplateKey')"
          density="comfortable"
          class="mb-3"
          @update:model-value="emit('update:templateKey', $event)"
        />
        <v-text-field
          :model-value="recipients"
          :label="t('operationCore.workspaceDefinitions.rules.automationRecipients')"
          :hint="t('operationCore.workspaceDefinitions.rules.automationRecipientsHint')"
          persistent-hint
          density="comfortable"
          class="mb-3"
          @update:model-value="emit('update:recipients', $event)"
        />
      </template>
      <template v-if="automationAction === 'createActivity'">
        <v-textarea
          :model-value="activitySummary"
          :label="t('operationCore.workspaceDefinitions.rules.automationActivitySummary')"
          rows="2"
          auto-grow
          density="comfortable"
          class="mb-3"
          @update:model-value="emit('update:activitySummary', $event)"
        />
        <v-text-field
          :model-value="activityType"
          :label="t('operationCore.workspaceDefinitions.rules.automationActivityType')"
          density="comfortable"
          @update:model-value="emit('update:activityType', $event)"
        />
      </template>
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
