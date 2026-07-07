<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { diCloneResource } from '@/services/documentIntelligenceService';
import { isDiCloneable } from '@/utils/diFilePreview';
import DiFolderPickerList from '@/components/apps/document-intelligence/DiFolderPickerList.vue';
import { DI_RESOURCE_TYPE } from '@/types/apps/documentIntelligence';
import type { DiResource, DiTreeNode } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  resource: DiResource | null;
  loadChildren: (parentId: string | null) => Promise<DiTreeNode[]>;
  isLoading?: (parentId: string | null) => boolean;
  loading?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  cloned: [resource: DiResource];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const name = ref('');
const documentNo = ref('');
const parentId = ref<string | null>(null);
const submitting = ref(false);
const error = ref<string | null>(null);

const isMarkdown = computed(() => props.resource?.type === DI_RESOURCE_TYPE.markdown);
const isManualDocx = computed(() => props.resource != null && isDiCloneable(props.resource) && !isMarkdown.value);

const sourceLabel = computed(() => {
  const r = props.resource;
  if (!r) return '';
  if (r.type === 'markdown') return r.title || r.name;
  const parts = [r.name];
  if (r.documentNo) parts.unshift(r.documentNo);
  return parts.filter(Boolean).join(' · ');
});

const canSubmit = computed(() => {
  if (!name.value.trim()) return false;
  if (isManualDocx.value && !documentNo.value.trim()) return false;
  return true;
});

function close() {
  emit('update:modelValue', false);
}

function resetForm() {
  name.value = '';
  documentNo.value = '';
  parentId.value = props.resource?.parentId ?? null;
  error.value = null;
}

function defaultCloneName(sourceName: string): string {
  const base = sourceName.trim();
  if (!base) return '';
  const copySuffix = t('documentIntelligence.cloneNameSuffix');
  if (base.toLowerCase().endsWith(copySuffix.toLowerCase())) return base;
  return `${base} ${copySuffix}`.trim();
}

watch(
  () => [props.modelValue, props.resource?.id] as const,
  ([open, id]) => {
    if (open && id && props.resource) {
      resetForm();
      name.value = defaultCloneName(props.resource.title || props.resource.name || '');
    }
  },
);

async function submit() {
  const resource = props.resource;
  if (!resource || !canSubmit.value) return;
  submitting.value = true;
  error.value = null;
  try {
    const created = await diCloneResource(resource.id, {
      parentId: parentId.value,
      name: name.value.trim(),
      documentNo: isManualDocx.value ? documentNo.value.trim() : undefined,
    });
    close();
    emit('cloned', created);
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.errors.clone');
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="520" @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="text-subtitle-1 font-weight-bold">
        {{ t('documentIntelligence.cloneTitle') }}
      </v-card-title>
      <v-card-text>
        <div v-if="resource" class="text-body-2 text-medium-emphasis mb-4">
          {{ t('documentIntelligence.cloneSourceLabel') }}: {{ sourceLabel }}
        </div>

        <v-text-field
          v-if="isManualDocx"
          v-model="documentNo"
          :label="t('documentIntelligence.documentNoLabel')"
          :hint="t('documentIntelligence.documentNoHint')"
          persistent-hint
          variant="outlined"
          density="comfortable"
          class="mb-3"
          autofocus
        />

        <v-text-field
          v-model="name"
          :label="isMarkdown ? t('documentIntelligence.pageTitle') : t('documentIntelligence.nativeDocumentNameLabel')"
          variant="outlined"
          density="comfortable"
          class="mb-3"
          :autofocus="!isManualDocx"
          hide-details
          @keydown.enter="canSubmit && submit()"
        />

        <div class="text-caption text-medium-emphasis mb-2">
          {{ t('documentIntelligence.cloneTargetFolder') }}
        </div>
        <DiFolderPickerList
          v-model="parentId"
          :load-children="loadChildren"
          :is-loading="isLoading"
        />

        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3 rounded-lg">
          {{ error }}
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="close">{{ t('documentIntelligence.cancel') }}</v-btn>
        <v-btn
          color="primary"
          variant="flat"
          class="text-none"
          :loading="submitting || loading"
          :disabled="!canSubmit"
          @click="submit"
        >
          {{ t('documentIntelligence.clone') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
