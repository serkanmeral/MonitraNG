<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useWidgetStore } from '@/stores/apps/widget';
import { PlusIcon, RefreshIcon, EditIcon, TrashIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const widgetStore = useWidgetStore();
const deleteTarget = ref<{ __dataId: string; title: string } | null>(null);
const deleteDialogOpen = ref(false);

const page = computed(() => ({ title: mt('monitoring.widgets.pageTitle', 'Monitoring Widget\'ları') }));
const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('monitoring.pageTitle', 'Monitoring tanımları'), disabled: false, href: '/apps/monitoring' },
  { text: mt('monitoring.widgets.pageTitle', 'Monitoring Widget\'ları'), disabled: true, href: '#' },
]);

// Monitoring kategorisindeki widget'lar (config.monitoring varsa)
const monitoringWidgets = computed(() => {
  return widgetStore.widgets.filter((w) => {
    const cfg = w.config as any;
    return cfg?.monitoring === true || (cfg?.monitoring && typeof cfg.monitoring === 'object');
  });
});

async function refresh() {
  await widgetStore.fetchWidgetCategories();
  await widgetStore.fetchWidgets({ limit: 500 });
}

function openDeleteDialog(w: { __dataId?: string; dataId?: string; title?: string; name?: string }) {
  const wid = w.__dataId ?? w.dataId ?? '';
  if (!wid) return;
  deleteTarget.value = { __dataId: wid, title: w.title ?? w.name ?? wid };
  deleteDialogOpen.value = true;
}

async function confirmDelete() {
  if (!deleteTarget.value) return;
  try {
    await widgetStore.deleteWidget(deleteTarget.value.__dataId);
    deleteDialogOpen.value = false;
    deleteTarget.value = null;
  } catch {
    /* store sets error */
  }
}

function closeDeleteDialog() {
  deleteDialogOpen.value = false;
  deleteTarget.value = null;
}

onMounted(async () => {
  await refresh();
});
</script>

<template>
  <div class="monitoring-widgets-page">
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-container fluid>
      <v-alert v-if="widgetStore.error" type="error" variant="tonal" dismissible class="mb-4" @click:close="widgetStore.clearError">
        {{ widgetStore.error }}
      </v-alert>

      <div class="d-flex align-center gap-2 mb-4">
        <v-btn color="primary" variant="flat" size="small" to="/apps/monitoring/widgets/new">
          <PlusIcon size="18" class="mr-1" /> {{ mt('monitoring.widgets.newWidget', 'Yeni Widget') }}
        </v-btn>
        <v-btn variant="outlined" size="small" :disabled="widgetStore.loading" @click="refresh">
          <RefreshIcon size="18" class="mr-1" /> {{ mt('monitoring.common.refresh', 'Yenile') }}
        </v-btn>
        <v-btn variant="text" size="small" to="/apps/dashboards">
          {{ mt('monitoring.widgets.goToDashboards', 'Dashboard\'lar') }}
        </v-btn>
      </div>

      <v-card variant="outlined">
        <v-card-title class="py-3">{{ mt('monitoring.widgets.listTitle', 'Widget listesi') }}</v-card-title>
        <v-divider />
        <v-card-text>
          <div v-if="widgetStore.loading && monitoringWidgets.length === 0" class="d-flex justify-center py-12">
            <v-progress-circular indeterminate color="primary" />
          </div>
          <v-list v-else-if="monitoringWidgets.length > 0" density="compact">
            <v-list-item
              v-for="w in monitoringWidgets"
              :key="w.__dataId ?? w.dataId"
              :to="`/apps/monitoring/widgets/${w.__dataId ?? w.dataId}/edit`"
              lines="two"
              >
              <template #prepend>
                <v-icon :color="w.type === 'map' ? 'success' : w.type === 'gauge' ? 'warning' : w.type === 'chart' ? 'primary' : 'secondary'">
                  {{ w.type === 'map' ? 'mdi-map-marker' : w.type === 'gauge' ? 'mdi-gauge' : w.type === 'chart' ? 'mdi-chart-line' : 'mdi-card' }}
                </v-icon>
              </template>
              <v-list-item-title>{{ w.title }}</v-list-item-title>
              <v-list-item-subtitle>{{ w.name }} · {{ w.type === 'map' ? 'Harita' : w.type === 'gauge' ? 'Gauge' : w.type }}</v-list-item-subtitle>
              <template #append>
                <div class="d-flex gap-1" @click.stop.prevent>
                  <v-btn
                    icon
                    size="small"
                    variant="text"
                    :to="`/apps/monitoring/widgets/${w.__dataId ?? w.dataId}/edit`"
                    @click.stop
                  >
                    <EditIcon size="18" />
                    <v-tooltip activator="parent" location="top">{{ mt('monitoring.widgets.edit', 'Düzenle') }}</v-tooltip>
                  </v-btn>
                  <v-btn
                    icon
                    size="small"
                    variant="text"
                    color="error"
                    @click.stop.prevent="openDeleteDialog(w)"
                  >
                    <TrashIcon size="18" />
                    <v-tooltip activator="parent" location="top">{{ mt('monitoring.widgets.delete', 'Sil') }}</v-tooltip>
                  </v-btn>
                </div>
              </template>
            </v-list-item>
          </v-list>
          <div v-else class="text-center py-12 text-medium-emphasis">
            <v-icon size="48" class="mb-2 d-block mx-auto opacity-50">mdi-widgets-outline</v-icon>
            {{ mt('monitoring.widgets.noWidgets', 'Henüz monitoring widget\'ı yok. Yeni widget oluşturun.') }}
          </div>
        </v-card-text>
      </v-card>

      <!-- Delete confirmation dialog -->
      <v-dialog v-model="deleteDialogOpen" max-width="420" persistent>
        <v-card>
          <v-card-title class="text-h6">
            {{ mt('monitoring.widgets.deleteTitle', 'Widget Sil') }}
          </v-card-title>
          <v-card-text>
            {{ mt('monitoring.widgets.deleteConfirm', 'Bu widget\'ı silmek istediğinize emin misiniz?') }}
            <span v-if="deleteTarget" class="font-weight-medium d-block mt-2">"{{ deleteTarget.title }}"</span>
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="closeDeleteDialog">
              {{ mt('monitoring.common.cancel', 'İptal') }}
            </v-btn>
            <v-btn color="error" variant="flat" :loading="widgetStore.loading" @click="confirmDelete">
              {{ mt('monitoring.common.delete', 'Sil') }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>
    </v-container>
  </div>
</template>
