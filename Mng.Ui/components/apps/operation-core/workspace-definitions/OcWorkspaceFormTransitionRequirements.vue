<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OpStateFlow } from '@/types/apps/operationCore';
import type { OcFormPolicyLayoutFieldItem } from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceFormFieldPolicyEditor.vue';

const props = defineProps<{
  workspaceId: string;
  defaultStateFlowId?: string;
  stateFlows: OpStateFlow[];
  layoutFieldItems: OcFormPolicyLayoutFieldItem[];
}>();

const { t } = useAppI18n();

function fieldLabel(key: string): string {
  const item = props.layoutFieldItems.find((i) => i.value === key);
  return item?.displayLabel ?? item?.title ?? key;
}

const selectedFlow = computed(() => {
  const id = props.defaultStateFlowId?.trim();
  if (!id) return null;
  return props.stateFlows.find((f) => f.__dataId === id) ?? null;
});

const transitionRows = computed(() => {
  const flow = selectedFlow.value;
  if (!flow) return [];
  return flow.transitions
    .filter((tr) => tr.requiredFields?.length)
    .map((tr) => ({
      key: tr.transitionKey,
      label: tr.label?.trim() || tr.transitionKey,
      fields: (tr.requiredFields ?? []).map((f) => fieldLabel(f)),
    }));
});

const flowsLink = computed(() => {
  const qs = new URLSearchParams();
  if (props.workspaceId) qs.set('workspaceId', props.workspaceId);
  qs.set('tab', 'flows');
  return `/apps/operation-core/admin/workspace-definitions?${qs.toString()}`;
});
</script>

<template>
  <div class="oc-form-transition-requirements mt-8">
    <v-divider class="mb-6" />

    <h4 class="text-subtitle-2 font-weight-medium mb-1">
      {{ t('operationCore.workspaceDefinitions.forms.transitionReqTitle') }}
    </h4>
    <p class="text-caption text-medium-emphasis mb-3">
      {{ t('operationCore.workspaceDefinitions.forms.transitionReqHint') }}
    </p>

    <v-alert v-if="!defaultStateFlowId?.trim()" type="info" variant="tonal" density="compact" class="rounded-lg mb-3">
      {{ t('operationCore.workspaceDefinitions.forms.transitionReqNoFlow') }}
    </v-alert>

    <template v-else-if="selectedFlow">
      <p class="text-body-2 mb-2">
        {{ t('operationCore.workspaceDefinitions.forms.transitionReqFlowLabel') }}:
        <strong>{{ selectedFlow.name }}</strong>
      </p>

      <v-table v-if="transitionRows.length" density="compact" class="rounded-lg border mb-3">
        <thead>
          <tr>
            <th>{{ t('operationCore.workspaceDefinitions.forms.transitionReqColTransition') }}</th>
            <th>{{ t('operationCore.workspaceDefinitions.forms.transitionReqColFields') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in transitionRows" :key="row.key">
            <td class="text-body-2">
              <div class="font-weight-medium">{{ row.label }}</div>
              <div class="text-caption text-medium-emphasis">{{ row.key }}</div>
            </td>
            <td>
              <div class="d-flex flex-wrap gap-1">
                <v-chip v-for="f in row.fields" :key="f" size="x-small" variant="outlined">
                  {{ f }}
                </v-chip>
              </div>
            </td>
          </tr>
        </tbody>
      </v-table>

      <v-alert v-else type="info" variant="tonal" density="compact" class="rounded-lg mb-3">
        {{ t('operationCore.workspaceDefinitions.forms.transitionReqNone') }}
      </v-alert>
    </template>

    <v-btn
      :to="flowsLink"
      variant="text"
      color="primary"
      size="small"
      class="text-none px-0"
      prepend-icon="mdi-transit-connection-variant"
    >
      {{ t('operationCore.workspaceDefinitions.forms.transitionReqOpenFlows') }}
    </v-btn>
  </div>
</template>
