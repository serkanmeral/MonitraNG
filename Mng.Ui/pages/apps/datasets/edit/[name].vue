<script setup lang="ts">
import DatasetForm from '../form.vue';

definePageMeta({
  layout: 'default',
});

const route = useRoute();
const datasetName = computed(() => route.params.name as string);
</script>

<template>
  <DatasetForm :edit-name="datasetName" />
</template>
