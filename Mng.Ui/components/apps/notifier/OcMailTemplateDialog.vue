<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import DOMPurify from 'dompurify';
import { useAppI18n } from '@/composables/useAppI18n';
import OcMailHtmlEditor from '@/components/apps/notifier/OcMailHtmlEditor.client.vue';
import { previewMailTemplate } from '@/services/notifier/mailTemplates';
import type { MailTemplate, MailTemplatePreviewResult } from '@/types/apps/mailTemplates';
import {
  buildMailTemplatePayload,
  extractPlaceholderPaths,
  isSystemMailTemplate,
  newMailTemplateDraft,
  parseMailTemplateToDraft,
  parseSampleContextJson,
  validateMailTemplateDraft,
  type OcMailTemplateDraft,
} from '@/utils/ocMailTemplates';

const props = defineProps<{
  modelValue: boolean;
  template: MailTemplate | null;
  saving?: boolean;
  categoryOptions?: string[];
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [Record<string, unknown>];
}>();

const { t } = useAppI18n();
const draft = ref<OcMailTemplateDraft>(newMailTemplateDraft());
const previewLoading = ref(false);
const previewError = ref<string | null>(null);
const previewResult = ref<MailTemplatePreviewResult | null>(null);
const previewOpen = ref(false);

const isEdit = computed(() => !!props.template?.__dataId);
const isSystem = computed(() => (props.template ? isSystemMailTemplate(props.template) : false));
const canSave = computed(() => validateMailTemplateDraft(draft.value, isEdit.value) === null);

const canPreview = computed(() => {
  if (!draft.value.templateKey.trim()) return false;
  if (!draft.value.subject.trim() || !draft.value.bodyHtml.trim()) return false;
  return parseSampleContextJson(draft.value.sampleContextJson) !== null;
});

const previewHint = computed(() =>
  isEdit.value
    ? t('notifier.mailTemplates.previewHintSaved')
    : t('notifier.mailTemplates.previewHintDraft')
);

const sanitizedPreviewHtml = computed(() => {
  if (!previewResult.value?.htmlBody) return '';
  return DOMPurify.sanitize(previewResult.value.htmlBody, { WHOLE_DOCUMENT: true });
});

const inferredVariables = computed(() =>
  extractPlaceholderPaths(draft.value.subject, draft.value.bodyHtml).join(', ')
);

const categoryItems = computed(() => {
  const values = new Set<string>(['custom', 'system']);
  for (const option of props.categoryOptions ?? []) {
    const trimmed = option.trim();
    if (trimmed) values.add(trimmed);
  }
  const current = draft.value.category.trim();
  if (current) values.add(current);
  return [...values].sort((a, b) => a.localeCompare(b, 'tr'));
});

watch(
  () => [props.modelValue, props.template?.__dataId] as const,
  ([open]) => {
    if (open) {
      draft.value = props.template
        ? parseMailTemplateToDraft(props.template)
        : newMailTemplateDraft();
      previewResult.value = null;
      previewError.value = null;
    }
  }
);

function close() {
  emit('update:modelValue', false);
}

function submit() {
  if (!canSave.value) return;
  emit('save', buildMailTemplatePayload(draft.value, isEdit.value));
}

async function runPreview() {
  if (!canPreview.value) return;
  const context = parseSampleContextJson(draft.value.sampleContextJson);
  if (!context) {
    previewError.value = t('notifier.mailTemplates.invalidSampleContext');
    return;
  }
  previewLoading.value = true;
  previewError.value = null;
  try {
    previewResult.value = await previewMailTemplate(draft.value.templateKey.trim(), context, {
      subject: draft.value.subject,
      bodyHtmlOverride: draft.value.bodyHtml,
      layoutKeyOverride: draft.value.layoutKey,
      localeOverride: draft.value.locale,
    });
    previewOpen.value = true;
  } catch (e: unknown) {
    previewError.value = e instanceof Error ? e.message : t('notifier.mailTemplates.previewError');
  } finally {
    previewLoading.value = false;
  }
}
</script>

