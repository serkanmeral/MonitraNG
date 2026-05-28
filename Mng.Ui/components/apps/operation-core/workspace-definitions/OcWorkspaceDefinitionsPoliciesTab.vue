<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import OcWorkspaceFieldPolicyExplorer from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceFieldPolicyExplorer.vue';
import { ocListPoolFieldsForWorkspace } from '@/services/operationCoreService';
import { OC_FORM_LAYOUT_CORE_FIELD_KEYS } from '@/utils/ocFieldDefinitions';
import type { OpField } from '@/types/apps/operationCore';

const props = defineProps<{
  workspaceId: string;
}>();

const poolFields = ref<OpField[]>([]);

const catalogFieldKeys = computed(() => {
  const poolKeys = poolFields.value.map((f) => f.key).filter(Boolean);
  return [...new Set([...OC_FORM_LAYOUT_CORE_FIELD_KEYS, ...poolKeys])];
});

async function loadCatalog() {
  if (!props.workspaceId) {
    poolFields.value = [];
    return;
  }
  try {
    poolFields.value = await ocListPoolFieldsForWorkspace(props.workspaceId);
  } catch {
    poolFields.value = [];
  }
}

watch(
  () => props.workspaceId,
  () => {
    void loadCatalog();
  },
  { immediate: true }
);
</script>

<template>
  <OcWorkspaceFieldPolicyExplorer
    :workspace-id="workspaceId"
    :catalog-field-keys="catalogFieldKeys"
    :pool-fields="poolFields"
  />
</template>
