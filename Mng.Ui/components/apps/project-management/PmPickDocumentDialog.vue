<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import DiPickResourceDialog from '@/components/apps/document-intelligence/DiPickResourceDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import type { DiResource } from '@/types/apps/documentIntelligence';
import { ensureProjectLibrary } from '@/utils/pmProjectLibrary';

const props = withDefaults(
  defineProps<{
    modelValue: boolean;
    projectId: string;
    projectCode: string;
    hubFolderId?: string | null;
    markdownOnly?: boolean;
    excludeResourceIds?: string[];
  }>(),
  { markdownOnly: false, excludeResourceIds: () => [] },
);

const emit = defineEmits<{
  'update:modelValue': [boolean];
  pick: [DiResource];
  hubReady: [string];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const ensuring = ref(false);
const resolvedHub = ref((props.hubFolderId || '').trim());

const open = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value),
});

const pickerOpen = computed({
  get: () => open.value && Boolean(resolvedHub.value) && !ensuring.value,
  set: (value: boolean) => {
    open.value = value;
  },
});

watch(
  () => props.modelValue,
  async (isOpen) => {
    if (!isOpen) return;
    ensuring.value = true;
    try {
      const hubId = await ensureProjectLibrary(props.projectId, props.projectCode, props.hubFolderId);
      resolvedHub.value = hubId;
      if (hubId && hubId !== (props.hubFolderId || '').trim()) emit('hubReady', hubId);
    } catch (error) {
      resolvedHub.value = '';
      panelError(error, 'projectManagement.errors.loadFailed');
      open.value = false;
    } finally {
      ensuring.value = false;
    }
  },
);
</script>

<template>
  <DiPickResourceDialog
    v-model="pickerOpen"
    :root-folder-id="resolvedHub"
    :markdown-only="markdownOnly"
    :exclude-resource-ids="excludeResourceIds"
    :title="t('projectManagement.pickDocument.title')"
    :hint="t('projectManagement.pickDocument.hint')"
    :confirm-label="t('projectManagement.pickDocument.confirm')"
    :root-label="t('projectManagement.library.title')"
    @pick="emit('pick', $event)"
  />
</template>
