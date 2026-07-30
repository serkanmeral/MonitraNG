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

    <div class="siem-hero mb-4 pa-4 pa-md-5 rounded-lg">
      <div class="d-flex flex-wrap align-start justify-space-between gap-3">
        <div>
          <h1 class="text-h5 font-weight-bold mb-2">
            {{ t('siemCenter.discovery.pageTitle') }}
          </h1>
          <p class="text-body-2 text-medium-emphasis mb-0">
            {{ t('siemCenter.discovery.pageSubtitle') }}
          </p>
        </div>
        <div class="d-flex flex-wrap gap-2">
          <v-btn
            variant="outlined"
            color="primary"
            prepend-icon="mdi-cog-outline"
            to="/apps/siem-center/settings"
          >
            {{ t('siemCenter.settings.menuTitle') }}
          </v-btn>
          <v-btn
            variant="outlined"
            color="primary"
            prepend-icon="mdi-shield-search"
            to="/apps/siem-center"
          >
            {{ t('siemCenter.dashboard.menuTitle') }}
          </v-btn>
        </div>
      </div>
    </div>

    <AcSiemDiscoveryCoverageMap />
  </div>
</template>

<style scoped>
.siem-hero {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: linear-gradient(
    135deg,
    rgba(var(--v-theme-primary), 0.08) 0%,
    rgba(var(--v-theme-surface), 1) 55%
  );
}
</style>
