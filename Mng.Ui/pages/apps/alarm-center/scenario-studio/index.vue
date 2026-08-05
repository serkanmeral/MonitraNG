<script setup lang="ts">
import { computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AcAlarmCenterSectionNav from '@/components/apps/alarm-center/AcAlarmCenterSectionNav.vue';
import AcScenarioStudio from '@/components/apps/alarm-center/AcScenarioStudio.vue';
import { useAlarmCenterBreadcrumbs } from '@/composables/useAlarmCenterBreadcrumbs';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const auth = useAuthStore();
const { breadcrumbs } = useAlarmCenterBreadcrumbs({
  tail: computed(() => ({
    text: t('alarmCenter.scenarioStudio.pageTitle'),
    disabled: true,
  })),
});

onMounted(() => {
  if (!auth.isManager) void navigateTo('/unauthorized');
});
</script>

<template>
  <div class="ac-alarm-flow">
    <BaseBreadcrumb :title="t('alarmCenter.scenarioStudio.pageTitle')" :breadcrumbs="breadcrumbs" />
    <div class="ac-hero mb-4 pa-4 pa-md-5 rounded-lg">
      <h1 class="text-h5 font-weight-bold mb-2">{{ t('alarmCenter.scenarioStudio.pageTitle') }}</h1>
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('alarmCenter.scenarioStudio.pageSubtitle') }}
      </p>
    </div>
    <AcAlarmCenterSectionNav />
    <AcScenarioStudio />
  </div>
</template>
