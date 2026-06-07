<script setup lang="ts">
import OcWorkspaceMailPoliciesExplorer from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceMailPoliciesExplorer.vue';

defineProps<{
  workspaceId: string;
}>();
</script>

<template>
  <OcWorkspaceMailPoliciesExplorer :workspace-id="workspaceId" />
</template>
