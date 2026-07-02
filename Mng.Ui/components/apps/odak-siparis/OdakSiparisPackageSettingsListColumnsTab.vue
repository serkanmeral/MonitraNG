<script setup lang="ts">
import { ref } from 'vue';
import OdakSiparisHubListSettingsTab from '@/components/apps/odak-siparis/OdakSiparisHubListSettingsTab.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  odakLineSettingsFieldLabelTr,
  odakPackageSettingsFieldLabelTr,
  odakShipmentSettingsFieldLabelTr,
} from '@/utils/odakSiparisSettingsLabels';

const { t } = useAppI18n();
const activeListTab = ref<'packages_list' | 'lines_list' | 'shipments_list'>('packages_list');

const listTabs = [
  { value: 'packages_list' as const, label: t('odakSiparis.packages.settings.listTabs.packages') },
  { value: 'lines_list' as const, label: t('odakSiparis.packages.settings.listTabs.lines') },
  { value: 'shipments_list' as const, label: t('odakSiparis.packages.settings.listTabs.shipments') },
];
</script>

<template>
  <div>
    <v-tabs v-model="activeListTab" color="primary" density="compact" class="mb-4">
      <v-tab v-for="tab in listTabs" :key="tab.value" :value="tab.value">{{ tab.label }}</v-tab>
    </v-tabs>

    <v-window v-model="activeListTab" eager>
      <v-window-item value="packages_list">
        <OdakSiparisHubListSettingsTab
          scope="packages_list"
          hint-key="odakSiparis.packages.settings.listColumns.hint"
          :field-label="odakPackageSettingsFieldLabelTr"
        />
      </v-window-item>
      <v-window-item value="lines_list">
        <OdakSiparisHubListSettingsTab
          scope="lines_list"
          hint-key="odakSiparis.packages.settings.listTabs.linesHint"
          :field-label="odakLineSettingsFieldLabelTr"
        />
      </v-window-item>
      <v-window-item value="shipments_list">
        <OdakSiparisHubListSettingsTab
          scope="shipments_list"
          hint-key="odakSiparis.packages.settings.listTabs.shipmentsHint"
          :field-label="odakShipmentSettingsFieldLabelTr"
        />
      </v-window-item>
    </v-window>
  </div>
</template>
