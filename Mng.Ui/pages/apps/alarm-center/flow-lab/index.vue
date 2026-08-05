<script setup lang="ts">
import { computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AcFlowLab from '@/components/apps/alarm-center/AcFlowLab.vue';
import { useAlarmCenterBreadcrumbs } from '@/composables/useAlarmCenterBreadcrumbs';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const auth = useAuthStore();
const { breadcrumbs } = useAlarmCenterBreadcrumbs({
  tail: computed(() => ({
    text: t('alarmCenter.flowLab.pageTitle'),
    disabled: true,
  })),
});

onMounted(() => {
  if (!auth.isManager) void navigateTo('/unauthorized');
});
</script>

<template>
  <div class="ac-flow-lab">
    <BaseBreadcrumb :title="t('alarmCenter.flowLab.pageTitle')" :breadcrumbs="breadcrumbs" />
    <AcFlowLab />
  </div>
</template>
