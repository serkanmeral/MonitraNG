<script setup lang="ts">
import { computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AcAlarmRulesExplorer from '@/components/apps/alarm-center/AcAlarmRulesExplorer.vue';
import { useAlarmCenterBreadcrumbs } from '@/composables/useAlarmCenterBreadcrumbs';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const auth = useAuthStore();

const { breadcrumbs } = useAlarmCenterBreadcrumbs({
  tail: computed(() => ({
    text: t('alarmCenter.rules.pageTitle'),
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
  <div class="ac-alarm-flow ac-alarm-rules-page">
    <BaseBreadcrumb
      :title="t('alarmCenter.rules.pageTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <div class="ac-hero mb-4 pa-4 pa-md-5 rounded-lg">
      <h1 class="text-h5 font-weight-bold mb-2">
        {{ t('alarmCenter.rules.pageTitle') }}
      </h1>
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('alarmCenter.rules.pageSubtitle') }}
      </p>
    </div>

    <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
      <AcAlarmRulesExplorer />
    </v-card>
  </div>
</template>
