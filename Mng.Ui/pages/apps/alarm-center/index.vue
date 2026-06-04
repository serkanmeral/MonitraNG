<script setup lang="ts">
definePageMeta({ layout: 'blank' });
await navigateTo('/apps/alarm-center/alarms', { replace: true });
</script>

<template>
  <div class="d-flex align-center justify-center pa-12">
    <v-progress-circular indeterminate color="primary" size="48" />
    <span class="ml-3">Yönlendiriliyor…</span>
  </div>
</template>
