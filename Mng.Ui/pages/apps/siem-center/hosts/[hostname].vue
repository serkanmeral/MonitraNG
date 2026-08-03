<script setup lang="ts">
import { computed, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AcSiemHostDashboard from '@/components/apps/siem-center/AcSiemHostDashboard.vue';
import { useSiemCenterBreadcrumbs } from '@/composables/useSiemCenterBreadcrumbs';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';
import { shortHostKey } from '@/utils/siemDiscoveryHostMatch';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const auth = useAuthStore();
const route = useRoute();

const hostnameParam = computed(() => {
  const raw = route.params.hostname;
  const s = Array.isArray(raw) ? raw[0] : raw;
  return typeof s === 'string' ? decodeURIComponent(s) : '';
});

const displayHost = computed(
  () => shortHostKey(hostnameParam.value) || hostnameParam.value || '—',
);

const { breadcrumbs } = useSiemCenterBreadcrumbs({
  subPage: true,
  tail: computed(() => ({
    text: t('siemCenter.hostDashboard.pageTitle', { host: displayHost.value }),
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
  <div class="siem-host-dashboard-page">
    <BaseBreadcrumb
      :title="t('siemCenter.hostDashboard.menuTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <div class="siem-hero mb-4 pa-4 pa-md-5 rounded-lg">
      <div class="d-flex flex-wrap align-start justify-space-between gap-3">
        <div>
          <h1 class="text-h5 font-weight-bold mb-2">
            {{ t('siemCenter.hostDashboard.pageTitle', { host: displayHost }) }}
          </h1>
          <p class="text-body-2 text-medium-emphasis mb-0">
            {{ t('siemCenter.hostDashboard.pageSubtitle') }}
          </p>
        </div>
      </div>
    </div>

    <AcSiemHostDashboard v-if="hostnameParam" :hostname="hostnameParam" />
    <v-alert v-else type="warning" variant="tonal">
      {{ t('siemCenter.hostDashboard.missingHost') }}
    </v-alert>
  </div>
</template>
