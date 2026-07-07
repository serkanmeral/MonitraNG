<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import { useDiEditorSessionCleanup } from '@/composables/useDiEditorSessionCleanup';
import DiCollaboraEditor from '@/components/apps/document-intelligence/DiCollaboraEditor.vue';
import {
  diCreateTemplateGenerationPreviewSession,
  diGenerateFromTemplate,
  diGetTemplate,
  diListTemplates,
  diFetchResourceExportPdf,
} from '@/services/documentIntelligenceService';
import type {
  DiGenerateDocumentResult,
  DiTemplateParameter,
  DiTemplateSummary,
} from '@/types/apps/documentIntelligence';

const SYSTEM_PARAM_KEYS = new Set(['documentname', 'docno', 'generatedat', 'createperson']);

const props = defineProps<{
  modelValue: boolean;
  parentFolderId: string | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  created: [result: DiGenerateDocumentResult];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const { push } = useAppToast();
const { trackEditorAccessToken, releaseEditorSession } = useDiEditorSessionCleanup();

const loadingTemplates = ref(false);
const loadingTemplate = ref(false);
const templates = ref<DiTemplateSummary[]>([]);
const templateId = ref<string | null>(null);
const documentName = ref('');
const parameters = ref<DiTemplateParameter[]>([]);
const overrides = ref<Record<string, string>>({});
const previewLoading = ref(false);
const previewMissing = ref<string[]>([]);
const previewWarnings = ref<string[]>([]);
const collaboraPreviewOpen = ref(false);
const collaboraEditorUrl = ref<string | null>(null);
const collaboraPreviewError = ref<string | null>(null);
const submitting = ref(false);
const exportingPdf = ref(false);
const errorMessage = ref('');
const lastResult = ref<DiGenerateDocumentResult | null>(null);

const publishedTemplates = computed(() =>
  templates.value.filter((tpl) => (tpl.status ?? '').toLowerCase() === 'published')
);

const templateItems = computed(() =>
  publishedTemplates.value.map((tpl) => ({
    title: tpl.name,
    value: tpl.id,
    subtitle: [tpl.code, tpl.description].filter(Boolean).join(' · '),
  }))
);

const selectedTemplate = computed(
  () => publishedTemplates.value.find((tpl) => tpl.id === templateId.value) ?? null
);

const collaboraPreviewTitle = computed(() => {
  const name = documentName.value.trim() || selectedTemplate.value?.name || '';
  return name
    ? `${name} · ${t('documentIntelligence.generateFromTemplate.previewTitle')}`
    : t('documentIntelligence.generateFromTemplate.previewTitle');
});

const editableParameters = computed(() =>
  parameters.value.filter((p) => isUserEditableParam(p.key, p.valueSourceMode))
);

const autoParameters = computed(() =>
  parameters.value.filter((p) => !isUserEditableParam(p.key, p.valueSourceMode))
);

const previewMissingTokens = computed(() =>
  previewMissing.value.map((k) => `{{${k}}}`).join(', ')
);

const canSubmit = computed(
  () =>
    Boolean(
      templateId.value?.trim() &&
        props.parentFolderId?.trim() &&
        documentName.value.trim() &&
        !submitting.value
    )
);

function isUserEditableParam(key: string, mode: string | undefined): boolean {
  if (SYSTEM_PARAM_KEYS.has(key.trim().toLowerCase())) return false;
  const m = (mode ?? 'manual').toLowerCase();
  return m !== 'incremental' && m !== 'generated';
}

function paramModeLabel(mode: string | undefined): string {
  const m = (mode ?? 'manual').toLowerCase();
  return t(`documentIntelligence.generateFromTemplate.mode.${m}`, m);
}

function resetState() {
  templateId.value = null;
  documentName.value = '';
  parameters.value = [];
  overrides.value = {};
  previewMissing.value = [];
  previewWarnings.value = [];
  resetCollaboraPreview();
  errorMessage.value = '';
  lastResult.value = null;
}

function resetCollaboraPreview() {
  collaboraEditorUrl.value = null;
  collaboraPreviewError.value = null;
  collaboraPreviewOpen.value = false;
}

async function loadTemplates() {
  loadingTemplates.value = true;
  errorMessage.value = '';
  try {
    const res = await diListTemplates();
    templates.value = res.items ?? [];
    if (!templateId.value && publishedTemplates.value.length === 1) {
      templateId.value = publishedTemplates.value[0]!.id;
    }
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'documentIntelligence.generateFromTemplate.errors.loadTemplates');
    templates.value = [];
  } finally {
    loadingTemplates.value = false;
  }
}

