<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcDynamicForm from '@/components/apps/operation-core/OcDynamicForm.vue';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useOperationCoreStore } from '@/stores/apps/operationCore';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  buildCreateWorkItemRequest,
  collectOcFormValidationIssues,
  initialFormModelFromContext,
  ocCreateWorkItem,
  ocExtractDgErrorMessage,
  ocGetFormCreateContext,
  ocListBoardsForWorkspace,
  ocListPoolFieldsForWorkspace,
} from '@/services/operationCoreService';
import type { OcFormRuntimeContext } from '@/types/apps/operationCore';
import { enrichFormRuntimeFields } from '@/utils/ocFormFieldLabels';
import { normalizeOcDialogMaxWidthPx } from '@/utils/ocFormLayout';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();
const store = useOperationCoreStore();

const workspaceId = computed(() =>
  typeof route.query.workspaceId === 'string' ? route.query.workspaceId.trim() : ''
);
const boardId = computed(() => (typeof route.query.boardId === 'string' ? route.query.boardId.trim() : ''));
const formIdQuery = computed(() => (typeof route.query.formId === 'string' ? route.query.formId.trim() : ''));

const loading = ref(false);
const submitting = ref(false);
const errorLocal = ref<string | null>(null);
const formContext = ref<OcFormRuntimeContext | null>(null);
const formModel = ref<Record<string, unknown>>({});
const resolvedFormId = ref<string | undefined>(undefined);
const validationAttempted = ref(false);

const workspaceSegment = computed(() => {
  const id = workspaceId.value;
  if (!id) return null;
  const ws = store.workspaces.find((w) => w.__dataId === id);
  return { id, name: ws?.name ?? '' };
});

const boardSegment = computed(() => {
  const id = boardId.value;
  if (!id) return null;
  return { id, name: store.boardContext?.name ?? '' };
});

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  workspace: workspaceSegment,
  board: boardSegment,
  tail: computed(() => ({
    text: t('operationCore.breadcrumbNewWorkItem'),
    disabled: true,
  })),
});

const backTo = computed(() => {
  const bd = boardId.value;
  const ws = workspaceId.value;
  if (bd) return `/apps/operation-core/boards/${encodeURIComponent(bd)}`;
  if (ws) return `/apps/operation-core/workspace?workspaceId=${encodeURIComponent(ws)}`;
  return '/apps/operation-core/workspace';
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

const pageTitle = computed(() => {
  const name = formContext.value?.formName;
  return name ? `${t('operationCore.create.title')} — ${name}` : t('operationCore.create.title');
});

const formContentMaxWidthPx = computed(() =>
  normalizeOcDialogMaxWidthPx(formContext.value?.layout?.dialogMaxWidth)
);

async function resolveFormId(): Promise<string | undefined> {
  if (formIdQuery.value) return formIdQuery.value;
  const ws = workspaceId.value;
  const bd = boardId.value;
  if (!ws || !bd) return undefined;
  const boards = await ocListBoardsForWorkspace(ws);
  const board = boards.find((b) => b.__dataId === bd);
  return board?.defaultFormId ?? undefined;
}

async function loadForm() {
  const ws = workspaceId.value;
  if (!ws) {
    formContext.value = null;
    errorLocal.value = t('operationCore.create.missingWorkspace');
    return;
  }

  loading.value = true;
  errorLocal.value = null;
  try {
    if (!store.workspaces.length) {
      await store.loadWorkspaces();
    }
    resolvedFormId.value = await resolveFormId();
    const [ctx, poolFields] = await Promise.all([
      ocGetFormCreateContext(ws, { formId: resolvedFormId.value }),
      ocListPoolFieldsForWorkspace(ws),
    ]);
    if (ctx.permissions?.canEdit === false) {
      errorLocal.value = t('operationCore.create.noPermission');
      formContext.value = null;
      return;
    }
    formContext.value = enrichFormRuntimeFields(ctx, { poolFields, translate: t });
    formModel.value = initialFormModelFromContext(ctx);
    validationAttempted.value = false;
  } catch (e: unknown) {
    formContext.value = null;
    errorLocal.value = ocExtractDgErrorMessage(e, t('operationCore.create.loadError'));
  } finally {
    loading.value = false;
  }
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
    const payload = buildCreateWorkItemRequest(
      formModel.value,
      workspaceId.value,
      boardId.value || undefined,
      formContext.value
    );
    const created = await ocCreateWorkItem(payload);
    const qs = new URLSearchParams();
    if (boardId.value) qs.set('from', 'board');
    if (boardId.value) qs.set('boardId', boardId.value);
    const suffix = qs.toString() ? `?${qs.toString()}` : '';
    await router.push(
      `/apps/operation-core/work-items/${encodeURIComponent(created.id)}/profile${suffix}`
    );
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(e, t('operationCore.create.submitError'));
  } finally {
    submitting.value = false;
  }
}

onMounted(() => {
  void loadForm();
});

watch([workspaceId, formIdQuery], () => {
  void loadForm();
});

watch(formModel, () => {
  if (validationAttempted.value && validationIssues.value.length === 0) {
    errorLocal.value = null;
  }
}, { deep: true });
</script>

<template>
  <div class="oc-flow oc-create-work-item-page">
    <BaseBreadcrumb :title="pageTitle" :breadcrumbs="breadcrumbs" />

    <v-alert v-if="!workspaceId" type="warning" variant="tonal" class="ma-4 rounded-lg">
      {{ t('operationCore.create.missingWorkspace') }}
      <div class="mt-3">
        <v-btn variant="tonal" color="primary" class="text-none" to="/apps/operation-core/workspace">
          {{ t('operationCore.board.backToWorkspace') }}
        </v-btn>
      </div>
    </v-alert>

    <template v-else>
      <v-card
        variant="outlined"
        class="rounded-lg ma-4 mx-auto"
        :style="{ maxWidth: `${formContentMaxWidthPx}px` }"
      >
        <v-card-text>
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ t('operationCore.create.subtitle') }}
          </p>

          <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

          <v-alert
            v-if="validationAttempted && validationIssues.length"
            type="warning"
            variant="tonal"
            class="mb-4 rounded-lg"
            :title="t('operationCore.create.validationSummaryTitle')"
          >
            <p class="text-body-2 mb-2">
              {{ t('operationCore.create.validationRequired') }}
            </p>
            <ul class="oc-create-validation-list mb-0 pl-4">
              <li v-for="issue in validationIssues" :key="issue.fieldKey">
                {{ issue.label }}
              </li>
            </ul>
          </v-alert>

          <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4 rounded-lg" closable @click:close="errorLocal = null">
            {{ errorLocal }}
          </v-alert>

          <OcDynamicForm
            v-if="formContext && !loading"
            v-model="formModel"
            :context="formContext"
            :field-errors="fieldErrors"
          />
        </v-card-text>

        <v-divider />

        <v-card-actions class="pa-4">
          <v-btn variant="text" class="text-none" :to="backTo" :disabled="submitting">
            {{ t('operationCore.create.cancel') }}
          </v-btn>
          <v-spacer />
          <v-btn
            color="primary"
            variant="flat"
            rounded="lg"
            class="text-none"
            :loading="submitting"
            :disabled="loading || submitting || formContext?.permissions?.canEdit === false"
            @click="submit"
          >
            {{ t('operationCore.create.submit') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </template>
  </div>
</template>

<style scoped>
.oc-create-validation-list {
  list-style: disc;
}
</style>
