<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcWorkspaceRuleScope } from '@/utils/ocWorkspaceRules';

const props = defineProps<{
  scope: OcWorkspaceRuleScope;
  typeItems: { value: string; title: string }[];
  boardItems: { value: string; title: string }[];
  stateItems: { value: string; title: string }[];
  transitionItems: { value: string; title: string }[];
  showTransitionKey: boolean;
}>();

const emit = defineEmits<{
  'update:scope': [OcWorkspaceRuleScope];
}>();

const { t } = useAppI18n();

const local = computed({
  get: () => props.scope,
  set: (v: OcWorkspaceRuleScope) => emit('update:scope', v),
});

function patchScope(patch: Partial<OcWorkspaceRuleScope>) {
  emit('update:scope', { ...props.scope, ...patch });
}

const optionalItems = computed(() => [{ value: '', title: '—' }]);
</script>

<template>
  <div class="oc-rule-scope-panel">
    <p class="text-caption text-medium-emphasis mb-3">
      {{ t('operationCore.workspaceDefinitions.rules.scopeHint') }}
    </p>
    <v-row dense>
      <v-col cols="12" sm="6">
        <v-select
          :model-value="local.typeId ?? ''"
          :items="[...optionalItems, ...typeItems]"
          item-title="title"
          item-value="value"
          :label="t('operationCore.workspaceDefinitions.rules.scopeType')"
          density="comfortable"
          clearable
          @update:model-value="patchScope({ typeId: $event || undefined })"
        />
      </v-col>
      <v-col cols="12" sm="6">
        <v-select
          :model-value="local.boardId ?? ''"
          :items="[...optionalItems, ...boardItems]"
          item-title="title"
          item-value="value"
          :label="t('operationCore.workspaceDefinitions.rules.scopeBoard')"
          density="comfortable"
          clearable
          @update:model-value="patchScope({ boardId: $event || undefined })"
        />
      </v-col>
      <v-col cols="12" sm="6">
        <v-select
          :model-value="local.stateId ?? ''"
          :items="[...optionalItems, ...stateItems]"
          item-title="title"
          item-value="value"
          :label="t('operationCore.workspaceDefinitions.rules.scopeState')"
          density="comfortable"
          clearable
          @update:model-value="patchScope({ stateId: $event || undefined })"
        />
      </v-col>
      <v-col v-if="showTransitionKey" cols="12" sm="6">
        <v-combobox
          :model-value="local.transitionKey ?? ''"
          :items="transitionItems"
          item-title="title"
          item-value="value"
          :label="t('operationCore.workspaceDefinitions.rules.scopeTransition')"
          density="comfortable"
          clearable
          @update:model-value="patchScope({ transitionKey: String($event || '') || undefined })"
        />
      </v-col>
      <v-col cols="12" sm="6">
        <v-select
          :model-value="local.fromStateId ?? ''"
          :items="[...optionalItems, ...stateItems]"
          item-title="title"
          item-value="value"
          :label="t('operationCore.workspaceDefinitions.rules.scopeFromState')"
          density="comfortable"
          clearable
          @update:model-value="patchScope({ fromStateId: $event || undefined })"
        />
      </v-col>
      <v-col cols="12" sm="6">
        <v-select
          :model-value="local.toStateId ?? ''"
          :items="[...optionalItems, ...stateItems]"
          item-title="title"
          item-value="value"
          :label="t('operationCore.workspaceDefinitions.rules.scopeToState')"
          density="comfortable"
          clearable
          @update:model-value="patchScope({ toStateId: $event || undefined })"
        />
      </v-col>
    </v-row>
  </div>
</template>
