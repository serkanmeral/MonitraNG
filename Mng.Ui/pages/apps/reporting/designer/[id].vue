<script setup lang="ts">
import ReportingDesignerView from '@/components/apps/reporting/ReportingDesignerView.vue';

const route = useRoute();

const reportId = computed(() => {
  const raw = route.params.id;
  const id = Array.isArray(raw) ? raw[0] : raw;
  const trimmed = id != null ? String(id).trim() : '';
  return trimmed || undefined;
});
</script>

<template>
  <ReportingDesignerView v-if="reportId" :report-id="reportId" />
</template>
