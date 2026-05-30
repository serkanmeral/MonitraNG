<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocCreateWorkspace, ocExtractDgErrorMessage } from '@/services/operationCoreService';
import { OC_WORKSPACE_TYPE_VALUES } from '@/types/apps/operationCore';

const props = defineProps<{
  modelValue: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  created: [string];
}>();

const { t } = useAppI18n();

const saving = ref(false);
const errorLocal = ref<string | null>(null);

const form = ref({
  name: '',
  workspaceType: 'team' as string,
  description: '',
  workItemKeyPrefix: '',
});

const workspaceTypeItems = computed(() =>
  OC_WORKSPACE_TYPE_VALUES.map((value) => ({
    value,
    title: t(`operationCore.workspaceDefinitions.general.workspaceType.${value}`),
  }))
);

const canSave = computed(() => form.value.name.trim().length > 0 && !saving.value);

function resetForm() {
  form.value = { name: '', workspaceType: 'team', description: '', workItemKeyPrefix: '' };
  errorLocal.value = null;
}

watch(
  () => props.modelValue,
  (open) => {
    if (open) resetForm();
  }
);

function close() {
  emit('update:modelValue', false);
}

async function save() {
  const name = form.value.name.trim();
  if (!name) return;
  saving.value = true;
  errorLocal.value = null;
  try {
    const id = await ocCreateWorkspace({
      name,
      workspaceType: form.value.workspaceType || null,
      description: form.value.description.trim() || null,
      workItemKeyPrefix: form.value.workItemKeyPrefix.trim() || null,
    });
    if (!id) {
      errorLocal.value = t('operationCore.workspaceDefinitions.create.saveError');
      return;
    }
    emit('created', id);
    close();
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.create.saveError')
    );
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    max-width="560"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center ga-2">
        <v-icon icon="mdi-folder-plus-outline" />
        {{ t('operationCore.workspaceDefinitions.create.title') }}
      </v-card-title>
      <v-divider />
      <v-card-text>
        <v-alert
          v-if="errorLocal"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          closable
          @click:close="errorLocal = null"
        >
          {{ errorLocal }}
        </v-alert>

        <p class="text-body-2 text-medium-emphasis mb-4">
          {{ t('operationCore.workspaceDefinitions.create.subtitle') }}
        </p>

        <v-text-field
          v-model="form.name"
          :label="t('operationCore.workspaceDefinitions.general.fieldName')"
          density="comfortable"
          autofocus
          required
          @keyup.enter="save"
        />
        <v-select
          v-model="form.workspaceType"
          class="mt-3"
          :items="workspaceTypeItems"
          item-title="title"
          item-value="value"
          :label="t('operationCore.workspaceDefinitions.general.fieldWorkspaceType')"
          density="comfortable"
        />
        <v-text-field
          v-model="form.workItemKeyPrefix"
          class="mt-3"
          :label="t('operationCore.workspaceDefinitions.general.fieldKeyPrefix')"
          :hint="t('operationCore.workspaceDefinitions.general.keyPrefixHint')"
          persistent-hint
          density="comfortable"
        />
        <v-textarea
          v-model="form.description"
          class="mt-3"
          :label="t('operationCore.workspaceDefinitions.general.fieldDescription')"
          rows="2"
          auto-grow
          density="comfortable"
          variant="outlined"
        />
      </v-card-text>
      <v-divider />
      <v-card-actions class="px-4 py-3">
        <v-spacer />
        <v-btn variant="text" class="text-none" :disabled="saving" @click="close">
          {{ t('operationCore.definitions.cancel') }}
        </v-btn>
        <v-btn
          color="primary"
          rounded="lg"
          class="text-none"
          :loading="saving"
          :disabled="!canSave"
          @click="save"
        >
          {{ t('operationCore.workspaceDefinitions.create.submit') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
