<script setup lang="ts">
import { computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcApprovalsExplorer from '@/components/apps/operation-core/OcApprovalsExplorer.vue';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const auth = useAuthStore();

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  tail: computed(() => ({
    text: t('operationCore.adminApprovals.pageTitle'),
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
  <div class="oc-flow oc-approvals-page">
    <BaseBreadcrumb
      :title="t('operationCore.adminApprovals.pageTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <div class="oc-hero mb-4 pa-4 pa-md-5 rounded-lg">
      <h1 class="text-h5 font-weight-bold mb-2">
        {{ t('operationCore.adminApprovals.pageTitle') }}
      </h1>
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('operationCore.adminApprovals.pageSubtitle') }}
      </p>
    </div>

    <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
      <OcApprovalsExplorer />
    </v-card>
  </div>
</template>
