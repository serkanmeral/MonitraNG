<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OpWorkspaceFolder } from '@/types/apps/operationCore';

export type OcFolderDialogMode = 'create' | 'rename' | 'move' | 'delete';

const props = defineProps<{
  modelValue: boolean;
  mode: OcFolderDialogMode;
  name?: string;
  viewGroups?: string[];
  targetId?: string | null;
  targets: { id: string; title: string; disabled?: boolean }[];
  saving?: boolean;
  error?: string | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [open: boolean];
  save: [payload: { name: string; viewGroups: string[]; targetId: string | null }];
}>();

const { t } = useAppI18n();
const draftName = ref('');
const draftGroups = ref<string[]>([]);
const draftTarget = ref<string | null>(null);

watch(
  () => props.modelValue,
  (open) => {
    if (!open) return;
    draftName.value = props.name ?? '';
    draftGroups.value = [...(props.viewGroups ?? [])];
    draftTarget.value = props.targetId || '__root__';
  }
);

const title = computed(() => {
  if (props.mode === 'rename') return t('operationCore.workspace.folders.renameTitle');
  if (props.mode === 'move') return t('operationCore.workspace.folders.moveTitle');
  if (props.mode === 'delete') return t('operationCore.workspace.folders.deleteTitle');
  return t('operationCore.workspace.folders.createTitle');
});

const canSave = computed(() => {
  if (props.mode === 'delete' || props.mode === 'move') return true;
  return draftName.value.trim().length > 0;
});

function close() {
  emit('update:modelValue', false);
}

function targetItemProps(item: { disabled?: boolean }) {
  return { disabled: !!item.disabled };
}

function save() {
  emit('save', {
    name: draftName.value.trim(),
    viewGroups: [...draftGroups.value],
    targetId: draftTarget.value ? draftTarget.value : null,
  });
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="480" @update:model-value="emit('update:modelValue', $event)">
    <v-card>
      <v-card-title class="text-subtitle-1">{{ title }}</v-card-title>
      <v-card-text>
        <v-alert v-if="error" type="error" variant="tonal" class="mb-3" density="compact">
          {{ error }}
        </v-alert>
        <template v-if="mode === 'delete'">
          <p class="text-body-2 mb-0">{{ t('operationCore.workspace.folders.deleteHint') }}</p>
        </template>
        <template v-else-if="mode === 'move'">
          <v-select
            v-model="draftTarget"
            :items="targets"
            item-title="title"
            item-value="id"
            :item-props="targetItemProps"
            :label="t('operationCore.workspace.folders.target')"
            density="comfortable"
            variant="outlined"
          />
        </template>
        <template v-else>
          <v-text-field
            v-model="draftName"
            :label="t('operationCore.workspace.folders.name')"
            density="comfortable"
            variant="outlined"
            autofocus
            maxlength="120"
            @keyup.enter="canSave && save()"
          />
          <MngDirectoryPickerField
            v-model="draftGroups"
            entity="group"
            multiple
            :label="t('operationCore.workspace.folders.viewGroups')"
            :hint="t('operationCore.workspace.folders.viewGroupsHint')"
            persistent-hint
            density="comfortable"
            variant="outlined"
          />
        </template>
      </v-card-text>
      <v-card-actions class="justify-end">
        <v-btn variant="text" class="text-none" @click="close">
          {{ t('operationCore.workspace.folders.cancel') }}
        </v-btn>
        <v-btn
          :color="mode === 'delete' ? 'error' : 'primary'"
          variant="flat"
          class="text-none"
          :loading="saving"
          :disabled="!canSave"
          @click="save"
        >
          {{ mode === 'delete' ? t('operationCore.workspace.folders.delete') : t('operationCore.workspace.folders.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
