<script setup lang="ts">
import { ref, watch } from 'vue';
import PmDiLibrary from '@/components/apps/project-management/PmDiLibrary.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { resolveOcWorkspaceLibrary } from '@/utils/ocWorkspaceLibrary';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();
const loading = ref(false);
const missing = ref(false);
const hubId = ref<string | null>(null);

async function load(workspaceId: string) {
  const id = workspaceId.trim();
  loading.value = true;
  missing.value = false;
  hubId.value = null;
  if (!id) {
    missing.value = true;
    loading.value = false;
    return;
  }
  try {
    const folderId = await resolveOcWorkspaceLibrary(id);
    if (!folderId) missing.value = true;
    else hubId.value = folderId;
  } catch {
    missing.value = true;
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.workspaceId,
  (workspaceId) => {
    void load(workspaceId);
  },
  { immediate: true },
);
</script>

<template>
  <div>
    <v-progress-linear v-if="loading" indeterminate color="primary" />
    <v-alert v-else-if="missing" type="warning" variant="tonal">
      {{ t('operationCore.workspace.library.missingRoot') }}
    </v-alert>
    <PmDiLibrary v-else-if="hubId" :locked-hub-id="hubId" />
  </div>
</template>
