<script setup lang="ts">
import { OC_CONDITION_OPERATORS_EQ_NE } from '@/utils/ocConditionClauses';
import OcConditionClauseList from '@/components/apps/operation-core/workspace-definitions/OcConditionClauseList.vue';
import type {
  OcPolicyConditionFieldOption,
  OcWorkspacePolicyConditionClause,
} from '@/utils/ocWorkspaceFieldPolicies';

defineProps<{
  workspaceId: string;
  conditionFields: OcPolicyConditionFieldOption[];
  typeItems?: { value: string; title: string }[];
  priorityItems?: { value: string; title: string }[];
  stateItems?: { value: string; title: string }[];
  boardItems?: { value: string; title: string }[];
}>();

const clauses = defineModel<OcWorkspacePolicyConditionClause[]>({ required: true });
</script>

<template>
  <OcConditionClauseList
    v-model="clauses"
    label-mode="policies"
    :operators="OC_CONDITION_OPERATORS_EQ_NE"
    :workspace-id="workspaceId"
    :condition-fields="conditionFields"
    :type-items="typeItems"
    :priority-items="priorityItems"
    :state-items="stateItems"
    :board-items="boardItems"
  />
</template>
