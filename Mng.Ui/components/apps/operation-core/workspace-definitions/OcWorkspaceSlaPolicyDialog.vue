<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OpSlaPolicy } from '@/types/apps/operationCore';
import {
  buildSlaPolicyPayload,
  formatSlaTargetsSummary,
  newSlaPolicyDraft,
  parseOpSlaPolicyToDraft,
  validateSlaPolicyDraft,
  type OcSlaPolicyDraft,
} from '@/utils/ocSlaPolicies';

const props = defineProps<{
  modelValue: boolean;
  policy: OpSlaPolicy | null;
  workspaceId: string;
  typeItems: { value: string; title: string }[];
  priorityItems: { value: string; title: string }[];
  saving?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [Record<string, unknown>];
}>();

const { t } = useAppI18n();
const draft = ref<OcSlaPolicyDraft>(newSlaPolicyDraft());

const isEdit = computed(() => !!props.policy?.__dataId);
const canSave = computed(() => validateSlaPolicyDraft(draft.value) === null);

const typeSelectItems = computed(() => [
  { value: null, title: t('operationCore.workspaceDefinitions.sla.scopeAny') },
  ...props.typeItems,
]);

const prioritySelectItems = computed(() => [
  { value: null, title: t('operationCore.workspaceDefinitions.sla.scopeAny') },
  ...props.priorityItems,
]);

const previewTargets = computed(() =>
  formatSlaTargetsSummary(
    {
      responseTargetMinutes: draft.value.responseTargetMinutes,
      resolveTargetMinutes: draft.value.resolveTargetMinutes,
    },
    t('operationCore.workspaceDefinitions.sla.responseTarget'),
    t('operationCore.workspaceDefinitions.sla.resolveTarget')
  )
);

watch(
  () => [props.modelValue, props.policy?.__dataId] as const,
  ([open]) => {
    if (open) {
      draft.value = props.policy
        ? parseOpSlaPolicyToDraft(props.policy)
        : newSlaPolicyDraft();
    }
  }
);

function close() {
  emit('update:modelValue', false);
}

function submit() {
  if (!canSave.value) return;
  emit('save', buildSlaPolicyPayload(draft.value, props.workspaceId));
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="720" persistent @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center py-4">
        <v-icon icon="mdi-clock-check-outline" color="primary" class="me-2" />
        {{
          isEdit
            ? t('operationCore.workspaceDefinitions.sla.editPolicy')
            : t('operationCore.workspaceDefinitions.sla.addPolicy')
        }}
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" @click="close" />
      </v-card-title>
      <v-divider />
      <v-card-text class="pt-4">
        <v-alert type="info" variant="tonal" density="compact" class="mb-4 rounded-lg">
          {{ t('operationCore.workspaceDefinitions.sla.dialogHint') }}
        </v-alert>
        <v-text-field
          v-model="draft.name"
          :label="t('operationCore.workspaceDefinitions.sla.fieldName')"
          density="comfortable"
          class="mb-3"
        />
        <v-textarea
          v-model="draft.description"
          :label="t('operationCore.workspaceDefinitions.sla.fieldDescription')"
          rows="2"
          auto-grow
          density="comfortable"
          class="mb-3"
        />
        <v-row dense>
          <v-col cols="12" md="6">
            <v-select
              v-model="draft.typeId"
              :items="typeSelectItems"
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.sla.scopeType')"
              density="comfortable"
              clearable
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-select
              v-model="draft.priorityId"
              :items="prioritySelectItems"
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.sla.scopePriority')"
              density="comfortable"
              clearable
            />
          </v-col>
        </v-row>
        <v-row dense class="mt-1">
          <v-col cols="12" md="6">
            <v-text-field
              v-model.number="draft.responseTargetMinutes"
              type="number"
              min="0"
              :label="t('operationCore.workspaceDefinitions.sla.responseMinutes')"
              :hint="t('operationCore.workspaceDefinitions.sla.responseMinutesHint')"
              persistent-hint
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-text-field
              v-model.number="draft.resolveTargetMinutes"
              type="number"
              min="0"
              :label="t('operationCore.workspaceDefinitions.sla.resolveMinutes')"
              :hint="t('operationCore.workspaceDefinitions.sla.resolveMinutesHint')"
              persistent-hint
              density="comfortable"
            />
          </v-col>
        </v-row>
        <v-text-field
          v-model.number="draft.policyPriority"
          type="number"
          :label="t('operationCore.workspaceDefinitions.sla.policyPriority')"
          :hint="t('operationCore.workspaceDefinitions.sla.policyPriorityHint')"
          persistent-hint
          density="comfortable"
          class="mt-3"
        />
        <v-switch
          v-model="draft.isActive"
          color="primary"
          :label="t('operationCore.workspaceDefinitions.sla.isActive')"
          hide-details
          class="mt-2"
        />
        <v-sheet variant="tonal" color="primary" rounded="lg" class="pa-3 mt-4">
          <div class="text-caption text-medium-emphasis mb-1">
            {{ t('operationCore.workspaceDefinitions.sla.previewTitle') }}
          </div>
          <div class="text-body-2">{{ previewTargets }}</div>
        </v-sheet>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="close">{{ t('operationCore.workspaceDefinitions.sla.cancel') }}</v-btn>
        <v-btn color="primary" variant="flat" :loading="saving" :disabled="!canSave" @click="submit">
          {{ t('operationCore.workspaceDefinitions.sla.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