async function loadTemplateDetail(id: string) {
  loadingTemplate.value = true;
  errorMessage.value = '';
  previewMissing.value = [];
  previewWarnings.value = [];
  try {
    const detail = await diGetTemplate(id);
    parameters.value = detail.parameters ?? [];
    overrides.value = {};
    for (const p of editableParameters.value) {
      if (p.defaultValue?.trim()) {
        overrides.value[p.key] = p.defaultValue.trim();
      }
    }
    if (!documentName.value.trim() && detail.name) {
      documentName.value = detail.name;
    }
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'documentIntelligence.generateFromTemplate.errors.loadTemplate');
    parameters.value = [];
  } finally {
    loadingTemplate.value = false;
  }
}

function buildOverridesPayload(): Record<string, string> {
  const payload: Record<string, string> = {};
  for (const [key, value] of Object.entries(overrides.value)) {
    if (value?.trim()) payload[key] = value.trim();
  }
  return payload;
}

async function runPreview() {
  const id = templateId.value?.trim();
  if (!id) return;

  previewLoading.value = true;
  errorMessage.value = '';
  await releaseEditorSession();
  resetCollaboraPreview();
  try {
    const session = await diCreateTemplateGenerationPreviewSession(id, {
      overrides: buildOverridesPayload(),
      documentName: documentName.value.trim() || undefined,
    });

    previewMissing.value = session.missingKeys ?? [];
    previewWarnings.value = [
      ...(session.undefinedParameterKeys ?? []),
      ...(session.unresolvedParameterKeys ?? []),
    ];

    for (const [key, rawValue] of Object.entries(session.values ?? {})) {
      const value = String(rawValue ?? '');
      if (!isUserEditableParam(key, parameters.value.find((p) => p.key === key)?.valueSourceMode)) {
        continue;
      }
      if (!overrides.value[key]?.trim() && value.trim()) {
        overrides.value[key] = value;
      }
    }

    collaboraEditorUrl.value = session.editorUrl || null;
    trackEditorAccessToken(session.accessToken);
    collaboraPreviewOpen.value = true;
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'documentIntelligence.generateFromTemplate.errors.preview');
  } finally {
    previewLoading.value = false;
  }
}

async function closeCollaboraPreview() {
  collaboraPreviewOpen.value = false;
  await releaseEditorSession();
  resetCollaboraPreview();
}

function onCollaboraPreviewOpenChange(open: boolean) {
  if (!open) void closeCollaboraPreview();
}

async function submit() {
  const id = templateId.value?.trim();
  const folderId = props.parentFolderId?.trim();
  const name = documentName.value.trim();
  if (!id || !folderId || !name) return;

  submitting.value = true;
  errorMessage.value = '';
  try {
    const result = await diGenerateFromTemplate(id, {
      parentFolderId: folderId,
      documentName: name,
      overrides: buildOverridesPayload(),
    });
    lastResult.value = result;
    emit('created', result);
    if (result.hasParameterWarnings) {
      push({
        title: t('documentIntelligence.generateFromTemplate.warningsTitle'),
        message: t('documentIntelligence.generateFromTemplate.warningsBody'),
        severity: 'warning',
      });
    } else {
      push({
        title: t('documentIntelligence.notify.successTitle'),
        message: t('documentIntelligence.generateFromTemplate.created'),
        severity: 'success',
      });
    }
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'documentIntelligence.generateFromTemplate.errors.generate');
  } finally {
    submitting.value = false;
  }
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

