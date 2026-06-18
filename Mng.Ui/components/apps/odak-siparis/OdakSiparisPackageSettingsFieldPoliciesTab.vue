<script setup lang="ts">
import { ref } from 'vue';
import OdakSiparisHubFieldPoliciesTab from '@/components/apps/odak-siparis/OdakSiparisHubFieldPoliciesTab.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ODAK_LINE_POLICY_FIELD_KEYS,
  ODAK_PACKAGE_POLICY_FIELD_KEYS,
  ODAK_SHIPMENT_POLICY_FIELD_KEYS,
} from '@/utils/odakSiparisFieldPolicies';
import {
  loadOdakHubFieldPoliciesBlob,
  saveOdakHubFieldPoliciesBlob,
} from '@/utils/odakSiparisHubSettingsService';
import {
  odakLineSettingsFieldLabelTr,
  odakPackageSettingsFieldLabelTr,
  odakShipmentSettingsFieldLabelTr,
} from '@/utils/odakSiparisSettingsLabels';

const { t } = useAppI18n();
const activeFieldTab = ref<'field_policies' | 'lines_field_policies' | 'shipments_field_policies'>('field_policies');

const fieldTabs = [
  { value: 'field_policies' as const, label: t('odakSiparis.packages.settings.fieldTabs.packages') },
  { value: 'lines_field_policies' as const, label: t('odakSiparis.packages.settings.fieldTabs.lines') },
  { value: 'shipments_field_policies' as const, label: t('odakSiparis.packages.settings.fieldTabs.shipments') },
];

const packageStatusItems = [
  { value: 'open', title: t('odakSiparis.packages.tabs.open') },
  { value: 'closed', title: t('odakSiparis.packages.tabs.closed') },
];

const lineBooleanItems = [
  { value: 'true', title: 'Evet' },
  { value: 'false', title: 'Hayır' },
];

const shipmentStatusItems = [
  { value: 'Planlandi', title: 'Planlandı' },
  { value: 'Tamamlandi', title: 'Tamamlandı' },
  { value: 'Iptal', title: 'İptal' },
];

const qcfStatusItems = [
  { value: 'Yok', title: 'Yok' },
  { value: 'Bekliyor', title: 'Bekliyor' },
  { value: 'Tamamlandi', title: 'Tamamlandı' },
];
</script>

<template>
  <div>
    <v-tabs v-model="activeFieldTab" color="primary" density="compact" class="mb-4">
      <v-tab v-for="tab in fieldTabs" :key="tab.value" :value="tab.value">{{ tab.label }}</v-tab>
    </v-tabs>

    <v-window v-model="activeFieldTab">
      <v-window-item value="field_policies">
        <OdakSiparisHubFieldPoliciesTab
          scope="field_policies"
          hint-key="odakSiparis.packages.settings.fieldPolicies.hint"
          :field-keys="[...ODAK_PACKAGE_POLICY_FIELD_KEYS]"
          :field-label="odakPackageSettingsFieldLabelTr"
          :condition-field-keys="['status', 'partCount', 'stockCount', 'beginDate', 'deliveryDate', 'poVersion']"
          default-condition-field="status"
          :enum-field-options="{ status: packageStatusItems }"
          :load-policies="() => loadOdakHubFieldPoliciesBlob('field_policies')"
          :save-policies="(blob, rowId) => saveOdakHubFieldPoliciesBlob('field_policies', blob, rowId)"
        />
      </v-window-item>
      <v-window-item value="lines_field_policies">
        <OdakSiparisHubFieldPoliciesTab
          scope="lines_field_policies"
          hint-key="odakSiparis.packages.settings.fieldTabs.linesHint"
          :field-keys="[...ODAK_LINE_POLICY_FIELD_KEYS]"
          :field-label="odakLineSettingsFieldLabelTr"
          :condition-field-keys="['isFai', 'isFaiComplete', 'quantity', 'shippedQuantity', 'lineNo']"
          default-condition-field="isFai"
          :enum-field-options="{
            isFai: lineBooleanItems,
            isFaiComplete: lineBooleanItems,
          }"
          :load-policies="() => loadOdakHubFieldPoliciesBlob('lines_field_policies')"
          :save-policies="(blob, rowId) => saveOdakHubFieldPoliciesBlob('lines_field_policies', blob, rowId)"
        />
      </v-window-item>
      <v-window-item value="shipments_field_policies">
        <OdakSiparisHubFieldPoliciesTab
          scope="shipments_field_policies"
          hint-key="odakSiparis.packages.settings.fieldTabs.shipmentsHint"
          :field-keys="[...ODAK_SHIPMENT_POLICY_FIELD_KEYS]"
          :field-label="odakShipmentSettingsFieldLabelTr"
          :condition-field-keys="['status', 'qcfStatus']"
          default-condition-field="status"
          :enum-field-options="{
            status: shipmentStatusItems,
            qcfStatus: qcfStatusItems,
          }"
          :load-policies="() => loadOdakHubFieldPoliciesBlob('shipments_field_policies')"
          :save-policies="(blob, rowId) => saveOdakHubFieldPoliciesBlob('shipments_field_policies', blob, rowId)"
        />
      </v-window-item>
    </v-window>
  </div>
</template>
