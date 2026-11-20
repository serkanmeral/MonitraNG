<script setup lang="ts">
definePageMeta({ layout: 'blank' });

const route = useRoute();

onMounted(() => {
  const doorKey = String(route.params.doorKey || '').trim();
  const kioskToken = String(route.query.k || '').trim();
  if (doorKey && kioskToken) {
    const current = localStorage.getItem('pdks.screen');
    if (!current) {
      localStorage.setItem('pdks.screen', JSON.stringify({ doorKey, kioskToken }));
    }
  }
  navigateTo('/kapi', { replace: true });
});
</script>

<template>
  <p style="font-family: sans-serif; padding: 24px;">Ekran açılıyor.</p>
</template>
