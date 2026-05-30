<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import OcDynamicForm from '@/components/apps/operation-core/OcDynamicForm.vue';
import {
  buildCreateWorkItemRequest,
  buildUpdateWorkItemRequest,
  collectOcFormValidationIssues,
  hasUpdateWorkItemChanges,
  initialFormModelFromContext,
  ocCreateWorkItem,
  ocExtractDgErrorMessage,
  ocGetFormCreateContext,
  ocGetFormEditContext,
  ocListBoardsForWorkspace,
  ocListPoolFieldsForWorkspace,
  ocUpdateWorkItem,
} from '@/services/operationCoreService';
import type { OcFormRuntimeContext } from '@/types/apps/operationCore';
import { enrichFormRuntimeFields } from '@/utils/ocFormFieldLabels';
import { normalizeOcDialogMaxWidthPx } from '@/utils/ocFormLayout';

type DialogMode = 'create' | 'edit';

const props = defineProps<{
  modelValue: boolean;
  mode: DialogMode;
  workspaceId?: string | null;
  boardId?: string | null;
  formId?: string | null;
  workItemId?: string | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  saved: [{ id: string; key: string; mode: DialogMode }];
}>();

const { t } = useAppI18n();

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const loading = ref(false);
const submitting = ref(false);
const errorLocal = ref<string | null>(null);
const formContext = ref<OcFormRuntimeContext | null>(null);
const formModel = ref<Record<string, unknown>>({});
const initialModel = ref<Record<string, unknown>>({});
const validationAttempted = ref(false);

const isEdit = computed(() => props.mode === 'edit');

const dialogMaxWidthPx = computed(() =>
  normalizeOcDialogMaxWidthPx(formContext.value?.layout?.dialogMaxWidth)
);

const dialogTitle = computed(() => {
  const name = formContext.value?.formName?.trim();
  const base = isEdit.value
    ? t('operationCore.workItemDialog.editTitle')
    : t('operationCore.workItemDialog.createTitle');
  return name ? `${base} — ${name}` : base;
});

const validationIssues = computed(() => {
  if (!formContext.value) return [];
  return collectOcFormValidationIssues(formContext.value, formModel.value);
});

const fieldErrors = computed(() => {
  if (!validationAttempted.value) return {} as Record<string, string>;
  const msg = t('operationCore.formUi.fieldRequired');
  const errors: Record<string, string> = {};
  for (const issue of validationIssues.value) {
    errors[issue.fieldKey] = msg;
  }
  return errors;
});

async function resolveCreateFormId(workspaceId: string): Promise<string | undefined> {
  if (props.formId?.trim()) return props.formId.trim();
  const bd = props.boardId?.trim();
  if (!bd) return undefined;
  const boards = await ocListBoardsForWorkspace(workspaceId);
  return boards.find((b) => b.__dataId === bd)?.defaultFormId ?? undefined;
}

async function loadForm() {
  errorLocal.value = null;
  validationAttempted.value = false;
  formContext.value = null;
  formModel.value = {};
  initialModel.value = {};

  loading.value = true;
  try {
    let ctx: OcFormRuntimeContext;
    if (isEdit.value) {
      const id = props.workItemId?.trim();
      if (!id) {
        errorLocal.value = t('operationCore.workItemDialog.missingWorkItem');
        return;
      }
      ctx = await ocGetFormEditContext(id);
    } else {
      const ws = props.workspaceId?.trim();
      if (!ws) {
        errorLocal.value = t('operationCore.create.missingWorkspace');
        return;
      }
      const formId = await resolveCreateFormId(ws);
      ctx = await ocGetFormCreateContext(ws, { formId });
    }

    if (ctx.permissions?.canEdit === false) {
      errorLocal.value = t(
        isEdit.value ? 'operationCore.workItemDialog.noEditPermission' : 'operationCore.create.noPermission'
      );
      return;
    }

    const poolFields = await ocListPoolFieldsForWorkspace(ctx.workspaceId);
    formContext.value = enrichFormRuntimeFields(ctx, { poolFields, translate: t });
    const model = initialFormModelFromContext(ctx);
    formModel.value = model;
    initialModel.value = JSON.parse(JSON.stringify(model));
  } catch (e: unknown) {
    formContext.value = null;
    errorLocal.value = ocExtractDgErrorMessage(e, t('operationCore.create.loadError'));
  } finally {
    loading.value = false;
  }
}

function collectChangedFields(): Record<string, unknown> {
  const changed: Record<string, unknown> = {};
  const keys = new Set([...Object.keys(formModel.value), ...Object.keys(initialModel.value)]);
  for (const key of keys) {
    const current = formModel.value[key];
    const before = initialModel.value[key];
    if (JSON.stringify(current ?? null) !== JSON.stringify(before ?? null)) {
      changed[key] = current;
    }
  }
  return changed;
}

