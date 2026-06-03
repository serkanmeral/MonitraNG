<script setup lang="ts">
import { computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcAdminAlarmsExplorer from '@/components/apps/operation-core/admin/OcAdminAlarmsExplorer.vue';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const auth = useAuthStore();

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  tail: computed(() => ({
    text: t('operationCore.adminAlarms.pageTitle'),
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
  <div class="oc-flow oc-admin-alarms-page">
    <BaseBreadcrumb
      :title="t('operationCore.adminAlarms.pageTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <div class="oc-hero mb-4 pa-4 pa-md-5 rounded-lg">
      <h1 class="text-h5 font-weight-bold mb-2">
        {{ t('operationCore.adminAlarms.pageTitle') }}
      </h1>
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('operationCore.adminAlarms.pageSubtitle') }}
      </p>
    </div>

    <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
      <OcAdminAlarmsExplorer />
    </v-card>
  </div>
</template>
