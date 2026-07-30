<script setup lang="ts">
definePageMeta({ layout: 'default' });

await navigateTo(
  {
    path: '/apps/siem-center/settings',
    query: { tab: 'dictionary' },
  },
  { redirectCode: 301, replace: true },
);
</script>

<template>
  <div />
</template>
