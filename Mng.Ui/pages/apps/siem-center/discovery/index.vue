<script setup lang="ts">
import { computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AcSiemDiscoveryCoverageMap from '@/components/apps/siem-center/AcSiemDiscoveryCoverageMap.vue';
import { useSiemCenterBreadcrumbs } from '@/composables/useSiemCenterBreadcrumbs';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const auth = useAuthStore();

const { breadcrumbs } = useSiemCenterBreadcrumbs({
  subPage: true,
  tail: computed(() => ({
    text: t('siemCenter.discovery.pageTitle'),
    disabled: true,
  })),
});

onMounted(() => {
  if (!auth.isManager) {
    void navigateTo('/unauthorized');
  }
});
</script>

<template>
  <div class="siem-discovery-page">
    <BaseBreadcrumb
      :title="t('siemCenter.discovery.menuTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <AcSiemDiscoveryCoverageMap />
  </div>
</template>
