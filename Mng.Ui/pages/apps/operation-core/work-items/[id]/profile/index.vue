<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcDynamicForm from '@/components/apps/operation-core/OcDynamicForm.vue';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useOperationCoreStore } from '@/stores/apps/operationCore';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  initialFormModelFromContext,
  ocExtractDgErrorMessage,
  ocGetFormEditContext,
  ocListPoolFieldsForWorkspace,
} from '@/services/operationCoreService';
import type { OcFormRuntimeContext } from '@/types/apps/operationCore';
import { enrichFormRuntimeFields } from '@/utils/ocFormFieldLabels';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const store = useOperationCoreStore();

const workItemId = computed(() => String(route.params.id ?? ''));
const boardIdQuery = computed(() =>
  typeof route.query.boardId === 'string' ? route.query.boardId.trim() : ''
);

const loading = ref(false);
const errorLocal = ref<string | null>(null);
const formContext = ref<OcFormRuntimeContext | null>(null);
const formModel = ref<Record<string, unknown>>({});

const workItemTitle = computed(() => {
  const fromModel = formModel.value.title;
  return (typeof fromModel === 'string' && fromModel.trim()) || t('operationCore.profile.placeholderTitle');
});

const pageTitle = computed(() => {
  const name = formContext.value?.formName?.trim();
  const base = t('operationCore.profile.title');
  return name ? `${base} — ${name}` : base;
});

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  tail: computed(() => ({
    text: workItemTitle.value,
    disabled: true,
  })),
});

const backToBoardTo = computed(() =>
  boardIdQuery.value ? `/apps/operation-core/boards/${encodeURIComponent(boardIdQuery.value)}` : null
);

async function loadProfile() {
  const id = workItemId.value;
  if (!id) return;

  loading.value = true;
  errorLocal.value = null;
  try {
    if (!store.workspaces.length) {
      await store.loadWorkspaces();
    }
    const ctx = await ocGetFormEditContext(id);
    const poolFields = await ocListPoolFieldsForWorkspace(ctx.workspaceId);
    formContext.value = enrichFormRuntimeFields(ctx, { poolFields, translate: t });
    formModel.value = initialFormModelFromContext(ctx);
  } catch (e: unknown) {
    formContext.value = null;
    errorLocal.value = ocExtractDgErrorMessage(e, t('operationCore.profile.loadError'));
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadProfile();
});

watch(workItemId, () => {
  void loadProfile();
});
</script>

<template>
  <div class="oc-flow oc-profile-page">
    <BaseBreadcrumb :title="pageTitle" :breadcrumbs="breadcrumbs" />

    <v-card variant="outlined" class="rounded-lg mb-4">
      <v-card-title class="d-flex align-center flex-wrap gap-2 py-3">
        <v-btn
          v-if="backToBoardTo"
          icon="mdi-arrow-left"
          variant="text"
          size="small"
          :to="backToBoardTo"
          :title="t('operationCore.board.backToBoard')"
        />
        <div class="min-width-0">
          <div class="text-subtitle-1 font-weight-bold text-truncate">{{ workItemTitle }}</div>
          <div class="text-caption text-medium-emphasis">
            {{ t('operationCore.profile.readonlyHint') }}
          </div>
        </div>
        <v-spacer />
        <v-chip size="small" variant="tonal" color="primary" prepend-icon="mdi-lock-outline">
          {{ t('operationCore.profile.readonlyChip') }}
        </v-chip>
      </v-card-title>
    </v-card>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4 rounded-lg" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>

    <v-card v-if="formContext && !loading" variant="outlined" class="rounded-lg">
      <v-card-text class="pa-4 pa-md-5">
        <OcDynamicForm v-model="formModel" :context="formContext" readonly />
      </v-card-text>
    </v-card>

    <v-card v-else-if="!loading && !errorLocal" variant="outlined" class="rounded-lg">
      <v-card-text class="pa-8 text-center text-medium-emphasis">
        {{ t('operationCore.profile.workItemId') }}: {{ workItemId }}
      </v-card-text>
    </v-card>
  </div>
</template>

<style scoped>
.min-width-0 {
  min-width: 0;
}
</style>
