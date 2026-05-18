<script setup lang="ts">
/**
 * Redirect: /apps/organization → /apps/monitoring/organization
 * Organizasyon sayfası /apps/monitoring/organization altına taşındı.
 */
definePageMeta({
  layout: 'blank',
});

await navigateTo('/apps/monitoring/organization', { replace: true });
</script>

<template>
  <div class="d-flex align-center justify-center pa-12">
    <v-progress-circular indeterminate color="primary" size="48" />
    <span class="ml-3">Yönlendiriliyor...</span>
  </div>
</template>
