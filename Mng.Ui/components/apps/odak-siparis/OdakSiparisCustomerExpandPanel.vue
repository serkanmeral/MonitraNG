<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import OdakSiparisCustomerContactsPanel from '@/components/apps/odak-siparis/OdakSiparisCustomerContactsPanel.vue';
import OdakSiparisCustomerDashboardPanel from '@/components/apps/odak-siparis/OdakSiparisCustomerDashboardPanel.vue';
import OdakSiparisCustomerQualityReqPanel from '@/components/apps/odak-siparis/OdakSiparisCustomerQualityReqPanel.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakCustomerRow } from '@/utils/odakSiparisConfig';
import { packageDataId } from '@/utils/odakSiparisService';

const props = defineProps<{
  customerRow: OdakCustomerRow;
  refreshToken?: number;
}>();

const { t } = useAppI18n();
const activeTab = ref<'dashboard' | 'contacts' | 'quality'>('dashboard');

const panelKey = computed(() => {
  const row = props.customerRow;
  return `${packageDataId(row)}|${row.isMusteri}|${row.isTedarikci}|${props.refreshToken ?? 0}`;
});

watch(
  () => packageDataId(props.customerRow),
  () => {
    activeTab.value = 'dashboard';
  }
);
</script>

<template>
  <div class="odak-customer-expand-panel pa-4">
    <v-tabs v-model="activeTab" color="primary" density="compact" class="mb-3">
      <v-tab value="dashboard">{{ t('odakSiparis.detail.tabs.dashboard') }}</v-tab>
      <v-tab value="contacts">{{ t('odakSiparis.customers.contacts.title') }}</v-tab>
      <v-tab value="quality">{{ t('odakSiparis.customers.qualityReqs.title') }}</v-tab>
    </v-tabs>

    <OdakSiparisCustomerDashboardPanel
      v-if="activeTab === 'dashboard'"
      :key="`dash-${panelKey}`"
      :customer-row="customerRow"
    />
    <OdakSiparisCustomerContactsPanel
      v-if="activeTab === 'contacts'"
      :key="`contacts-${panelKey}`"
      :customer-row="customerRow"
    />
    <OdakSiparisCustomerQualityReqPanel
      v-if="activeTab === 'quality'"
      :key="`quality-${panelKey}`"
      :customer-row="customerRow"
    />
  </div>
</template>

<style scoped>
.odak-customer-expand-panel {
  background: rgba(var(--v-theme-surface-variant), 0.25);
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
