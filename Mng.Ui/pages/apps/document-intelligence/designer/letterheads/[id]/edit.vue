<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DiCollaboraEditor from '@/components/apps/document-intelligence/DiCollaboraEditor.vue';
import DiLetterheadDesignFooterSummary from '@/components/apps/document-intelligence/DiLetterheadDesignFooterSummary.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useDiEditorSessionCleanup } from '@/composables/useDiEditorSessionCleanup';
import { diGetLetterhead, diGetLetterheadDesignSession } from '@/services/documentIntelligenceService';
import type { DiLetterhead, DiLetterheadDesignSession } from '@/types/apps/documentIntelligence';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const { trackEditorAccessToken, releaseEditorSession } = useDiEditorSessionCleanup();
const route = useRoute();
const router = useRouter();

const letterheadId = computed(() => String(route.params.id ?? ''));

const letterhead = ref<DiLetterhead | null>(null);
const designSession = ref<DiLetterheadDesignSession | null>(null);
const editorUrl = ref<string | null>(null);
const loading = ref(true);
const editorLoading = ref(false);
const error = ref<string | null>(null);
const editorError = ref<string | null>(null);

const breadcrumbs = computed(() => [
  { title: t('documentIntelligence.designer.title'), to: '/apps/document-intelligence/designer' },
  { title: t('documentIntelligence.letterheads.title'), to: '/apps/document-intelligence/designer/letterheads' },
  { title: letterhead.value?.name ?? t('documentIntelligence.letterheads.designTitle'), disabled: true },
]);

async function loadEditor() {
  const id = letterheadId.value;
  if (!id) {
    error.value = t('documentIntelligence.letterheads.errors.load');
    loading.value = false;
    return;
  }

  loading.value = true;
  editorLoading.value = true;
  error.value = null;
  editorError.value = null;
  editorUrl.value = null;
  designSession.value = null;

  await releaseEditorSession();

  try {
    letterhead.value = await diGetLetterhead(id);
    const session = await diGetLetterheadDesignSession(id);
    designSession.value = session;
    editorUrl.value = session.editorUrl || null;
    trackEditorAccessToken(session.accessToken);
  } catch (e: unknown) {
    const message = panelError(e, 'documentIntelligence.letterheads.errors.designSession');
    error.value = message;
    editorError.value = message;
    letterhead.value = null;
    editorUrl.value = null;
  } finally {
    loading.value = false;
    editorLoading.value = false;
  }
}

function backToList() {
  void releaseEditorSession();
  router.push('/apps/document-intelligence/designer/letterheads');
}

onMounted(() => {
  void loadEditor();
});
</script>

<template>
  <div>
    <BaseBreadcrumb
      :title="letterhead?.name ?? t('documentIntelligence.letterheads.designTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <div class="d-flex align-center justify-space-between mb-4 ga-2 flex-wrap">
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('documentIntelligence.letterheads.designSubtitle') }}
      </p>
      <v-btn variant="text" class="text-none" @click="backToList">
        {{ t('documentIntelligence.letterheads.backToLetterheads') }}
      </v-btn>
    </div>

    <v-alert v-if="error" type="error" variant="tonal" class="mb-3 rounded-lg">
      {{ error }}
    </v-alert>

    <DiLetterheadDesignFooterSummary
      v-if="!loading && letterhead"
      :letterhead="letterhead"
      :session="designSession"
    />

    <v-card elevation="10" rounded="lg" class="pa-0 overflow-hidden">
      <DiCollaboraEditor
        :editor-url="editorUrl"
        :loading="loading || editorLoading"
        :error="editorError"
        :title="letterhead?.name"
      />
    </v-card>
  </div>
</template>

<style scoped>
:deep(.di-collabora-frame) {
  min-height: 72vh;
}
</style>
