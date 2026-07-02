<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OdakSiparisHubListSettingsTab from '@/components/apps/odak-siparis/OdakSiparisHubListSettingsTab.vue';
import OdakSiparisPackageSettingsNotificationsTab from '@/components/apps/odak-siparis/OdakSiparisPackageSettingsNotificationsTab.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useOdakSiparisHubSettingsStore } from '@/stores/apps/odakSiparisHubSettings';
import { ensureAuthUserReady, userHasManagerRole } from '@/utils/authRoles';
import {
  ODAK_GLOBAL_SHIPMENT_DEFAULT_MAIL_TEMPLATE,
  ODAK_GLOBAL_SHIPMENT_NOTIFICATION_EVENT,
  ODAK_GLOBAL_SHIPMENT_NOTIFICATION_EVENT_TYPES,
} from '@/utils/odakSiparisNotificationPolicies';
import { odakGlobalShipmentSettingsFieldLabelTr } from '@/utils/odakSiparisSettingsLabels';
import { useAuthStore } from '@/stores/auth';

definePageMeta({
  layout: 'default',
  middleware: ['odak-siparis-shipments-settings'],
});

const { t } = useAppI18n();
const auth = useAuthStore();
const panelError = usePanelErrorNotify('errors.dg.generic');
const hubStore = useOdakSiparisHubSettingsStore();
const scopeStatus = computed(() => hubStore.scopes.global_shipments_list.status);

const activeTab = ref('listColumns');
const loadError = ref('');

const page = computed(() => ({ title: t('odakSiparis.globalShipments.settings.title') }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('odakSiparis.globalShipments.title'), disabled: false, href: '/apps/odak-siparis/shipments' },
  { text: t('odakSiparis.globalShipments.settings.title'), disabled: true, href: '#' },
]);

const tabs = computed(() => [
  { value: 'listColumns', label: t('odakSiparis.globalShipments.settings.tabs.listColumns') },
  { value: 'notifications', label: t('odakSiparis.globalShipments.settings.tabs.notifications') },
]);

const isLoading = computed(() => scopeStatus.value === 'loading');

onMounted(async () => {
  loadError.value = '';
  await ensureAuthUserReady(auth);
  if (!userHasManagerRole(auth.userInfo)) {
    await navigateTo('/apps/odak-siparis/shipments');
    return;
  }
  try {
    await hubStore.ensureScopeReady('global_shipments_list', true);
  } catch (e: unknown) {
    loadError.value = panelError(e, 'errors.dg.generic');
  }
});

onBeforeRouteLeave((_to, _from, next) => {
  if (!hubStore.isScopeDirty('global_shipments_list')) {
    next();
    return;
  }
  const ok = window.confirm(
    'Kaydedilmemis hub ayar degisiklikleri var. Sayfadan ayrilmak istediginize emin misiniz?'
  );
  next(ok);
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-card elevation="10">
      <v-card-title class="d-flex flex-wrap align-center ga-3 py-4">
        <span class="text-h6">{{ t('odakSiparis.globalShipments.settings.title') }}</span>
        <v-spacer />
        <v-btn variant="text" prepend-icon="mdi-arrow-left" to="/apps/odak-siparis/shipments">
          {{ t('odakSiparis.globalShipments.settings.backToList') }}
        </v-btn>
      </v-card-title>

      <v-tabs v-model="activeTab" color="primary" class="px-4">
        <v-tab v-for="tab in tabs" :key="tab.value" :value="tab.value">{{ tab.label }}</v-tab>
      </v-tabs>

      <v-card-text class="pa-4 pa-md-6">
        <v-alert
          v-if="loadError"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
        >
          {{ loadError }}
        </v-alert>
        <v-progress-linear v-if="isLoading && activeTab === 'listColumns'" indeterminate color="primary" class="mb-4" />

        <v-window v-model="activeTab" eager>
          <v-window-item value="listColumns">
            <OdakSiparisHubListSettingsTab
              scope="global_shipments_list"
              hint-key="odakSiparis.globalShipments.settings.listColumns.hint"
              :field-label="odakGlobalShipmentSettingsFieldLabelTr"
            />
          </v-window-item>
          <v-window-item value="notifications">
            <OdakSiparisPackageSettingsNotificationsTab
              :event-types="ODAK_GLOBAL_SHIPMENT_NOTIFICATION_EVENT_TYPES"
              :default-event-type="ODAK_GLOBAL_SHIPMENT_NOTIFICATION_EVENT"
              :default-template-key="ODAK_GLOBAL_SHIPMENT_DEFAULT_MAIL_TEMPLATE"
              hint-key="odakSiparis.globalShipments.settings.notifications.hint"
              i18n-prefix="odakSiparis.globalShipments.settings.notifications"
            />
          </v-window-item>
        </v-window>
      </v-card-text>
    </v-card>
  </div>
</template>
