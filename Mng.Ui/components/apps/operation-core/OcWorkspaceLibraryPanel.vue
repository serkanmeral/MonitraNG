<script setup lang="ts">
import { ref, watch } from 'vue';
import PmDiLibrary from '@/components/apps/project-management/PmDiLibrary.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  resolveOcWorkspaceLibraryState,
  rewriteOcWorkspaceLibraryPermissions,
} from '@/utils/ocWorkspaceLibrary';

const props = defineProps<{
  workspaceId: string;
}>();

const { t } = useAppI18n();
const loading = ref(false);
const missing = ref(false);
const hubId = ref<string | null>(null);
const detached = ref(false);
const canRewrite = ref(false);
const rewriting = ref(false);
const rewriteError = ref(false);
const libraryKey = ref(0);

async function load(workspaceId: string) {
  const id = workspaceId.trim();
  loading.value = true;
  missing.value = false;
  hubId.value = null;
  detached.value = false;
  canRewrite.value = false;
  rewriteError.value = false;
  if (!id) {
    missing.value = true;
    loading.value = false;
    return;
  }
  try {
    const state = await resolveOcWorkspaceLibraryState(id);
    if (!state.folderId) missing.value = true;
    else hubId.value = state.folderId;
    detached.value = state.detached;
    canRewrite.value = state.canRewrite;
  } catch {
    missing.value = true;
  } finally {
    loading.value = false;
  }
}

async function rewrite() {
  const id = props.workspaceId.trim();
  if (!id || rewriting.value) return;
  rewriting.value = true;
  rewriteError.value = false;
  try {
    const state = await rewriteOcWorkspaceLibraryPermissions(id);
    if (state.failed || !state.folderId) {
      rewriteError.value = true;
      return;
    }
    hubId.value = state.folderId;
    detached.value = state.detached;
    canRewrite.value = state.canRewrite;
    libraryKey.value += 1;
  } catch {
    rewriteError.value = true;
  } finally {
    rewriting.value = false;
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
    <template v-else-if="hubId">
      <v-alert v-if="detached" type="info" variant="tonal" class="mb-3">
        <div class="d-flex flex-wrap align-center justify-space-between ga-2">
          <span>{{ t('operationCore.workspace.library.detached') }}</span>
          <v-btn
            v-if="canRewrite"
            size="small"
            variant="flat"
            color="primary"
            :loading="rewriting"
            @click="rewrite"
          >
            {{ t('operationCore.workspace.library.rewrite') }}
          </v-btn>
        </div>
      </v-alert>
      <v-alert v-if="rewriteError" type="error" variant="tonal" class="mb-3">
        {{ t('operationCore.workspace.library.rewriteError') }}
      </v-alert>
      <PmDiLibrary :key="libraryKey" :locked-hub-id="hubId" />
    </template>
  </div>
</template>
