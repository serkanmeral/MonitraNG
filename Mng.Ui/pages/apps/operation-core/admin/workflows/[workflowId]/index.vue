<script setup lang="ts">
definePageMeta({ layout: 'blank' });

const route = useRoute();
const workflowId = String(route.params.workflowId ?? '');

await navigateTo(`/apps/automation-center/workflows/${encodeURIComponent(workflowId)}`, { replace: true });
</script>

<template>
  <div class="d-flex align-center justify-center pa-12">
    <v-progress-circular indeterminate color="primary" size="48" />
    <span class="ml-3">Yönlendiriliyor…</span>
  </div>
</template>
