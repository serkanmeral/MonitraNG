<script setup lang="ts">
import OcWorkspaceSlaPoliciesExplorer from '@/components/apps/operation-core/workspace-definitions/OcWorkspaceSlaPoliciesExplorer.vue';

defineProps<{
  workspaceId: string;
}>();
</script>

<template>
  <OcWorkspaceSlaPoliciesExplorer :workspace-id="workspaceId" />
</template>
