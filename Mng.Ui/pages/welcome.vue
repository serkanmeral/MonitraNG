<script setup lang="ts">
/** Eski /welcome adresi — ana sayfaya yönlendir */
definePageMeta({ layout: 'default' });

onMounted(() => {
  navigateTo('/', { replace: true });
});
</script>

<template>
  <div />
</template>
