<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { storeToRefs } from 'pinia';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OdakSiparisPackageSettingsListColumnsTab from '@/components/apps/odak-siparis/OdakSiparisPackageSettingsListColumnsTab.vue';
import OdakSiparisPackageSettingsFieldPoliciesTab from '@/components/apps/odak-siparis/OdakSiparisPackageSettingsFieldPoliciesTab.vue';
import OdakSiparisPackageSettingsNotificationsTab from '@/components/apps/odak-siparis/OdakSiparisPackageSettingsNotificationsTab.vue';
import OdakSiparisPackageSettingsPoDocumentAccessTab from '@/components/apps/odak-siparis/OdakSiparisPackageSettingsPoDocumentAccessTab.vue';
import OdakSiparisPackageSettingsPersonnelTab from '@/components/apps/odak-siparis/OdakSiparisPackageSettingsPersonnelTab.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  ODAK_HUB_SETTINGS_SCOPES,
  useOdakSiparisHubSettingsStore,
} from '@/stores/apps/odakSiparisHubSettings';

definePageMeta({
  layout: 'default',
  middleware: ['odak-siparis-packages-settings'],
});

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const hubStore = useOdakSiparisHubSettingsStore();
const { bootstrapStatus, bootstrapError } = storeToRefs(hubStore);

const activeTab = ref('listColumns');
const loadError = ref('');

const page = { title: t('odakSiparis.packages.settings.title') };
const breadcrumbs = [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('odakSiparis.packages.title'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('odakSiparis.packages.settings.title'), disabled: true, href: '#' },
];

const tabs = [
  { value: 'listColumns', label: t('odakSiparis.packages.settings.tabs.listColumns') },
  { value: 'personnel', label: t('odakSiparis.packages.settings.tabs.personnel') },
  { value: 'notifications', label: t('odakSiparis.packages.settings.tabs.notifications') },
  { value: 'poDocumentAccess', label: t('odakSiparis.packages.settings.tabs.poDocumentAccess') },
  { value: 'fieldPolicies', label: t('odakSiparis.packages.settings.tabs.fieldPolicies') },
];

const isLoading = computed(() => bootstrapStatus.value === 'loading');

onMounted(async () => {
  loadError.value = '';
  try {
    await hubStore.bootstrap(true);
  } catch (e: unknown) {
    loadError.value = panelError(e, 'errors.dg.generic');
  }
});

onBeforeRouteLeave((_to, _from, next) => {
  const dirtyScopes = ODAK_HUB_SETTINGS_SCOPES.filter((scope) => hubStore.isScopeDirty(scope));
  if (!dirtyScopes.length) {
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
        <span class="text-h6">{{ t('odakSiparis.packages.settings.title') }}</span>
        <v-spacer />
        <v-btn variant="text" prepend-icon="mdi-arrow-left" to="/apps/odak-siparis/packages">
          {{ t('odakSiparis.packages.settings.backToList') }}
        </v-btn>
      </v-card-title>

      <v-tabs v-model="activeTab" color="primary" class="px-4">
        <v-tab v-for="tab in tabs" :key="tab.value" :value="tab.value">{{ tab.label }}</v-tab>
      </v-tabs>

      <v-card-text class="pa-4 pa-md-6">
        <v-alert
          v-if="loadError || bootstrapError"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
        >
          {{ loadError || bootstrapError }}
        </v-alert>
        <v-progress-linear v-if="isLoading" indeterminate color="primary" class="mb-4" />

        <v-window v-model="activeTab" eager>
          <v-window-item value="listColumns">
            <OdakSiparisPackageSettingsListColumnsTab />
          </v-window-item>
          <v-window-item value="personnel">
            <OdakSiparisPackageSettingsPersonnelTab />
          </v-window-item>
          <v-window-item value="notifications">
            <OdakSiparisPackageSettingsNotificationsTab />
          </v-window-item>
          <v-window-item value="poDocumentAccess">
            <OdakSiparisPackageSettingsPoDocumentAccessTab />
          </v-window-item>
          <v-window-item value="fieldPolicies">
            <OdakSiparisPackageSettingsFieldPoliciesTab />
          </v-window-item>
        </v-window>
      </v-card-text>
    </v-card>
  </div>
</template>
