<script setup lang="ts">
definePageMeta({ layout: 'default' });

const router = useRouter();

onMounted(() => {
  router.replace({ path: '/apps/monitoring', query: { tab: 'periods' } });
});
</script>

<template>
  <div class="d-flex align-center justify-center pa-8">
    <p class="text-medium-emphasis">Yönlendiriliyor...</p>
  </div>
</template>
