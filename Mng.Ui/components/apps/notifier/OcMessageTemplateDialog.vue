<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { MessageTemplate } from '@/types/apps/messageTemplates';
import {
  buildMessageTemplatePayload,
  isSystemMessageTemplate,
  newMessageTemplateDraft,
  parseMessageTemplateToDraft,
  renderMessageTemplatePreview,
  validateMessageTemplateDraft,
  type OcMessageTemplateDraft,
} from '@/utils/ocMessageTemplates';
import { extractPlaceholderPaths, parseSampleContextJson } from '@/utils/ocMailTemplates';

const props = defineProps<{
  modelValue: boolean;
  template: MessageTemplate | null;
  saving?: boolean;
  categoryOptions?: string[];
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [Record<string, unknown>];
}>();

const { t } = useAppI18n();
const draft = ref<OcMessageTemplateDraft>(newMessageTemplateDraft());
const previewOpen = ref(false);
const previewText = ref('');
const previewError = ref<string | null>(null);

const isEdit = computed(() => !!props.template?.__dataId);
const isSystem = computed(() => (props.template ? isSystemMessageTemplate(props.template) : false));
const canSave = computed(() => validateMessageTemplateDraft(draft.value, isEdit.value) === null);

const inferredVariables = computed(() =>
  extractPlaceholderPaths(draft.value.bodyText).join(', ')
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

const channelItems = ['telegram'];

watch(
  () => [props.modelValue, props.template?.__dataId] as const,
  ([open]) => {
    if (open) {
      draft.value = props.template
        ? parseMessageTemplateToDraft(props.template)
        : newMessageTemplateDraft();
      previewText.value = '';
      previewError.value = null;
    }
  }
);

function close() {
  emit('update:modelValue', false);
}

function submit() {
  if (!canSave.value) return;
  emit('save', buildMessageTemplatePayload(draft.value, isEdit.value));
}

function runPreview() {
  previewError.value = null;
  const context = parseSampleContextJson(draft.value.sampleContextJson);
  if (context === null) {
    previewError.value = t('notifier.messageTemplates.invalidSampleContext');
    return;
  }
  previewText.value = renderMessageTemplatePreview(draft.value.bodyText, context);
  previewOpen.value = true;
}
</script>

<template>
  <div>
    <v-dialog
      :model-value="modelValue"
      max-width="840"
      persistent
      scrollable
      @update:model-value="emit('update:modelValue', $event)"
    >
      <v-card rounded="lg">
        <v-card-title class="d-flex align-center py-4">
          <v-icon icon="mdi-message-text-outline" color="primary" class="me-2" />
          {{
            isEdit
              ? t('notifier.messageTemplates.editTemplate')
              : t('notifier.messageTemplates.addTemplate')
          }}
          <v-chip v-if="isSystem" size="small" variant="tonal" class="ms-2">system</v-chip>
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="close" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pt-4" style="max-height: 72vh">
          <v-alert type="info" variant="tonal" density="compact" class="mb-4 rounded-lg">
            {{ t('notifier.messageTemplates.dialogHint') }}
          </v-alert>

          <v-row dense>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="draft.templateKey"
                :label="t('notifier.messageTemplates.fieldTemplateKey')"
                :hint="t('notifier.messageTemplates.fieldTemplateKeyHint')"
                persistent-hint
                :disabled="isEdit"
                density="comfortable"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="draft.name"
                :label="t('notifier.messageTemplates.fieldName')"
                density="comfortable"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-select
                v-model="draft.channel"
                :items="channelItems"
                :label="t('notifier.messageTemplates.fieldChannel')"
                density="comfortable"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-combobox
                v-model="draft.category"
                :items="categoryItems"
                :label="t('notifier.messageTemplates.fieldCategory')"
                density="comfortable"
                :disabled="isSystem"
                clearable
              />
            </v-col>
          </v-row>

          <v-textarea
            v-model="draft.description"
            :label="t('notifier.messageTemplates.fieldDescription')"
            rows="2"
            auto-grow
            density="comfortable"
            class="mb-3"
          />

          <v-textarea
            v-model="draft.bodyText"
            :label="t('notifier.messageTemplates.fieldBodyText')"
            :hint="t('notifier.messageTemplates.placeholderHint')"
            persistent-hint
            rows="8"
            auto-grow
            density="comfortable"
            class="mb-3 font-mono-body"
          />

          <v-row dense>
            <v-col cols="12" md="4">
              <v-text-field
                v-model="draft.parseMode"
                :label="t('notifier.messageTemplates.fieldParseMode')"
                :hint="t('notifier.messageTemplates.fieldParseModeHint')"
                persistent-hint
                density="comfortable"
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field
                v-model="draft.locale"
                :label="t('notifier.messageTemplates.fieldLocale')"
                density="comfortable"
              />
            </v-col>
            <v-col cols="12" md="4">
              <v-switch
                v-model="draft.isActive"
                :label="t('notifier.messageTemplates.isActive')"
                color="primary"
                hide-details
                density="comfortable"
              />
            </v-col>
          </v-row>

          <v-text-field
            :model-value="inferredVariables"
            :label="t('notifier.messageTemplates.fieldVariables')"
            readonly
            density="comfortable"
            class="mb-3"
          />

          <v-textarea
            v-model="draft.sampleContextJson"
            :label="t('notifier.messageTemplates.fieldSampleContext')"
            :hint="t('notifier.messageTemplates.fieldSampleContextHint')"
            persistent-hint
            rows="5"
            auto-grow
            density="comfortable"
            class="mb-2 font-mono-body"
          />

          <v-alert v-if="previewError" type="error" variant="tonal" density="compact" class="mb-2">
            {{ previewError }}
          </v-alert>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-btn variant="tonal" prepend-icon="mdi-eye-outline" @click="runPreview">
            {{ t('notifier.messageTemplates.preview') }}
          </v-btn>
          <v-spacer />
          <v-btn variant="text" @click="close">{{ t('notifier.messageTemplates.cancel') }}</v-btn>
          <v-btn color="primary" variant="flat" :disabled="!canSave" :loading="saving" @click="submit">
            {{ t('notifier.messageTemplates.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="previewOpen" max-width="560" scrollable>
      <v-card rounded="lg">
        <v-card-title>{{ t('notifier.messageTemplates.previewTitle') }}</v-card-title>
        <v-card-text>
          <pre class="message-preview-pre pa-3 rounded-lg">{{ previewText }}</pre>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="previewOpen = false">{{ t('notifier.messageTemplates.close') }}</v-btn>
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
.message-preview-pre {
  white-space: pre-wrap;
  word-break: break-word;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 0.9rem;
  background: rgba(var(--v-theme-surface-variant), 0.25);
  margin: 0;
}
</style>
