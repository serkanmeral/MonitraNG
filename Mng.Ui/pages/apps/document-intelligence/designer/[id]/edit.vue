<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DiCollaboraEditor from '@/components/apps/document-intelligence/DiCollaboraEditor.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  diExtractMessage,
  diGetTemplate,
  diGetTemplateEditorSession,
  diPublishTemplate,
} from '@/services/documentIntelligenceService';
import type { DiTemplateDetail } from '@/types/apps/documentIntelligence';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();

const templateId = computed(() => String(route.params.id ?? ''));

const template = ref<DiTemplateDetail | null>(null);
const editorUrl = ref<string | null>(null);
const editorReadOnly = ref(false);
const loading = ref(true);
const editorLoading = ref(false);
const error = ref<string | null>(null);
const editorError = ref<string | null>(null);
const notify = ref<string | null>(null);

const publishDialog = ref(false);
const publishing = ref(false);

const isPublished = computed(
  () => (template.value?.status ?? '').toLowerCase() === 'published'
);
const isDraft = computed(() => !isPublished.value);

const breadcrumbs = computed(() => [
  { title: t('documentIntelligence.menuTitle'), to: '/apps/document-intelligence' },
  { title: t('documentIntelligence.designer.title'), to: listPath.value },
  { title: template.value?.name ?? t('documentIntelligence.designer.editTitle'), disabled: true },
]);

const listPath = computed(() => {
  const categoryId = typeof route.query.categoryId === 'string' ? route.query.categoryId : template.value?.categoryId;
  if (categoryId) {
    return `/apps/document-intelligence/designer?categoryId=${encodeURIComponent(categoryId)}`;
  }
  return '/apps/document-intelligence/designer';
});

async function loadEditor() {
  const id = templateId.value;
  if (!id) {
    error.value = t('documentIntelligence.designer.errors.load');
    loading.value = false;
    return;
  }

  loading.value = true;
  editorLoading.value = true;
  error.value = null;
  editorError.value = null;
  notify.value = null;
  editorUrl.value = null;
  editorReadOnly.value = false;

  try {
    template.value = await diGetTemplate(id);

    if (isPublished.value) {
      return;
    }

    const session = await diGetTemplateEditorSession(id);
    editorReadOnly.value = session.readOnly;
    editorUrl.value = session.editorUrl || null;
  } catch (e: unknown) {
    const message = diExtractMessage(e, t('documentIntelligence.designer.errors.load'));
    error.value = message;
    editorError.value = diExtractMessage(e, t('documentIntelligence.designer.errors.editorSession'));
    template.value = null;
    editorUrl.value = null;
  } finally {
    loading.value = false;
    editorLoading.value = false;
  }
}

function backToList() {
  router.push(listPath.value);
}

function openPublishDialog() {
  publishDialog.value = true;
}

async function submitPublish() {
  const id = templateId.value;
  if (!id) return;

  publishing.value = true;
  error.value = null;
  notify.value = null;
  try {
    template.value = await diPublishTemplate(id);
    publishDialog.value = false;
    editorUrl.value = null;
    notify.value = t('documentIntelligence.published');
  } catch (e: unknown) {
    error.value = diExtractMessage(e, t('documentIntelligence.designer.errors.publish'));
  } finally {
    publishing.value = false;
  }
}

onMounted(loadEditor);
</script>

<template>
  <div>
    <BaseBreadcrumb
      :title="template?.name ?? t('documentIntelligence.designer.editTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <div class="d-flex align-center justify-space-between mb-4 ga-2 flex-wrap">
      <div>
        <p v-if="template?.code" class="text-caption text-medium-emphasis mb-1">
          {{ t('documentIntelligence.designer.templateCode') }}: <code>{{ template.code }}</code>
        </p>
        <v-chip
          v-if="template?.status"
          size="small"
          variant="tonal"
          label
          :color="isDraft ? 'warning' : 'success'"
        >
          {{
            isDraft
              ? t('documentIntelligence.draft')
              : t('documentIntelligence.designer.statusPublished')
          }}
        </v-chip>
      </div>
      <div class="d-flex ga-2 flex-wrap">
        <v-btn variant="text" class="text-none" @click="backToList">
          {{ t('documentIntelligence.designer.backToList') }}
        </v-btn>
        <v-btn
          v-if="isDraft"
          color="success"
          variant="tonal"
          class="text-none"
          prepend-icon="mdi-publish"
          @click="openPublishDialog"
        >
          {{ t('documentIntelligence.publish') }}
        </v-btn>
        <v-btn
          v-if="isDraft"
          color="primary"
          variant="flat"
          class="text-none"
          @click="backToList"
        >
          {{ t('documentIntelligence.designer.saveAndBackToList') }}
        </v-btn>
      </div>
    </div>

    <v-alert v-if="error" type="error" variant="tonal" class="mb-3 rounded-lg">
      {{ error }}
    </v-alert>
    <v-alert
      v-if="notify"
      type="success"
      variant="tonal"
      closable
      class="mb-3 rounded-lg"
      @click:close="notify = null"
    >
      {{ notify }}
    </v-alert>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

    <v-alert
      v-else-if="isPublished"
      type="info"
      variant="tonal"
      class="mb-3 rounded-lg"
    >
      {{ t('documentIntelligence.designer.publishedReadOnlyHint') }}
    </v-alert>

    <v-alert
      v-else-if="editorReadOnly"
      type="warning"
      variant="tonal"
      class="mb-3 rounded-lg"
    >
      {{ t('documentIntelligence.designer.editorReadOnlyHint') }}
    </v-alert>

    <DiCollaboraEditor
      v-if="!loading && isDraft"
      :editor-url="editorUrl"
      :loading="editorLoading"
      :error="editorError"
      :title="template?.name"
    />

    <v-dialog v-model="publishDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.publish') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4 text-body-2">
          {{
            t('documentIntelligence.designer.publishConfirm', {
              name: template?.name ?? '',
            })
          }}
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="publishDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="success"
            variant="flat"
            class="text-none"
            :loading="publishing"
            @click="submitPublish"
          >
            {{ t('documentIntelligence.publish') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
