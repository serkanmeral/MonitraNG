<script setup lang="ts">
import { ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OdakSiparisPackageSettingsListColumnsTab from '@/components/apps/odak-siparis/OdakSiparisPackageSettingsListColumnsTab.vue';
import OdakSiparisPackageSettingsFieldPoliciesTab from '@/components/apps/odak-siparis/OdakSiparisPackageSettingsFieldPoliciesTab.vue';
import OdakSiparisPackageSettingsNotificationsTab from '@/components/apps/odak-siparis/OdakSiparisPackageSettingsNotificationsTab.vue';
import OdakSiparisPackageSettingsPoDocumentAccessTab from '@/components/apps/odak-siparis/OdakSiparisPackageSettingsPoDocumentAccessTab.vue';
import { useAppI18n } from '@/composables/useAppI18n';

definePageMeta({
  layout: 'default',
  middleware: ['odak-siparis-packages-settings'],
});

const { t } = useAppI18n();
const activeTab = ref('listColumns');

const page = { title: t('odakSiparis.packages.settings.title') };
const breadcrumbs = [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('odakSiparis.packages.title'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('odakSiparis.packages.settings.title'), disabled: true, href: '#' },
];

const tabs = [
  { value: 'listColumns', label: t('odakSiparis.packages.settings.tabs.listColumns') },
  { value: 'notifications', label: t('odakSiparis.packages.settings.tabs.notifications') },
  { value: 'poDocumentAccess', label: t('odakSiparis.packages.settings.tabs.poDocumentAccess') },
  { value: 'fieldPolicies', label: t('odakSiparis.packages.settings.tabs.fieldPolicies') },
];
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
        <v-window v-model="activeTab">
          <v-window-item value="listColumns">
            <OdakSiparisPackageSettingsListColumnsTab />
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
