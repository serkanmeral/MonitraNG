<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  newQueryStep,
  newQueryStepMapping,
  newQueryStepParameter,
  type OcQueryStep,
  type OcQueryStepMapping,
  type OcQueryStepOnEmpty,
  type OcQueryStepParameter,
} from '@/utils/ocWorkspaceRules';

const props = defineProps<{
  modelValue: OcQueryStep[];
  conditionFieldItems: { key: string; label: string }[];
}>();

const emit = defineEmits<{
  'update:modelValue': [OcQueryStep[]];
}>();

const { t } = useAppI18n();

const steps = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value),
});

const onEmptyItems = computed(() => [
  { value: 'zero', title: t('operationCore.workspaceDefinitions.rules.querySteps.onEmptyZero') },
  { value: 'false', title: t('operationCore.workspaceDefinitions.rules.querySteps.onEmptyFalse') },
  { value: 'fail', title: t('operationCore.workspaceDefinitions.rules.querySteps.onEmptyFail') },
  { value: 'skip', title: t('operationCore.workspaceDefinitions.rules.querySteps.onEmptySkip') },
]);

function patchStep(id: string, partial: Partial<OcQueryStep>) {
  emit(
    'update:modelValue',
    props.modelValue.map((step) => (step.id === id ? { ...step, ...partial } : step))
  );
}

function addStep() {
  emit('update:modelValue', [...props.modelValue, newQueryStep()]);
}

function removeStep(id: string) {
  emit(
    'update:modelValue',
    props.modelValue.filter((step) => step.id !== id)
  );
}

function moveStep(index: number, direction: -1 | 1) {
  const next = index + direction;
  if (next < 0 || next >= props.modelValue.length) return;
  const copy = [...props.modelValue];
  const [item] = copy.splice(index, 1);
  copy.splice(next, 0, item);
  emit('update:modelValue', copy);
}

function patchParameter(step: OcQueryStep, id: string, partial: Partial<OcQueryStepParameter>) {
  patchStep(step.id, {
    parameters: step.parameters.map((item) => (item.id === id ? { ...item, ...partial } : item)),
  });
}

function patchMapping(step: OcQueryStep, id: string, partial: Partial<OcQueryStepMapping>) {
  patchStep(step.id, {
    mappings: step.mappings.map((item) => (item.id === id ? { ...item, ...partial } : item)),
  });
}
</script>

<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact" class="mb-3 rounded-lg">
      {{ t('operationCore.workspaceDefinitions.rules.querySteps.hint') }}
    </v-alert>

    <v-card
      v-for="(step, index) in steps"
      :key="step.id"
      variant="outlined"
      class="mb-3 pa-3"
    >
      <div class="d-flex align-center ga-2 mb-3">
        <span class="text-subtitle-2">{{ index + 1 }}</span>
        <v-spacer />
        <v-btn icon variant="text" size="small" :disabled="index === 0" @click="moveStep(index, -1)">
          <v-icon icon="mdi-arrow-up" />
        </v-btn>
        <v-btn
          icon
          variant="text"
          size="small"
          :disabled="index === steps.length - 1"
          @click="moveStep(index, 1)"
        >
          <v-icon icon="mdi-arrow-down" />
        </v-btn>
        <v-btn icon variant="text" size="small" color="error" @click="removeStep(step.id)">
          <v-icon icon="mdi-delete-outline" />
        </v-btn>
      </div>
      <v-row dense>
        <v-col cols="12" sm="6">
          <v-text-field
            :model-value="step.dataset"
            :label="t('operationCore.workspaceDefinitions.rules.querySteps.dataset')"
            variant="outlined"
            density="comfortable"
            @update:model-value="patchStep(step.id, { dataset: String($event ?? '') })"
          />
        </v-col>
        <v-col cols="12" sm="6">
          <v-text-field
            :model-value="step.query"
            :label="t('operationCore.workspaceDefinitions.rules.querySteps.query')"
            variant="outlined"
            density="comfortable"
            @update:model-value="patchStep(step.id, { query: String($event ?? '') })"
          />
        </v-col>
      </v-row>

      <div class="text-caption text-medium-emphasis mb-2">
        {{ t('operationCore.workspaceDefinitions.rules.querySteps.parameters') }}
      </div>
      <div v-for="param in step.parameters" :key="param.id" class="d-flex ga-2 mb-2">
        <v-text-field
          :model-value="param.name"
          :label="t('operationCore.workspaceDefinitions.rules.querySteps.parameterName')"
          variant="outlined"
          density="compact"
          hide-details
          @update:model-value="patchParameter(step, param.id, { name: String($event ?? '') })"
        />
        <v-select
          :model-value="param.from"
          :items="conditionFieldItems"
          item-title="label"
          item-value="key"
          :label="t('operationCore.workspaceDefinitions.rules.querySteps.parameterFrom')"
          variant="outlined"
          density="compact"
          hide-details
          @update:model-value="patchParameter(step, param.id, { from: String($event ?? '') })"
        />
        <v-btn icon variant="text" size="small" @click="patchStep(step.id, { parameters: step.parameters.filter((p) => p.id !== param.id) })">
          <v-icon icon="mdi-close" />
        </v-btn>
      </div>
      <v-btn
        variant="text"
        size="small"
        class="mb-3"
        @click="patchStep(step.id, { parameters: [...step.parameters, newQueryStepParameter()] })"
      >
        {{ t('operationCore.workspaceDefinitions.rules.querySteps.addParameter') }}
      </v-btn>

      <div class="text-caption text-medium-emphasis mb-2">
        {{ t('operationCore.workspaceDefinitions.rules.querySteps.mappings') }}
      </div>
      <div v-for="mapping in step.mappings" :key="mapping.id" class="d-flex ga-2 mb-2">
        <v-text-field
          :model-value="mapping.from"
          :label="t('operationCore.workspaceDefinitions.rules.querySteps.mappingFrom')"
          variant="outlined"
          density="compact"
          hide-details
          @update:model-value="patchMapping(step, mapping.id, { from: String($event ?? '') })"
        />
        <v-select
          :model-value="mapping.to"
          :items="conditionFieldItems"
          item-title="label"
          item-value="key"
          :label="t('operationCore.workspaceDefinitions.rules.querySteps.mappingTo')"
          variant="outlined"
          density="compact"
          hide-details
          @update:model-value="patchMapping(step, mapping.id, { to: String($event ?? '') })"
        />
        <v-select
          :model-value="mapping.onEmpty"
          :items="onEmptyItems"
          item-title="title"
          item-value="value"
          :label="t('operationCore.workspaceDefinitions.rules.querySteps.onEmpty')"
          variant="outlined"
          density="compact"
          hide-details
          style="max-width: 180px"
          @update:model-value="patchMapping(step, mapping.id, { onEmpty: $event as OcQueryStepOnEmpty })"
        />
        <v-btn icon variant="text" size="small" @click="patchStep(step.id, { mappings: step.mappings.filter((m) => m.id !== mapping.id) })">
          <v-icon icon="mdi-close" />
        </v-btn>
      </div>
      <v-btn
        variant="text"
        size="small"
        @click="patchStep(step.id, { mappings: [...step.mappings, newQueryStepMapping()] })"
      >
        {{ t('operationCore.workspaceDefinitions.rules.querySteps.addMapping') }}
      </v-btn>
    </v-card>

    <v-btn variant="tonal" @click="addStep">
      {{ t('operationCore.workspaceDefinitions.rules.querySteps.add') }}
    </v-btn>
  </div>
</template>
