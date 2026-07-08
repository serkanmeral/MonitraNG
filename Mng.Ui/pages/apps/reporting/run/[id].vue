<script setup lang="ts">
import ReportingRunnerView from '@/components/apps/reporting/ReportingRunnerView.vue';
import { reportingDomainKey } from '@/services/reportingCatalogService';
import { bootstrapReportingCatalog } from '@/utils/reportingCatalogBootstrap';
import { useAuthStore } from '@/stores/auth';

definePageMeta({ layout: 'default' });

const route = useRoute();
const authStore = useAuthStore();

const reportId = computed(() => String(route.params.id ?? '').trim());

onMounted(() => {
  bootstrapReportingCatalog(
    reportingDomainKey(authStore.userInfo?.domain_id, authStore.userInfo?.domain_name)
  );
});
</script>

<template>
  <ReportingRunnerView v-if="reportId" :report-id="reportId" />
</template>
