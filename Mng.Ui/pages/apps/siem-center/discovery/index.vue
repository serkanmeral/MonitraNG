<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AcSiemDiscoveryCoverageMap from '@/components/apps/siem-center/AcSiemDiscoveryCoverageMap.vue';
import { useSiemCenterBreadcrumbs } from '@/composables/useSiemCenterBreadcrumbs';
import { useAppI18n } from '@/composables/useAppI18n';
import { useDomain, type Domain } from '@/composables/useDomain';
import { useAuthStore } from '@/stores/auth';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const auth = useAuthStore();
const { getCurrentDomain } = useDomain();
const currentDomain = ref<Domain | null>(null);

const discoveryRootLabel = computed(() =>
  currentDomain.value?.discoveryRootLabel?.trim()
  || currentDomain.value?.displayName?.trim()
  || currentDomain.value?.name?.trim()
  || auth.domainName?.trim()
  || t('siemCenter.discovery.rootLabel'),
);

const { breadcrumbs } = useSiemCenterBreadcrumbs({
  subPage: true,
  tail: computed(() => ({
    text: t('siemCenter.discovery.pageTitle'),
    disabled: true,
  })),
});

onMounted(async () => {
  if (!auth.isManager) {
    void navigateTo('/unauthorized');
    return;
  }

  currentDomain.value = await getCurrentDomain();
});
</script>

<template>
  <div class="siem-discovery-page">
    <BaseBreadcrumb
      :title="t('siemCenter.discovery.menuTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <AcSiemDiscoveryCoverageMap :root-label="discoveryRootLabel" />
  </div>
</template>
