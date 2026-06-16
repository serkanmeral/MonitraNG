<script setup lang="ts">
/** Eski detay route — liste + expand panel kullanilir. */
definePageMeta({ layout: 'default' });

const route = useRoute();
const router = useRouter();

const packageId = computed(() => String(route.params.id ?? '').trim());
const tab = computed(() => (route.query.tab === 'lines' ? 'lines' : undefined));

onMounted(() => {
  const id = packageId.value;
  if (!id) {
    void router.replace('/apps/odak-siparis/packages');
    return;
  }
  void router.replace({
    path: '/apps/odak-siparis/packages',
    query: { expand: id, ...(tab.value ? { tab: tab.value } : {}) },
  });
});
</script>

<template>
  <div class="d-flex align-center justify-center pa-8">
    <v-progress-circular indeterminate color="primary" />
  </div>
</template>
