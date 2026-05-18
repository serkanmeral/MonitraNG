<script setup lang="ts">
/** Eski URL: durum akışı → proje düzenleyici «Durum akışı» sekmesi. */
definePageMeta({ layout: 'default' });

const route = useRoute();
await navigateTo(
  {
    path: `/apps/task-manager/projects/${String(route.params.id ?? '')}/edit`,
    query: { tab: 'workflow' },
  },
  { replace: true }
);
</script>

<template>
  <div class="d-flex justify-center align-center py-12">
    <v-progress-circular indeterminate color="primary" size="48" />
  </div>
</template>