async function submit() {
  if (!formContext.value) return;

  validationAttempted.value = true;
  if (validationIssues.value.length) {
    errorLocal.value = t('operationCore.create.validationRequired');
    return;
  }

  submitting.value = true;
  errorLocal.value = null;
  try {
    if (isEdit.value) {
      const id = props.workItemId?.trim();
      if (!id) return;
      const patch = buildUpdateWorkItemRequest(collectChangedFields());
      if (!hasUpdateWorkItemChanges(patch)) {
        open.value = false;
        return;
      }
      const updated = await ocUpdateWorkItem(id, patch);
      emit('saved', { id: updated.id || id, key: updated.key, mode: 'edit' });
    } else {
      const payload = buildCreateWorkItemRequest(
        formModel.value,
        formContext.value.workspaceId,
        props.boardId?.trim() || undefined
      );
      const created = await ocCreateWorkItem(payload);
      emit('saved', { id: created.id, key: created.key, mode: 'create' });
    }
    open.value = false;
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(e, t('operationCore.create.submitError'));
  } finally {
    submitting.value = false;
  }
}

watch(
  () => [props.modelValue, props.mode, props.workItemId, props.workspaceId, props.boardId, props.formId],
  () => {
    if (props.modelValue) void loadForm();
  },
  { immediate: true }
);

watch(
  formModel,
  () => {
    if (validationAttempted.value && validationIssues.value.length === 0) {
      errorLocal.value = null;
    }
  },
  { deep: true }
);
</script>

<template>
  <v-dialog v-model="open" :max-width="dialogMaxWidthPx" scrollable persistent content-class="oc-work-item-dialog">
    <v-card rounded="xl" elevation="8">
      <div class="oc-work-item-dialog__header px-5 py-4 d-flex align-start gap-3">
        <v-avatar color="primary" variant="tonal" size="44" rounded="lg">
          <v-icon :icon="isEdit ? 'mdi-pencil-outline' : 'mdi-plus'" size="24" />
        </v-avatar>
        <div class="flex-grow-1 min-width-0">
          <h2 class="text-h6 font-weight-bold text-truncate mb-0">{{ dialogTitle }}</h2>
          <p class="text-caption text-medium-emphasis mb-0 mt-1">
            {{ t('operationCore.create.subtitle') }}
          </p>
        </div>
        <v-btn icon variant="text" size="small" :disabled="submitting" @click="open = false">
          <v-icon icon="mdi-close" />
        </v-btn>
      </div>

      <v-divider />

      <v-card-text class="pa-0">
        <div class="oc-work-item-dialog__body px-5 py-5">
          <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

          <v-alert
            v-if="validationAttempted && validationIssues.length"
            type="warning"
            variant="tonal"
            class="mb-4 rounded-lg"
            :title="t('operationCore.create.validationSummaryTitle')"
          >
            <p class="text-body-2 mb-2">{{ t('operationCore.create.validationRequired') }}</p>
            <ul class="oc-work-item-dialog__validation mb-0 pl-4">
              <li v-for="issue in validationIssues" :key="issue.fieldKey">{{ issue.label }}</li>
            </ul>
          </v-alert>

          <v-alert
            v-if="errorLocal"
            type="error"
            variant="tonal"
            class="mb-4 rounded-lg"
            closable
            @click:close="errorLocal = null"
          >
            {{ errorLocal }}
          </v-alert>

          <OcDynamicForm
            v-if="formContext && !loading"
            v-model="formModel"
            :context="formContext"
            :field-errors="fieldErrors"
          />
        </div>
      </v-card-text>

      <v-divider />

      <v-card-actions class="px-5 py-3 bg-surface">
        <v-spacer />
        <v-btn variant="text" class="text-none" :disabled="submitting" @click="open = false">
          {{ t('operationCore.create.cancel') }}
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          rounded="lg"
          class="text-none px-6"
          :loading="submitting"
          :disabled="loading || submitting || !formContext || formContext?.permissions?.canEdit === false"
          @click="submit"
        >
          {{ isEdit ? t('operationCore.workItemDialog.save') : t('operationCore.create.submit') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.oc-work-item-dialog__header {
  background: linear-gradient(
    135deg,
    rgba(var(--v-theme-primary), 0.08) 0%,
    rgba(var(--v-theme-surface), 1) 55%
  );
}

.oc-work-item-dialog__body {
  background: rgba(var(--v-theme-on-surface), 0.02);
  max-height: min(74vh, 680px);
  overflow-x: hidden;
  overflow-y: auto;
}

.oc-work-item-dialog__validation {
  list-style: disc;
}

.min-width-0 {
  min-width: 0;
}
</style>
