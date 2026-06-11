<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcAutomationIdempotencyMode, OcAutomationRelationMode, OcFieldMapping } from '@/types/apps/operationCore';

export type OcAutomationFormModel = {
  name: string;
  description: string;
  isActive: boolean;
  sourceTypeId: string;
  sourceBoardId: string;
  transitionKey: string;
  conditionField: string;
  conditionValue: string;
  targetBoardId: string;
  targetTypeId: string;
  title: string;
  assignee: string;
  idempotencyMode: OcAutomationIdempotencyMode;
  relationMode: OcAutomationRelationMode;
  fieldMappings: OcFieldMapping[];
};

const props = defineProps<{
  modelValue: boolean;
  editId: string | null;
  boardItems: { value: string; title: string }[];
  typeItems: { value: string; title: string }[];
  transitionItems: { value: string; title: string }[];
  saving?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [OcAutomationFormModel];
}>();

const { t } = useAppI18n();
const validationError = ref<string | null>(null);

const form = ref<OcAutomationFormModel>(emptyForm());

function emptyForm(): OcAutomationFormModel {
  return {
    name: '',
    description: '',
    isActive: true,
    sourceTypeId: '',
    sourceBoardId: '',
    transitionKey: '',
    conditionField: '',
    conditionValue: '',
    targetBoardId: '',
    targetTypeId: '',
    title: '',
    assignee: '',
    idempotencyMode: 'none',
    relationMode: 'parent',
    fieldMappings: [
      { target: 'parentItemId', source: 'relation', relation: 'parent' },
    ],
  };
}

const mappingSourceItems = computed(() => [
  { value: 'field', title: t('operationCore.workspaceDefinitions.automations.mappingSourceField') },
  { value: 'static', title: t('operationCore.workspaceDefinitions.automations.mappingSourceStatic') },
  { value: 'relation', title: t('operationCore.workspaceDefinitions.automations.mappingSourceRelation') },
]);

const idempotencyItems = computed(() => [
  { value: 'none', title: t('operationCore.workspaceDefinitions.automations.idempotencyNone') },
  { value: 'one_per_source', title: t('operationCore.workspaceDefinitions.automations.idempotencyOnePerSource') },
]);

const relationItems = computed(() => [
  { value: 'parent', title: t('operationCore.workspaceDefinitions.automations.relationParent') },
  { value: 'none', title: t('operationCore.workspaceDefinitions.automations.relationNone') },
]);

function resetForm(model?: OcAutomationFormModel) {
  form.value = model ? structuredClone(model) : emptyForm();
  validationError.value = null;
}

function addMappingRow() {
  form.value.fieldMappings.push({ target: '', source: 'field', path: '' });
}

function removeMappingRow(index: number) {
  form.value.fieldMappings.splice(index, 1);
}

function validate(): boolean {
  if (!form.value.name.trim()) {
    validationError.value = t('operationCore.workspaceDefinitions.automations.validationName');
    return false;
  }
  if (!form.value.targetBoardId) {
    validationError.value = t('operationCore.workspaceDefinitions.automations.validationTargetBoard');
    return false;
  }
  if (!form.value.targetTypeId) {
    validationError.value = t('operationCore.workspaceDefinitions.automations.validationTargetType');
    return false;
  }
  if (!form.value.title.trim()) {
    validationError.value = t('operationCore.workspaceDefinitions.automations.validationTitle');
    return false;
  }
  validationError.value = null;
  return true;
}

function onSave() {
  if (!validate()) return;
  emit('save', structuredClone(form.value));
}

watch(
  () => props.modelValue,
  (open) => {
    if (!open) validationError.value = null;
  }
);