async function downloadPdf() {
  const result = lastResult.value;
  if (!result?.resourceId) return;
  exportingPdf.value = true;
  try {
    const blob = await diFetchResourceExportPdf(result.resourceId);
    const pdfName = result.fileName?.replace(/\.docx$/i, '.pdf') || 'document.pdf';
    downloadBlob(blob, pdfName);
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'documentIntelligence.generateFromTemplate.errors.exportPdf');
  } finally {
    exportingPdf.value = false;
  }
}

function closeDialog() {
  emit('update:modelValue', false);
}

watch(
  () => props.modelValue,
  (open) => {
    if (!open) {
      void releaseEditorSession();
      resetState();
      return;
    }
    void loadTemplates();
  }
);

watch(templateId, (id) => {
  if (!props.modelValue || !id) {
    parameters.value = [];
    overrides.value = {};
    return;
  }
  void loadTemplateDetail(id);
});
</script>

<template>
  <div>
  <v-dialog
    :model-value="modelValue"
    max-width="640"
    scrollable
    @update:model-value="emit('update:modelValue', $event)"
  >
    <v-card rounded="lg">
      <v-card-title class="text-subtitle-1 font-weight-bold">
        {{ t('documentIntelligence.generateFromTemplate.title') }}
      </v-card-title>

      <v-card-text>
        <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
          {{ errorMessage }}
        </v-alert>

        <template v-if="lastResult">
          <v-alert type="success" variant="tonal" density="compact" class="mb-3">
            {{ t('documentIntelligence.generateFromTemplate.successHint') }}
          </v-alert>
          <v-list density="compact" class="bg-transparent rounded-lg border mb-3">
            <v-list-item v-if="lastResult.docNo">
              <v-list-item-title class="text-caption text-medium-emphasis">
                {{ t('documentIntelligence.documentNoLabel') }}
              </v-list-item-title>
              <v-list-item-subtitle>{{ lastResult.docNo }}</v-list-item-subtitle>
            </v-list-item>
            <v-list-item>
              <v-list-item-title class="text-caption text-medium-emphasis">
                {{ t('documentIntelligence.generateFromTemplate.fileName') }}
              </v-list-item-title>
              <v-list-item-subtitle>{{ lastResult.fileName }}</v-list-item-subtitle>
            </v-list-item>
          </v-list>
          <div class="d-flex flex-wrap ga-2">
            <v-btn
              color="primary"
              variant="flat"
              class="text-none"
              prepend-icon="mdi-file-pdf-box"
              :loading="exportingPdf"
              @click="downloadPdf"
            >
              {{ t('documentIntelligence.generateFromTemplate.downloadPdf') }}
            </v-btn>
          </div>
        </template>

        <template v-else>
          <v-alert
            v-if="!loadingTemplates && !publishedTemplates.length"
            type="warning"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{ t('documentIntelligence.generateFromTemplate.noTemplates') }}
          </v-alert>

          <v-alert
            v-if="!parentFolderId"
            type="info"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{ t('documentIntelligence.generateFromTemplate.selectFolderHint') }}
          </v-alert>

          <v-select
            v-model="templateId"
            :items="templateItems"
            :label="t('documentIntelligence.generateFromTemplate.templateLabel')"
            :loading="loadingTemplates"
            :disabled="!templateItems.length"
            item-title="title"
            item-value="value"
            density="comfortable"
            variant="outlined"
            class="mb-3"
          >
            <template #item="{ item, props: itemProps }">
              <v-list-item v-bind="itemProps" :subtitle="item.raw.subtitle" />
            </template>
          </v-select>

          <v-text-field
            v-model="documentName"
            :label="t('documentIntelligence.generateFromTemplate.documentNameLabel')"
            :hint="t('documentIntelligence.generateFromTemplate.documentNameHint')"
            variant="outlined"
            density="comfortable"
            persistent-hint
            class="mb-3"
          />

          <v-progress-linear v-if="loadingTemplate" indeterminate class="mb-3" />

          <template v-if="editableParameters.length">
            <div class="text-subtitle-2 font-weight-bold mb-2">
              {{ t('documentIntelligence.generateFromTemplate.parametersTitle') }}
            </div>
            <v-text-field
              v-for="param in editableParameters"
              :key="param.key"
              :model-value="overrides[param.key] ?? ''"
              :label="param.label || param.key"
              :hint="paramModeLabel(param.valueSourceMode)"
              persistent-hint
              variant="outlined"
              density="comfortable"
              class="mb-2"
              @update:model-value="overrides[param.key] = String($event ?? '')"
            />
          </template>

          <template v-if="autoParameters.length">
            <div class="text-caption text-medium-emphasis mb-1 mt-2">
              {{ t('documentIntelligence.generateFromTemplate.autoParametersTitle') }}
            </div>
            <v-chip
              v-for="param in autoParameters"
              :key="param.key"
              size="small"
              variant="tonal"
              class="mr-1 mb-1"
            >
              {{ param.label || param.key }} · {{ paramModeLabel(param.valueSourceMode) }}
            </v-chip>
          </template>

          <v-alert
            v-if="previewMissing.length || previewWarnings.length"
            type="info"
            variant="tonal"
            density="compact"
            class="mt-3"
          >
            <div>{{ t('documentIntelligence.generateFromTemplate.previewPlaceholderHint') }}</div>
            <div v-if="previewMissing.length" class="mt-1">
              {{ t('documentIntelligence.generateFromTemplate.missingKeys') }}:
              {{ previewMissingTokens }}
            </div>
            <div v-if="previewWarnings.length" class="mt-1">
              {{ t('documentIntelligence.generateFromTemplate.placeholderWarnings') }}:
              {{ previewWarnings.join(', ') }}
            </div>
          </v-alert>
        </template>
      </v-card-text>

      <v-card-actions>
        <v-btn
          v-if="!lastResult && templateId"
          variant="text"
          class="text-none"
          :loading="previewLoading"
          prepend-icon="mdi-eye-outline"
          @click="runPreview"
        >
          {{ t('documentIntelligence.generateFromTemplate.preview') }}
        </v-btn>
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="closeDialog">
          {{ lastResult ? t('documentIntelligence.close') : t('documentIntelligence.cancel') }}
        </v-btn>
        <v-btn
          v-if="!lastResult"
          color="primary"
          variant="flat"
          class="text-none"
          :loading="submitting"
          :disabled="!canSubmit"
          prepend-icon="mdi-file-document-plus-outline"
          @click="submit"
        >
          {{ t('documentIntelligence.generateFromTemplate.generate') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-dialog
    v-model="collaboraPreviewOpen"
    fullscreen
    transition="dialog-bottom-transition"
    @update:model-value="onCollaboraPreviewOpenChange"
  >
    <v-card rounded="0" class="d-flex flex-column h-100">
      <v-toolbar density="comfortable" color="surface" class="border-b">
        <v-icon icon="mdi-file-eye-outline" class="ml-3 mr-2" color="primary" />
        <div class="px-1 py-1 flex-grow-1 min-width-0">
          <div class="text-subtitle-1 font-weight-bold text-truncate">
            {{ collaboraPreviewTitle }}
          </div>
          <div class="text-caption text-medium-emphasis">
            {{ t('documentIntelligence.generateFromTemplate.previewReadOnlyHint') }}
          </div>
        </div>
        <v-chip size="small" variant="tonal" color="warning" label class="ml-2">
          {{ t('documentIntelligence.designer.editorReadOnlyHint') }}
        </v-chip>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" @click="closeCollaboraPreview" />
      </v-toolbar>

      <v-card-text class="flex-grow-1 pa-0 di-template-preview-body">
        <DiCollaboraEditor
          :editor-url="collaboraEditorUrl"
          :loading="previewLoading"
          :error="collaboraPreviewError"
          :title="null"
        />
      </v-card-text>
    </v-card>
  </v-dialog>
  </div>
</template>

<style scoped>
.border-b {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}

.di-template-preview-body {
  min-height: 0;
}

.di-template-preview-body :deep(.di-collabora-editor) {
  border: 0;
  border-radius: 0;
  height: 100%;
}

.di-template-preview-body :deep(.di-collabora-editor__frame-wrap) {
  height: calc(100vh - 72px);
  max-height: none;
  min-height: 0;
}
</style>
