<script setup lang="ts">
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OrgChartCanvas from '@/components/apps/organizasyon/OrgChartCanvas.client.vue';

definePageMeta({ layout: 'default' });

const breadcrumbs = [
  { text: 'AnkaraBT', disabled: false, href: '/dashboards/analytical' },
  { text: 'Organizasyon', disabled: true, href: '#' },
];
</script>

<template>
  <div>
    <BaseBreadcrumb title="Organizasyon şeması" :breadcrumbs="breadcrumbs" />
    <OrgChartCanvas />
  </div>
</template>
