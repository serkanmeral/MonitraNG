<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OcMailTemplatesExplorer from '@/components/apps/notifier/OcMailTemplatesExplorer.vue';
import OcMessageTemplatesExplorer from '@/components/apps/notifier/OcMessageTemplatesExplorer.vue';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const auth = useAuthStore();
const tab = ref('email');

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  tail: computed(() => ({
    text: t('notifier.notificationTemplates.menuTitle'),
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
  <div class="oc-flow oc-notification-templates-page">
    <BaseBreadcrumb :title="t('notifier.notificationTemplates.menuTitle')" :breadcrumbs="breadcrumbs" />

    <div class="oc-hero mb-4 pa-4 pa-md-5 rounded-lg">
      <h1 class="text-h5 font-weight-bold mb-2">{{ t('notifier.notificationTemplates.menuTitle') }}</h1>
      <p class="text-body-2 text-medium-emphasis mb-0">{{ t('notifier.notificationTemplates.pageSubtitle') }}</p>
    </div>

    <v-card variant="outlined" class="rounded-lg">
      <v-tabs v-model="tab" color="primary" class="px-2 pt-1">
        <v-tab value="email">{{ t('notifier.notificationTemplates.tabEmail') }}</v-tab>
        <v-tab value="message">{{ t('notifier.notificationTemplates.tabMessage') }}</v-tab>
      </v-tabs>
      <v-divider />
      <v-tabs-window v-model="tab">
        <v-tabs-window-item value="email">
          <OcMailTemplatesExplorer />
        </v-tabs-window-item>
        <v-tabs-window-item value="message">
          <OcMessageTemplatesExplorer />
        </v-tabs-window-item>
      </v-tabs-window>
    </v-card>
  </div>
</template>
