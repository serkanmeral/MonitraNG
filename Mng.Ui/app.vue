<script setup lang="ts">
import { onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';

const route = useRoute();

onMounted(() => {
  console.log('[app.vue] App mounted, current route:', {
    path: route.path,
    fullPath: route.fullPath,
    name: route.name,
    query: route.query,
    matched: route.matched?.map(m => ({ 
      path: m.path, 
      name: m.name,
      components: Object.keys(m.components || {}),
      meta: m.meta
    })) || [],
    params: route.params
  });
  
  // Check if route is actually matched
  if (route.matched && route.matched.length > 0) {
    console.log('[app.vue] Route matched successfully, components:', route.matched[0].components);
    console.log('[app.vue] Matched route details:', {
      path: route.matched[0].path,
      name: route.matched[0].name,
      meta: route.matched[0].meta,
      components: Object.keys(route.matched[0].components || {})
    });
  } else {
    console.warn('[app.vue] Route NOT matched! This is a 404.');
  }
});

// Watch route changes
watch(() => route.path, (newPath, oldPath) => {
  console.log('[app.vue] Route changed:', {
    from: oldPath,
    to: newPath,
    name: route.name,
    fullPath: route.fullPath,
    matched: route.matched?.map(m => ({ path: m.path, name: m.name })) || []
  });
}, { immediate: true });
</script>

<template>
  <NuxtLoadingIndicator />
  <NuxtLayout >
    <NuxtPage />
  </NuxtLayout>
</template>
