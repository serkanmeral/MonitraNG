<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakSiparisLinesPanel from '@/components/apps/odak-siparis/OdakSiparisLinesPanel.vue';
import OdakSiparisPoDocumentPanel from '@/components/apps/odak-siparis/OdakSiparisPoDocumentPanel.vue';
import OdakSiparisQualityPanel from '@/components/apps/odak-siparis/OdakSiparisQualityPanel.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakPackageRow } from '@/utils/odakSiparisConfig';
import {
  fetchOdakPackageById,
  packageDataId,
  packageDisplayNo,
} from '@/utils/odakSiparisService';
import { buildOdakPackageSummaryRows } from '@/utils/odakSiparisSummary';

const props = defineProps<{
  packageRow: OdakPackageRow;
  customerLabels: Record<string, string>;
  /** Liste sayfasindan expand acilirken baslangic sekmesi (URL ile senkron). */
  initialTab?: 'summary' | 'lines' | 'quality';
}>();

const emit = defineEmits<{
  'open-customer': [customerId: string];
  'update:activeTab': ['summary' | 'lines' | 'quality'];
}>();

const { t } = useAppI18n();
const route = useRoute();

type ExpandTab = 'summary' | 'lines' | 'quality';

function tabFromRouteQuery(): ExpandTab {
  const tabFromQuery = route.query.tab;
  if (tabFromQuery === 'lines') return 'lines';
  if (tabFromQuery === 'quality') return 'quality';
  return 'summary';
}

const activeTab = ref<ExpandTab>(props.initialTab ?? tabFromRouteQuery());
const loading = ref(false);
const errorMessage = ref('');
const pkg = ref<OdakPackageRow | null>(null);

const packageId = computed(() => packageDataId(props.packageRow));

const summaryRows = computed(() => {
  const p = pkg.value ?? props.packageRow;
  return buildOdakPackageSummaryRows(p, props.customerLabels, t);
});

async function loadPackage() {
  const id = packageId.value;
  if (!id) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    pkg.value = (await fetchOdakPackageById(id)) ?? props.packageRow;
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    pkg.value = props.packageRow;
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadPackage();
});

watch(
  () => packageId.value,
  () => {
    void loadPackage();
  }
);

watch(
  () => props.initialTab,
  (tab) => {
    if (tab) activeTab.value = tab;
  }
);

watch(activeTab, (tab) => {
  emit('update:activeTab', tab);
});
</script>

<template>
  <div class="odak-package-expand-panel pa-4">
    <div class="text-subtitle-2 font-weight-medium mb-3">
      {{ packageDisplayNo(pkg ?? packageRow) }}
    </div>

    <v-alert v-if="errorMessage" type="warning" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

    <v-tabs v-model="activeTab" color="primary" density="compact" class="mb-3">
      <v-tab value="summary">{{ t('odakSiparis.detail.tabs.summary') }}</v-tab>
      <v-tab value="lines">{{ t('odakSiparis.detail.tabs.lines') }}</v-tab>
      <v-tab value="quality">{{ t('odakSiparis.detail.tabs.quality') }}</v-tab>
    </v-tabs>

    <div v-show="activeTab === 'summary'" class="odak-summary-split">
      <v-row dense align="stretch">
        <v-col cols="12" md="7" class="odak-summary-split__info">
          <v-table density="compact" class="border rounded-md bg-surface h-100">
            <tbody>
              <tr v-for="row in summaryRows" :key="row.label">
                <td class="font-weight-medium text-medium-emphasis" width="200">{{ row.label }}</td>
                <td>
                  <a
                    v-if="row.customerId"
                    href="#"
                    class="text-primary text-decoration-none"
                    @click.prevent="emit('open-customer', row.customerId!)"
                  >
                    {{ row.value }}
                  </a>
                  <NuxtLink
                    v-else-if="row.link"
                    :to="row.link"
                    class="text-primary text-decoration-none"
                  >
                    {{ row.value }}
                  </NuxtLink>
                  <span v-else class="text-body-2">{{ row.value }}</span>
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-col>
        <v-col cols="12" md="5" class="odak-summary-split__po">
          <OdakSiparisPoDocumentPanel
            :key="`${packageId}-po`"
            :package-id="packageId"
            :package-no="(pkg ?? packageRow).packageNo"
            @saved="loadPackage"
          />
        </v-col>
      </v-row>
    </div>

    <div v-if="activeTab === 'lines'">
      <OdakSiparisLinesPanel
        :key="packageId"
        :package-id="packageId"
        :package-no="(pkg ?? packageRow).packageNo"
        compact
      />
    </div>

    <div v-if="activeTab === 'quality'">
      <OdakSiparisQualityPanel
        :key="packageId"
        :package-id="packageId"
        :package-no="(pkg ?? packageRow).packageNo"
      />
    </div>
  </div>
</template>

<style scoped>
.odak-package-expand-panel {
  background: rgba(var(--v-theme-surface-variant), 0.25);
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.odak-summary-split__po {
  min-height: 0;
}

@media (min-width: 960px) {
  .odak-summary-split__info,
  .odak-summary-split__po {
    align-self: stretch;
  }
}
</style>
