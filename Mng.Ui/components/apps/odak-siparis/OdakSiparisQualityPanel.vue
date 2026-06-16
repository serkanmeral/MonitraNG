<script setup lang="ts">
import { ref } from 'vue';
import OdakSiparisCapaPanel from '@/components/apps/odak-siparis/OdakSiparisCapaPanel.vue';
import OdakSiparisNcrPanel from '@/components/apps/odak-siparis/OdakSiparisNcrPanel.vue';
import { useAppI18n } from '@/composables/useAppI18n';

const props = defineProps<{
  packageId: string;
  packageNo?: string;
}>();

const { t } = useAppI18n();

type QualityTab = 'ncr' | 'capa';
const activeTab = ref<QualityTab>('ncr');
</script>

<template>
  <div class="odak-quality-panel">
    <v-tabs v-model="activeTab" color="primary" density="compact" class="mb-3">
      <v-tab value="ncr">{{ t('odakSiparis.quality.tabs.ncr') }}</v-tab>
      <v-tab value="capa">{{ t('odakSiparis.quality.tabs.capa') }}</v-tab>
    </v-tabs>

    <div v-if="activeTab === 'ncr'">
      <OdakSiparisNcrPanel :key="`${packageId}-ncr`" :package-id="packageId" :package-no="packageNo" />
    </div>
    <div v-if="activeTab === 'capa'">
      <OdakSiparisCapaPanel :key="`${packageId}-capa`" :package-id="packageId" :package-no="packageNo" />
    </div>
  </div>
</template>
