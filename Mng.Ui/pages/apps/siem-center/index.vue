<script setup lang="ts">
import { computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AcSiemCenterDashboard from '@/components/apps/siem-center/AcSiemCenterDashboard.vue';
import { useSiemCenterBreadcrumbs } from '@/composables/useSiemCenterBreadcrumbs';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const auth = useAuthStore();

const { breadcrumbs } = useSiemCenterBreadcrumbs({
  tail: computed(() => ({
    text: t('siemCenter.dashboard.pageTitle'),
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
  <div class="siem-dashboard-page">
    <BaseBreadcrumb
      :title="t('siemCenter.dashboard.pageTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <div class="siem-hero mb-4 pa-4 pa-md-5 rounded-lg">
      <h1 class="text-h5 font-weight-bold mb-2">
        {{ t('siemCenter.dashboard.pageTitle') }}
      </h1>
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('siemCenter.dashboard.pageSubtitle') }}
      </p>
    </div>

    <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
      <AcSiemCenterDashboard />
    </v-card>
  </div>
</template>
