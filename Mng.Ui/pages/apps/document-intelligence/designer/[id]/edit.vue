<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DiCollaboraEditor from '@/components/apps/document-intelligence/DiCollaboraEditor.vue';
import DiTemplatePageStructureForm from '@/components/apps/document-intelligence/DiTemplatePageStructureForm.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  diGetTemplate,
  diGetTemplateEditorSession,
  diListLetterheads,
  diPublishTemplate,
  diUnpublishTemplate,
  diUpdateTemplatePageStructure,
} from '@/services/documentIntelligenceService';
import type {
  DiLetterhead,
  DiTemplateDetail,
} from '@/types/apps/documentIntelligence';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
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
const unpublishDialog = ref(false);
const unpublishing = ref(false);

const letterheads = ref<DiLetterhead[]>([]);
const letterheadsLoading = ref(false);
const defaultLetterheadId = ref<string | null>(null);
const applyingPageStructure = ref(false);
const pageStructureExpanded = ref<number | undefined>(undefined);

const isPublished = computed(
  () => (template.value?.status ?? '').toLowerCase() === 'published'
);
const isDraft = computed(() => !isPublished.value);
const viewOnly = computed(() => isPublished.value);

const letterheadSelectOptions = computed(() =>
  letterheads.value
    .filter((item) => item.isActive)
    .map((item) => ({
      value: item.id,
      title: item.name,
      subtitle: item.code,
    }))
);

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

function resolveCatalogDefaultLetterheadId(items: DiLetterhead[]): string | null {
  const catalogDefault = items.find((item) => item.isDefault && item.isActive);
  if (catalogDefault) return catalogDefault.id;
  const firstActive = items.find((item) => item.isActive);
  return firstActive?.id ?? null;
}

function syncPageStructureFromTemplate(detail: DiTemplateDetail) {
  defaultLetterheadId.value =
    detail.defaultLetterheadId ?? resolveCatalogDefaultLetterheadId(letterheads.value);
}

async function loadLetterheads() {
  letterheadsLoading.value = true;
  try {
    const res = await diListLetterheads(true);
    letterheads.value = res.items;
  } catch {
    letterheads.value = [];
  } finally {
    letterheadsLoading.value = false;
  }
}

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
  editorUrl.value = null;
  editorReadOnly.value = false;

  try {
    template.value = await diGetTemplate(id);
    syncPageStructureFromTemplate(template.value);

    const session = await diGetTemplateEditorSession(id);
    editorReadOnly.value = session.readOnly || isPublished.value;
    editorUrl.value = session.editorUrl || null;
  } catch (e: unknown) {
    const message = panelError(e, 'documentIntelligence.designer.errors.load');
    error.value = message;
    editorError.value = message;
    template.value = null;
    editorUrl.value = null;
  } finally {
    loading.value = false;
    editorLoading.value = false;
  }
}

async function applyPageStructure() {
  const id = templateId.value;
  if (!id || !isDraft.value) return;

  applyingPageStructure.value = true;
  error.value = null;
  notify.value = null;

  try {
    template.value = await diUpdateTemplatePageStructure(id, {
      defaultLetterheadId: defaultLetterheadId.value,
    });
    syncPageStructureFromTemplate(template.value);
    notify.value = t('documentIntelligence.designer.pageStructureApplied');
    await loadEditor();
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.pageStructure');
  } finally {
    applyingPageStructure.value = false;
  }
}

function backToList() {
  router.push(listPath.value);
}

function openPublishDialog() {
  publishDialog.value = true;
}

function openUnpublishDialog() {
  unpublishDialog.value = true;
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
    editorReadOnly.value = true;
    notify.value = t('documentIntelligence.published');
    await loadEditor();
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.publish');
  } finally {
    publishing.value = false;
  }
}

async function submitUnpublish() {
  const id = templateId.value;
  if (!id) return;

  unpublishing.value = true;
  error.value = null;
  notify.value = null;
  try {
    template.value = await diUnpublishTemplate(id);
    unpublishDialog.value = false;
    notify.value = t('documentIntelligence.designer.unpublished');
    await loadEditor();
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.unpublish');
  } finally {
    unpublishing.value = false;
  }
}

onMounted(async () => {
  await loadLetterheads();
  await loadEditor();
});
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
              ? t('documentIntelligence.designer.statusDraft')
              : t('documentIntelligence.designer.statusActive')
          }}
        </v-chip>
      </div>
      <div class="d-flex ga-2 flex-wrap">
        <v-btn variant="text" class="text-none" @click="backToList">
          {{ t('documentIntelligence.designer.backToList') }}
        </v-btn>
        <v-btn
          v-if="isPublished"
          color="warning"
          variant="tonal"
          class="text-none"
          prepend-icon="mdi-lock-open-variant-outline"
          @click="openUnpublishDialog"
        >
          {{ t('documentIntelligence.designer.unpublishToEdit') }}
        </v-btn>
        <v-btn
          v-if="isDraft"
          color="success"
          variant="tonal"
          class="text-none"
          prepend-icon="mdi-publish"
          @click="openPublishDialog"
        >
          {{ t('documentIntelligence.designer.activateForGeneration') }}
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
      v-else-if="viewOnly"
      type="info"
      variant="tonal"
      class="mb-3 rounded-lg"
    >
      {{ t('documentIntelligence.designer.publishedReadOnlyHint') }}
    </v-alert>

    <v-expansion-panels
      v-if="isDraft && template && !loading"
      v-model="pageStructureExpanded"
      class="mb-3"
      rounded="lg"
    >
      <v-expansion-panel elevation="1">
        <v-expansion-panel-title class="text-subtitle-2 font-weight-bold">
          {{ t('documentIntelligence.designer.pageStructure.title') }}
        </v-expansion-panel-title>
        <v-expansion-panel-text>
          <DiTemplatePageStructureForm
            v-model:default-letterhead-id="defaultLetterheadId"
            :letterhead-options="letterheadSelectOptions"
            :loading="letterheadsLoading"
            draft-hint
          />
          <div class="d-flex justify-end mt-4">
            <v-btn
              color="primary"
              variant="flat"
              class="text-none"
              prepend-icon="mdi-file-document-refresh-outline"
              :loading="applyingPageStructure"
              @click="applyPageStructure"
            >
              {{ t('documentIntelligence.designer.applyPageStructure') }}
            </v-btn>
          </div>
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>

    <DiCollaboraEditor
      v-if="!loading && editorUrl"
      :editor-url="editorUrl"
      :loading="editorLoading"
      :error="editorError"
      :title="template?.name"
    />

    <v-dialog v-model="publishDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.designer.activateForGeneration') }}
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
            {{ t('documentIntelligence.designer.activateForGeneration') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="unpublishDialog" max-width="520">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.designer.unpublishToEdit') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4 text-body-2">
          {{
            t('documentIntelligence.designer.unpublishConfirm', {
              name: template?.name ?? '',
            })
          }}
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="unpublishDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="warning"
            variant="flat"
            class="text-none"
            :loading="unpublishing"
            @click="submitUnpublish"
          >
            {{ t('documentIntelligence.designer.unpublishToEdit') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
