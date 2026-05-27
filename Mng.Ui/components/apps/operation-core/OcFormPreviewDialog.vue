<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcDynamicForm from '@/components/apps/operation-core/OcDynamicForm.vue';
import type { OcFormRuntimeContext } from '@/types/apps/operationCore';
import { normalizeOcDialogMaxWidthPx } from '@/utils/ocFormLayout';

const props = defineProps<{
  modelValue: boolean;
  context: OcFormRuntimeContext | null;
  formValues: Record<string, unknown>;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  'update:formValues': [Record<string, unknown>];
}>();

const { t } = useAppI18n();

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const formValuesModel = computed({
  get: () => props.formValues,
  set: (v: Record<string, unknown>) => emit('update:formValues', v),
});

const formTitle = computed(() => props.context?.formName?.trim() || '—');
const sectionCount = computed(() => props.context?.layout?.sections?.length ?? 0);
const fieldCount = computed(() => Object.keys(props.context?.fields ?? {}).length);

const dialogMaxWidthPx = computed(() =>
  normalizeOcDialogMaxWidthPx(props.context?.layout?.dialogMaxWidth)
);

const innerFrameMaxWidthPx = computed(() => Math.max(400, dialogMaxWidthPx.value - 96));
</script>

<template>
  <v-dialog
    v-model="open"
    :max-width="dialogMaxWidthPx"
    scrollable
    content-class="oc-form-preview-dialog"
  >
    <v-card rounded="xl" elevation="8" class="oc-form-preview-dialog__card overflow-hidden">
      <div class="oc-form-preview-dialog__header px-5 py-4">
        <div class="d-flex align-start gap-3">
          <v-avatar color="primary" variant="tonal" size="44" rounded="lg">
            <v-icon icon="mdi-eye-outline" size="24" />
          </v-avatar>
          <div class="flex-grow-1 min-width-0">
            <div class="d-flex flex-wrap align-center gap-2 mb-1">
              <span class="text-overline text-medium-emphasis">
                {{ t('operationCore.workspaceDefinitions.forms.previewTitle') }}
              </span>
              <v-chip size="x-small" color="primary" variant="flat" class="text-none font-weight-medium">
                {{ t('operationCore.formUi.previewDraftChip') }}
              </v-chip>
            </div>
            <h2 class="text-h6 font-weight-bold text-truncate">
              {{ formTitle }}
            </h2>
            <p class="text-caption text-medium-emphasis mb-0 mt-1">
              {{ t('operationCore.workspaceDefinitions.forms.previewDraftHint') }}
            </p>
          </div>
          <v-btn icon variant="text" size="small" @click="open = false">
            <v-icon icon="mdi-close" />
          </v-btn>
        </div>
        <div v-if="context" class="d-flex flex-wrap gap-2 mt-3">
          <v-chip size="small" variant="outlined" prepend-icon="mdi-view-dashboard-outline">
            {{ sectionCount }} {{ t('operationCore.workspaceDefinitions.forms.previewStatSections') }}
          </v-chip>
          <v-chip size="small" variant="outlined" prepend-icon="mdi-form-textbox">
            {{ fieldCount }} {{ t('operationCore.workspaceDefinitions.forms.previewStatFields') }}
          </v-chip>
          <v-chip size="small" variant="outlined" prepend-icon="mdi-arrow-expand-horizontal">
            {{ dialogMaxWidthPx }} px
          </v-chip>
        </div>
      </div>

      <v-divider />

      <v-card-text class="pa-0">
        <div class="oc-form-preview-dialog__body px-5 py-5">
          <div class="oc-form-preview-dialog__frame rounded-xl pa-4 pa-md-5">
            <OcDynamicForm
              v-if="context"
              v-model="formValuesModel"
              :context="context"
              readonly
              preview
            />
          </div>
        </div>
      </v-card-text>

      <v-divider />

      <v-card-actions class="px-5 py-3 bg-surface">
        <v-spacer />
        <v-btn variant="flat" color="primary" rounded="lg" class="text-none px-6" @click="open = false">
          {{ t('operationCore.definitions.close') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.oc-form-preview-dialog__header {
  background: linear-gradient(
    135deg,
    rgba(var(--v-theme-primary), 0.08) 0%,
    rgba(var(--v-theme-surface), 1) 55%
  );
}

.oc-form-preview-dialog__body {
  background: rgba(var(--v-theme-on-surface), 0.03);
  max-height: min(72vh, 640px);
  overflow-y: auto;
}

.oc-form-preview-dialog__frame {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  box-shadow:
    0 1px 2px rgba(0, 0, 0, 0.04),
    0 8px 24px rgba(0, 0, 0, 0.06);
  max-width: v-bind('`${innerFrameMaxWidthPx}px`');
  margin-left: auto;
  margin-right: auto;
}

.min-width-0 {
  min-width: 0;
}
</style>
