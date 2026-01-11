<script setup lang="ts">
import DatasetCategoryForm from '../form.vue';

definePageMeta({
  layout: 'default',
});

const route = useRoute();
const dataId = computed(() => route.params.dataId as string);
</script>

<template>
  <DatasetCategoryForm :edit-data-id="dataId" />
</template>
