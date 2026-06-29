<script setup lang="ts">
import { computed } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OdakSiparisGlobalCustomerDashboard from '@/components/apps/odak-siparis/OdakSiparisGlobalCustomerDashboard.vue';
import { useAppI18n } from '@/composables/useAppI18n';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();

const page = computed(() => ({ title: t('odakSiparis.dashboard.customerGlobal.pageTitle') }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('dashboards.breadcrumbs.dashboards'), disabled: false, href: '/apps/dashboards' },
  { text: t('odakSiparis.dashboard.customerGlobal.pageTitle'), disabled: true, href: '#' },
]);
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
    <v-container fluid class="pa-0">
      <OdakSiparisGlobalCustomerDashboard />
    </v-container>
  </div>
</template>
