<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import OcWorkspaceRulesExplorer from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceRulesExplorer.vue';
import { ocListPoolFieldsForWorkspace } from '@/services/operationCoreService';
import { OC_FORM_LAYOUT_CORE_FIELD_KEYS } from '@/utils/ocFieldDefinitions';
import type { OpField } from '@/types/apps/operationCore';

const props = defineProps<{
  workspaceId: string;
}>();

const poolFields = ref<OpField[]>([]);
const loading = ref(false);

const catalogFieldKeys = computed(() => {
  const poolKeys = poolFields.value.map((f) => f.key).filter(Boolean);
  return [...new Set([...OC_FORM_LAYOUT_CORE_FIELD_KEYS, ...poolKeys])];
});

async function loadCatalog() {
  if (!props.workspaceId) {
    poolFields.value = [];
    return;
  }
  loading.value = true;
  try {
    poolFields.value = await ocListPoolFieldsForWorkspace(props.workspaceId);
  } finally {
    loading.value = false;
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
  <div class="oc-workspace-rules-tab">
    <OcWorkspaceRulesExplorer
      :workspace-id="workspaceId"
      :catalog-field-keys="catalogFieldKeys"
      :pool-fields="poolFields"
    />
  </div>
</template>
