<script setup lang="ts">
/**
 * Gömülü rapor görüntüleyici — ağaç / breadcrumb yok; blank layout.
 * Auth zorunlu. Örnek: /apps/reporting/embed?reportId=…&personId=…
 */
import { computed, onMounted, ref, watch } from 'vue';
import ReportingRunnerView from '@/components/apps/reporting/ReportingRunnerView.vue';
import { useAuthStore } from '@/stores/auth';
import {
  ReportingCatalogService,
  reportingDomainKey,
} from '@/services/reportingCatalogService';
import { bootstrapReportingCatalog } from '@/utils/reportingCatalogBootstrap';
import { canViewReportingReport } from '@/utils/reportingReportAccess';
import { REPORTING_EMBED_SHARE_PATH } from '@/utils/reportingShareLink';

definePageMeta({ layout: 'blank' });

const route = useRoute();
const authStore = useAuthStore();

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: Record<string, unknown>) => {
  if (i18n?.t) return i18n.t(key, params);
  if (i18n?.global?.t) return i18n.global.t(key, params);
  return key;
};

const domainKey = computed(() =>
  reportingDomainKey(authStore.userInfo?.domain_id, authStore.userInfo?.domain_name)
);

const catalogService = computed(() => new ReportingCatalogService(domainKey.value));

const reportId = computed(() => {
  const q = route.query.reportId;
  return typeof q === 'string' && q.trim() ? q.trim() : '';
});

const bootstrapped = ref(false);
const accessDenied = ref(false);

async function refreshAccess() {
  accessDenied.value = false;
  if (!reportId.value) return;
  await bootstrapReportingCatalog(domainKey.value);
  const report = catalogService.value.getReport(reportId.value);
  if (!report || !canViewReportingReport(report.visibilityPolicies, authStore.userGroups)) {
    accessDenied.value = true;
  }
  bootstrapped.value = true;
}

onMounted(() => {
  void refreshAccess();
});

watch([reportId, domainKey], () => {
  void refreshAccess();
});
</script>

<template>
  <div class="reporting-embed pa-3">
    <v-alert v-if="!reportId" type="warning" variant="tonal" density="compact">
      {{ t('reporting.embed.missingReport') }}
    </v-alert>
    <v-alert v-else-if="bootstrapped && accessDenied" type="error" variant="tonal" density="compact">
      {{ t('reporting.embed.accessDenied') }}
    </v-alert>
    <ReportingRunnerView
      v-else-if="reportId && bootstrapped && !accessDenied"
      :key="reportId"
      :report-id="reportId"
      embedded
      :show-admin-tools="false"
      :share-base-path="REPORTING_EMBED_SHARE_PATH"
    />
    <div v-else-if="reportId && !bootstrapped" class="pa-4">
      <v-progress-linear indeterminate color="primary" />
    </div>
  </div>
</template>

<style scoped>
.reporting-embed {
  min-height: 100vh;
  max-width: 100%;
}
</style>