defineExpose({ resetForm });
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="920"
    scrollable
    persistent
    @update:model-value="emit('update:modelValue', $event)"
  >
    <v-card>
      <v-card-title class="d-flex align-center">
        {{
          editId
            ? t('operationCore.workspaceDefinitions.automations.editAutomation')
            : t('operationCore.workspaceDefinitions.automations.addAutomation')
        }}
      </v-card-title>
      <v-divider />
      <v-card-text style="max-height: 70vh">
        <v-alert v-if="validationError" type="error" variant="tonal" class="mb-4">
          {{ validationError }}
        </v-alert>

        <div class="text-subtitle-2 mb-2">
          {{ t('operationCore.workspaceDefinitions.automations.sectionGeneral') }}
        </div>
        <v-row dense>
          <v-col cols="12" md="8">
            <v-text-field
              v-model="form.name"
              :label="t('operationCore.workspaceDefinitions.automations.fieldName')"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="4" class="d-flex align-center">
            <v-switch
              v-model="form.isActive"
              :label="t('operationCore.workspaceDefinitions.automations.fieldActive')"
              color="primary"
              hide-details
            />
          </v-col>
          <v-col cols="12">
            <v-textarea
              v-model="form.description"
              :label="t('operationCore.workspaceDefinitions.automations.fieldDescription')"
              rows="2"
              auto-grow
            />
          </v-col>
        </v-row>

        <v-divider class="my-4" />
        <div class="text-subtitle-2 mb-2">
          {{ t('operationCore.workspaceDefinitions.automations.sectionTrigger') }}
        </div>
        <v-row dense>
          <v-col cols="12" md="6">
            <v-select
              v-model="form.sourceTypeId"
              :items="typeItems"
              :label="t('operationCore.workspaceDefinitions.automations.fieldSourceType')"
              clearable
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-select
              v-model="form.sourceBoardId"
              :items="boardItems"
              :label="t('operationCore.workspaceDefinitions.automations.fieldSourceBoard')"
              clearable
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-select
              v-model="form.transitionKey"
              :items="transitionItems"
              item-title="title"
              item-value="value"
              :label="t('operationCore.workspaceDefinitions.automations.fieldTransition')"
              clearable
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field
              v-model="form.conditionField"
              :label="t('operationCore.workspaceDefinitions.automations.fieldConditionField')"
              placeholder="fields.qualityResult"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field
              v-model="form.conditionValue"
              :label="t('operationCore.workspaceDefinitions.automations.fieldConditionValue')"
              density="comfortable"
            />
          </v-col>
        </v-row>

        <v-divider class="my-4" />
        <div class="text-subtitle-2 mb-2">
          {{ t('operationCore.workspaceDefinitions.automations.sectionAction') }}
        </div>
        <v-row dense>
          <v-col cols="12" md="6">
            <v-select
              v-model="form.targetBoardId"
              :items="boardItems"
              :label="t('operationCore.workspaceDefinitions.automations.fieldTargetBoard')"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-select
              v-model="form.targetTypeId"
              :items="typeItems"
              :label="t('operationCore.workspaceDefinitions.automations.fieldTargetType')"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12">
            <v-text-field
              v-model="form.title"
              :label="t('operationCore.workspaceDefinitions.automations.fieldTitle')"
              :hint="t('operationCore.workspaceDefinitions.automations.titleHint')"
              persistent-hint
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-text-field
              v-model="form.assignee"
              :label="t('operationCore.workspaceDefinitions.automations.fieldAssignee')"
              :hint="t('operationCore.workspaceDefinitions.automations.assigneeHint')"
              persistent-hint
              density="comfortable"
            />
          </v-col>
        </v-row>

        <div class="d-flex align-center justify-space-between mt-4 mb-2">
          <span class="text-subtitle-2">
            {{ t('operationCore.workspaceDefinitions.automations.fieldMappings') }}
          </span>
          <v-btn size="small" variant="tonal" prepend-icon="mdi-plus" @click="addMappingRow">
            {{ t('operationCore.workspaceDefinitions.automations.addMapping') }}
          </v-btn>
        </div>
        <div
          v-for="(row, idx) in form.fieldMappings"
          :key="idx"
          class="d-flex flex-wrap ga-2 mb-2 align-center"
        >
          <v-text-field
            v-model="row.target"
            :label="t('operationCore.workspaceDefinitions.automations.mappingTarget')"
            density="compact"
            style="min-width: 140px; flex: 1"
          />
          <v-select
            v-model="row.source"
            :items="mappingSourceItems"
            :label="t('operationCore.workspaceDefinitions.automations.mappingSource')"
            density="compact"
            style="min-width: 120px; flex: 1"
          />
          <v-text-field
            v-if="row.source === 'field'"
            v-model="row.path"
            label="path"
            placeholder="fields.lotSerial"
            density="compact"
            style="min-width: 160px; flex: 1"
          />
          <v-text-field
            v-else-if="row.source === 'static'"
            v-model="row.value"
            :label="t('operationCore.workspaceDefinitions.automations.mappingValue')"
            density="compact"
            style="min-width: 160px; flex: 1"
          />
          <v-btn
            icon="mdi-delete-outline"
            variant="text"
            size="small"
            @click="removeMappingRow(idx)"
          />
        </div>

        <v-divider class="my-4" />
        <v-row dense>
          <v-col cols="12" md="6">
            <v-select
              v-model="form.relationMode"
              :items="relationItems"
              :label="t('operationCore.workspaceDefinitions.automations.fieldRelation')"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-select
              v-model="form.idempotencyMode"
              :items="idempotencyItems"
              :label="t('operationCore.workspaceDefinitions.automations.fieldIdempotency')"
              density="comfortable"
            />
          </v-col>
        </v-row>
      </v-card-text>
      <v-divider />
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="emit('update:modelValue', false)">
          {{ t('operationCore.definitions.cancel') }}
        </v-btn>
        <v-btn color="primary" :loading="saving" @click="onSave">
          {{ t('operationCore.definitions.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