<template>
  <div>
  <v-dialog :model-value="modelValue" max-width="960" persistent scrollable @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center py-4">
        <v-icon icon="mdi-email-edit-outline" color="primary" class="me-2" />
        {{
          isEdit
            ? t('notifier.mailTemplates.editTemplate')
            : t('notifier.mailTemplates.addTemplate')
        }}
        <v-chip v-if="isSystem" size="small" variant="tonal" class="ms-2">system</v-chip>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" @click="close" />
      </v-card-title>
      <v-divider />
      <v-card-text class="pt-4" style="max-height: 72vh">
        <v-alert type="info" variant="tonal" density="compact" class="mb-4 rounded-lg">
          {{ t('notifier.mailTemplates.dialogHint') }}
        </v-alert>

        <v-row dense>
          <v-col cols="12" md="6">
            <v-text-field
              v-model="draft.templateKey"
              :label="t('notifier.mailTemplates.fieldTemplateKey')"
              :hint="t('notifier.mailTemplates.fieldTemplateKeyHint')"
              persistent-hint
              :disabled="isEdit"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-text-field
              v-model="draft.name"
              :label="t('notifier.mailTemplates.fieldName')"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="6">
            <v-combobox
              v-model="draft.category"
              :items="categoryItems"
              :label="t('notifier.mailTemplates.fieldCategory')"
              :hint="t('notifier.mailTemplates.fieldCategoryHint')"
              persistent-hint
              density="comfortable"
              :disabled="isSystem"
              clearable
            />
          </v-col>
        </v-row>

        <v-textarea
          v-model="draft.description"
          :label="t('notifier.mailTemplates.fieldDescription')"
          rows="2"
          auto-grow
          density="comfortable"
          class="mb-3"
        />

        <v-text-field
          v-model="draft.subject"
          :label="t('notifier.mailTemplates.fieldSubject')"
          :hint="t('notifier.mailTemplates.placeholderHint')"
          persistent-hint
          density="comfortable"
          class="mb-3"
        />

        <OcMailHtmlEditor
          v-model="draft.bodyHtml"
          :label="t('notifier.mailTemplates.fieldBodyHtml')"
          class="mb-3"
        />

        <v-row dense>
          <v-col cols="12" md="4">
            <v-text-field
              v-model="draft.layoutKey"
              :label="t('notifier.mailTemplates.fieldLayoutKey')"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field
              v-model="draft.locale"
              :label="t('notifier.mailTemplates.fieldLocale')"
              density="comfortable"
            />
          </v-col>
          <v-col cols="12" md="4">
            <v-switch
              v-model="draft.isActive"
              :label="t('notifier.mailTemplates.isActive')"
              color="primary"
              hide-details
              density="comfortable"
            />
          </v-col>
        </v-row>

        <v-text-field
          :model-value="inferredVariables"
          :label="t('notifier.mailTemplates.fieldVariables')"
          readonly
          density="comfortable"
          class="mb-3"
        />

        <v-textarea
          v-model="draft.sampleContextJson"
          :label="t('notifier.mailTemplates.fieldSampleContext')"
          :hint="t('notifier.mailTemplates.fieldSampleContextHint')"
          persistent-hint
          rows="6"
          auto-grow
          density="comfortable"
          class="mb-2 font-mono-body"
        />

        <p class="text-caption text-medium-emphasis mb-2">{{ previewHint }}</p>

        <v-alert v-if="previewError" type="error" variant="tonal" density="compact" class="mb-2">
          {{ previewError }}
        </v-alert>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-btn
          variant="tonal"
          prepend-icon="mdi-eye-outline"
          :disabled="!canPreview"
          :loading="previewLoading"
          @click="runPreview"
        >
          {{ t('notifier.mailTemplates.preview') }}
        </v-btn>
        <v-spacer />
        <v-btn variant="text" @click="close">{{ t('notifier.mailTemplates.cancel') }}</v-btn>
        <v-btn color="primary" variant="flat" :disabled="!canSave" :loading="saving" @click="submit">
          {{ t('notifier.mailTemplates.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-dialog v-model="previewOpen" max-width="900" scrollable>
    <v-card rounded="lg">
      <v-card-title>{{ t('notifier.mailTemplates.previewTitle') }}</v-card-title>
      <v-card-subtitle v-if="previewResult" class="text-wrap">{{ previewResult.subject }}</v-card-subtitle>
      <v-card-text>
        <div
          v-if="previewResult"
          class="mail-preview-frame pa-2 rounded-lg border"
          v-html="sanitizedPreviewHtml"
        />
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="previewOpen = false">{{ t('notifier.mailTemplates.close') }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
  </div>
</template>

<style scoped>
.font-mono-body :deep(textarea) {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 0.85rem;
}
.mail-preview-frame {
  max-height: 65vh;
  overflow: auto;
  background: #fff;
}
</style>
