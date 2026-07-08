<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DiCollaboraEditor from '@/components/apps/document-intelligence/DiCollaboraEditor.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useDiEditorSessionCleanup } from '@/composables/useDiEditorSessionCleanup';
import { diGetCoverPage, diGetCoverPageDesignSession } from '@/services/documentIntelligenceService';
import type { DiCoverPage, DiCoverPageDesignSession } from '@/types/apps/documentIntelligence';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const editorRef = ref<InstanceType<typeof DiCollaboraEditor> | null>(null);
const { trackEditorAccessToken, releaseEditorSession } = useDiEditorSessionCleanup({
  requestSave: () => editorRef.value?.requestSave() ?? Promise.resolve(),
});
const route = useRoute();
const router = useRouter();

const coverPageId = computed(() => String(route.params.id ?? ''));

const coverPage = ref<DiCoverPage | null>(null);
const designSession = ref<DiCoverPageDesignSession | null>(null);
const editorUrl = ref<string | null>(null);
const loading = ref(true);
const editorLoading = ref(false);
const error = ref<string | null>(null);
const editorError = ref<string | null>(null);

const breadcrumbs = computed(() => [
  { title: t('documentIntelligence.designer.title'), to: '/apps/document-intelligence/designer' },
  { title: t('documentIntelligence.coverPages.title'), to: '/apps/document-intelligence/designer/cover-pages' },
  { title: coverPage.value?.name ?? t('documentIntelligence.coverPages.designTitle'), disabled: true },
]);

async function loadEditor() {
  const id = coverPageId.value;
  if (!id) {
    error.value = t('documentIntelligence.coverPages.errors.load');
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
    coverPage.value = await diGetCoverPage(id);
    const session = await diGetCoverPageDesignSession(id);
    designSession.value = session;
    editorUrl.value = session.editorUrl || null;
    trackEditorAccessToken(session.accessToken);
  } catch (e: unknown) {
    const message = panelError(e, 'documentIntelligence.coverPages.errors.designSession');
    error.value = message;
    editorError.value = message;
    coverPage.value = null;
    editorUrl.value = null;
  } finally {
    loading.value = false;
    editorLoading.value = false;
  }
}

async function backToList() {
  await releaseEditorSession();
  router.push('/apps/document-intelligence/designer/cover-pages');
}

onMounted(() => {
  void loadEditor();
});
</script>

<template>
  <div>
    <BaseBreadcrumb
      :title="coverPage?.name ?? t('documentIntelligence.coverPages.designTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <div class="d-flex align-center justify-space-between mb-4 ga-2 flex-wrap">
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('documentIntelligence.coverPages.designSubtitle') }}
      </p>
      <v-btn variant="text" class="text-none" @click="backToList">
        {{ t('documentIntelligence.coverPages.backToCoverPages') }}
      </v-btn>
    </div>

    <v-alert v-if="error" type="error" variant="tonal" class="mb-3 rounded-lg">
      {{ error }}
    </v-alert>

    <v-card elevation="10" rounded="lg" class="pa-0 overflow-hidden">
      <DiCollaboraEditor
        ref="editorRef"
        :editor-url="editorUrl"
        :loading="loading || editorLoading"
        :error="editorError"
        :title="coverPage?.name"
      />
    </v-card>
  </div>
</template>

<style scoped>
:deep(.di-collabora-frame) {
  min-height: 72vh;
}
</style>
