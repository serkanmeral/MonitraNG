<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, provide } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DashboardLayoutRenderer from '@/components/dashboards/DashboardLayoutRenderer.vue';
import { useDashboardStore } from '@/stores/apps/dashboard';
import { useDashboardPermissions } from '@/composables/useDashboardPermissions';
import type { Dashboard, DashboardLayout } from '@/stores/apps/dashboard';

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string) => {
  if (i18n?.t) return i18n.t(key);
  if (i18n?.global?.t) return i18n.global.t(key);
  return key;
};

const dashboardStore = useDashboardStore();
const { canViewDashboard } = useDashboardPermissions();

// Development mode check
const isDev = import.meta.dev || import.meta.env?.DEV || process.env.NODE_ENV !== 'production';

const page = computed(() => ({
  title: t('dashboards.container.title') || 'Dashboard Container',
}));

const breadcrumbs = computed(() => [
  { text: t('dashboards.breadcrumbs.home') || 'Dashboard', disabled: false, href: '/dashboards/analytical' },
  { text: t('dashboards.container.title') || 'Dashboard Container', disabled: true, href: '#' },
]);

// Settings
const selectedDashboardIds = ref<string[]>([]);
const refreshTimeUnit = ref<'seconds' | 'minutes'>('seconds');
const refreshTimeValue = ref<number>(10);
const isPlaying = ref(false);
const showSettingsModal = ref(false);

// Widget refresh settings (separate from dashboard rotation)
const widgetRefreshTimeUnit = ref<'seconds' | 'minutes'>('seconds');
const widgetRefreshTimeValue = ref<number>(30);

// Loaded dashboards
const loadedDashboards = ref<Dashboard[]>([]);
const currentDashboardIndex = ref<number>(0);
const currentDashboard = computed<Dashboard | null>(() => {
  if (loadedDashboards.value.length === 0) return null;
  return loadedDashboards.value[currentDashboardIndex.value] || null;
});

const layout = computed<DashboardLayout | null>(() => {
  const d = currentDashboard.value;
  if (!d?.layout?.rows?.length) return null;
  return d.layout;
});

// Calculate refresh interval in milliseconds (for dashboard rotation)
const refreshIntervalMs = computed(() => {
  if (!refreshTimeValue.value || refreshTimeValue.value <= 0) {
    if (isDev) {
      console.log('[Container] refreshIntervalMs is null - refreshTimeValue:', refreshTimeValue.value);
    }
    return null;
  }
  const interval = refreshTimeUnit.value === 'seconds' 
    ? refreshTimeValue.value * 1000 
    : refreshTimeValue.value * 60 * 1000;
  return interval;
});

// Calculate widget refresh interval in milliseconds
const widgetRefreshIntervalMs = computed(() => {
  if (!widgetRefreshTimeValue.value || widgetRefreshTimeValue.value <= 0) return null;
  if (widgetRefreshTimeUnit.value === 'seconds') {
    return widgetRefreshTimeValue.value * 1000;
  } else {
    return widgetRefreshTimeValue.value * 60 * 1000;
  }
});

// Provide refresh interval to child components (widgets can refresh their data)
provide('dashboardRefreshInterval', widgetRefreshIntervalMs);

// Timer
let rotationTimer: ReturnType<typeof setInterval> | null = null;

// Load available dashboards (only those user has permission to view)
const availableDashboards = computed(() => {
  return dashboardStore.activeDashboards
    .filter((d) => canViewDashboard(d.permissions))
    .map((d) => ({
      title: d.title || d.name,
      value: d.__dataId || d.dataId || '',
      subtitle: d.description || '',
    }));
});

// Load selected dashboards
async function loadSelectedDashboards() {
  if (isDev) {
    console.log('[Container] loadSelectedDashboards called', {
      selectedDashboardIds: selectedDashboardIds.value,
    });
  }
  
  if (selectedDashboardIds.value.length === 0) {
    loadedDashboards.value = [];
    currentDashboardIndex.value = 0;
    if (isDev) {
      console.log('[Container] No dashboards selected');
    }
    return;
  }

  loadedDashboards.value = [];
  let loadedCount = 0;
  let deniedCount = 0;
  let errorCount = 0;
  
  for (const id of selectedDashboardIds.value) {
    try {
      const dashboard = await dashboardStore.fetchDashboardById(id);
      // Check permission
      if (canViewDashboard(dashboard.permissions)) {
        loadedDashboards.value.push(dashboard);
        loadedCount++;
        if (isDev) {
          console.log('[Container] Dashboard loaded:', dashboard.title || dashboard.name);
        }
      } else {
        deniedCount++;
        if (isDev) {
          console.warn('[Container] Dashboard permission denied:', dashboard.title || dashboard.name);
        }
      }
    } catch (e) {
      errorCount++;
      console.error(`[Container] Failed to load dashboard ${id}:`, e);
    }
  }

  // Reset to first dashboard
  if (loadedDashboards.value.length > 0) {
    currentDashboardIndex.value = 0;
    if (isDev) {
      console.log('[Container] Loaded dashboards count:', loadedDashboards.value.length, `(Selected: ${selectedDashboardIds.value.length}, Loaded: ${loadedCount}, Denied: ${deniedCount}, Errors: ${errorCount})`);
    }
  } else {
    currentDashboardIndex.value = 0;
    console.warn('[Container] No dashboards loaded', {
      selected: selectedDashboardIds.value.length,
      denied: deniedCount,
      errors: errorCount,
    });
  }
}

// Start rotation
function startRotation() {
  console.log('[Container] startRotation called', {
    loadedDashboardsCount: loadedDashboards.value.length,
    refreshIntervalMs: refreshIntervalMs.value,
    rotationTimer: rotationTimer,
  });
  
  if (loadedDashboards.value.length <= 1) {
    console.warn('[Container] Cannot start rotation: need at least 2 dashboards. Currently loaded:', loadedDashboards.value.length);
    return;
  }
  if (rotationTimer) {
    console.warn('[Container] Rotation already running');
    return; // Already running
  }
  if (!refreshIntervalMs.value) {
    console.warn('[Container] Cannot start rotation: refresh interval is not set');
    if (isDev) {
      alert('Rotasyon başlatılamadı: Rotasyon süresi belirtilmelidir.');
    }
    return;
  }

  const interval = refreshIntervalMs.value;
  console.log('[Container] Starting rotation with interval:', interval, 'ms');
  
  isPlaying.value = true;
  rotationTimer = setInterval(() => {
    currentDashboardIndex.value = (currentDashboardIndex.value + 1) % loadedDashboards.value.length;
    if (isDev) {
      console.log('[Container] Rotated to dashboard:', currentDashboardIndex.value + 1, '/', loadedDashboards.value.length);
    }
  }, interval);
  
  console.log('[Container] Rotation started successfully');
}

// Stop rotation
function stopRotation() {
  if (rotationTimer) {
    clearInterval(rotationTimer);
    rotationTimer = null;
  }
  isPlaying.value = false;
}

// Toggle play/pause
function togglePlayPause() {
  console.log('[Container] togglePlayPause called', {
    isPlaying: isPlaying.value,
    loadedDashboardsCount: loadedDashboards.value.length,
    refreshIntervalMs: refreshIntervalMs.value,
  });
  
  if (isPlaying.value) {
    stopRotation();
  } else {
    startRotation();
  }
}

// Watch for changes
watch(selectedDashboardIds, async () => {
  stopRotation();
  await loadSelectedDashboards();
});

watch(refreshIntervalMs, () => {
  if (isPlaying.value) {
    stopRotation();
    startRotation();
  }
});

// Cleanup
onUnmounted(() => {
  stopRotation();
});

// Load dashboards on mount
onMounted(async () => {
  try {
    await dashboardStore.fetchDashboards({ limit: 1000 });
  } catch {
    /* store sets error */
  }
});
</script>

<template>
  <div>
    <div class="d-flex justify-space-between align-center mb-4">
      <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
      <div class="d-flex ga-2 align-center">
        <!-- Current Dashboard Indicator -->
        <v-chip
          v-if="loadedDashboards.length > 0"
          color="primary"
          variant="flat"
          class="mr-2"
        >
          {{ t('dashboards.container.current') || 'Mevcut' }}: {{ currentDashboardIndex + 1 }} / {{ loadedDashboards.length }}
        </v-chip>
        <!-- Settings Button -->
        <v-btn
          icon
          variant="text"
          color="primary"
          @click="showSettingsModal = true"
        >
          <v-icon>mdi-cog</v-icon>
          <v-tooltip activator="parent" location="top">
            {{ t('dashboards.container.settings') || 'Ayarlar' }}
          </v-tooltip>
        </v-btn>
      </div>
    </div>

    <!-- Error Alert -->
    <v-alert
      v-if="dashboardStore.error"
      type="error"
      variant="tonal"
      closable
      class="mb-4"
      @click:close="dashboardStore.clearError"
    >
      {{ dashboardStore.error }}
    </v-alert>

    <!-- Loading -->
    <template v-if="dashboardStore.loading && loadedDashboards.length === 0">
      <v-card variant="outlined" class="py-12">
        <div class="text-center text-medium-emphasis">
          {{ t('dashboards.view.loading') || 'Yükleniyor...' }}
        </div>
      </v-card>
    </template>

    <!-- No Selection -->
    <v-card v-else-if="selectedDashboardIds.length === 0" variant="outlined" class="py-12">
      <div class="text-center text-medium-emphasis">
        {{ t('dashboards.container.noSelection') || 'Lütfen en az bir dashboard seçin' }}
      </div>
    </v-card>

    <!-- No Dashboards Loaded -->
    <v-card v-else-if="loadedDashboards.length === 0" variant="outlined" class="py-12">
      <div class="text-center">
        <v-icon size="64" color="warning" class="mb-4">mdi-alert-circle-outline</v-icon>
        <div class="text-h6 mb-2">{{ t('dashboards.container.noDashboards') || 'Dashboard Yüklenemedi' }}</div>
        <div class="text-body-2 text-medium-emphasis">
          {{ t('dashboards.container.noDashboardsDesc') || 'Seçilen dashboard\'lar yüklenemedi, görüntüleme yetkiniz yok veya bir hata oluştu.' }}
        </div>
        <div v-if="selectedDashboardIds.length > 0" class="text-caption text-medium-emphasis mt-2">
          Seçilen dashboard sayısı: {{ selectedDashboardIds.length }}
        </div>
      </div>
    </v-card>

    <!-- Current Dashboard -->
    <template v-else-if="currentDashboard">
      <v-card variant="outlined" class="mb-2">
        <v-card-title class="d-flex align-center">
          <v-icon class="mr-2">mdi-view-dashboard</v-icon>
          {{ currentDashboard.title || currentDashboard.name }}
          <v-spacer></v-spacer>
          <v-chip
            v-if="isPlaying"
            color="success"
            size="small"
            prepend-icon="mdi-play"
          >
            {{ t('dashboards.container.playing') || 'Oynatılıyor' }}
          </v-chip>
        </v-card-title>
      </v-card>
      <!-- Dashboard Layout -->
      <template v-if="layout && layout.rows && layout.rows.length > 0">
        <dashboards-dashboard-layout-renderer
          :rows="layout.rows"
          :t="t"
        />
      </template>
      <!-- No Layout -->
      <v-card v-else variant="outlined" class="py-8">
        <div class="text-center">
          <v-icon size="48" color="warning" class="mb-2">mdi-view-dashboard-outline</v-icon>
          <div class="text-h6 mb-2">{{ t('dashboards.view.noLayout') || 'Layout Tanımlı Değil' }}</div>
          <div class="text-body-2 text-medium-emphasis">
            Bu dashboard için layout tanımlanmamış. Lütfen dashboard'u düzenleyip layout ekleyin.
          </div>
        </div>
      </v-card>
    </template>

    <!-- Settings Modal -->
    <v-dialog
      v-model="showSettingsModal"
      max-width="800"
      persistent
    >
      <v-card>
        <v-card-title class="d-flex align-center">
          <v-icon class="mr-2">mdi-cog</v-icon>
          {{ t('dashboards.container.settings') || 'Ayarlar' }}
          <v-spacer></v-spacer>
          <v-btn
            icon
            variant="text"
            size="small"
            @click="showSettingsModal = false"
          >
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-card-text>
          <v-row>
            <!-- Dashboard Selection -->
            <v-col cols="12">
              <v-select
                v-model="selectedDashboardIds"
                :items="availableDashboards"
                :label="t('dashboards.container.selectDashboards') || 'Dashboard Seçin'"
                multiple
                chips
                closable-chips
                variant="outlined"
                :loading="dashboardStore.loading"
              >
                <template #prepend-inner>
                  <v-icon>mdi-view-dashboard</v-icon>
                </template>
              </v-select>
            </v-col>

            <!-- Dashboard Rotation Time Interval -->
            <v-col cols="12">
              <v-divider class="my-2"></v-divider>
              <div class="text-subtitle-2 mb-2">
                {{ t('dashboards.container.rotationInterval') || 'Dashboard Rotasyon Süresi' }}
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model.number="refreshTimeValue"
                type="number"
                min="1"
                :label="t('dashboards.container.intervalValue') || 'Süre'"
                variant="outlined"
                :disabled="isPlaying"
                @update:model-value="(v) => { refreshTimeValue = typeof v === 'number' ? v : Number(v) || 10; }"
              ></v-text-field>
            </v-col>

            <v-col cols="12" md="6">
              <v-select
                v-model="refreshTimeUnit"
                :items="[
                  { title: t('dashboards.view.refreshUnit.seconds') || 'Saniye', value: 'seconds' },
                  { title: t('dashboards.view.refreshUnit.minutes') || 'Dakika', value: 'minutes' },
                ]"
                :label="t('dashboards.container.intervalUnit') || 'Birim'"
                variant="outlined"
                :disabled="isPlaying"
              ></v-select>
            </v-col>

            <!-- Widget Refresh Time Interval -->
            <v-col cols="12">
              <v-divider class="my-2"></v-divider>
              <div class="text-subtitle-2 mb-2">
                {{ t('dashboards.container.widgetRefreshInterval') || 'Widget Yenileme Süresi' }}
              </div>
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model.number="widgetRefreshTimeValue"
                type="number"
                min="1"
                :label="t('dashboards.container.intervalValue') || 'Süre'"
                variant="outlined"
              ></v-text-field>
            </v-col>

            <v-col cols="12" md="6">
              <v-select
                v-model="widgetRefreshTimeUnit"
                :items="[
                  { title: t('dashboards.view.refreshUnit.seconds') || 'Saniye', value: 'seconds' },
                  { title: t('dashboards.view.refreshUnit.minutes') || 'Dakika', value: 'minutes' },
                ]"
                :label="t('dashboards.container.intervalUnit') || 'Birim'"
                variant="outlined"
              ></v-select>
            </v-col>
          </v-row>

          <!-- Info Alert: Single Dashboard Warning -->
          <v-row v-if="loadedDashboards.length === 1">
            <v-col cols="12">
              <v-alert
                type="info"
                variant="tonal"
                density="compact"
                class="mb-2"
              >
                <div class="d-flex align-center">
                  <v-icon class="mr-2">mdi-information-outline</v-icon>
                  <span>
                    {{ t('dashboards.container.singleDashboardWarning') || 'Sadece 1 dashboard seçildiği için rotasyon başlatılamaz. Dashboard görüntülenmeye devam edecek. Rotasyon için en az 2 dashboard seçmelisiniz.' }}
                  </span>
                </div>
              </v-alert>
            </v-col>
          </v-row>

          <!-- Control Buttons -->
          <v-row>
            <v-col cols="12">
              <div class="d-flex ga-2 flex-wrap align-center">
                <v-tooltip
                  v-if="loadedDashboards.length <= 1 || !refreshIntervalMs"
                  :text="
                    loadedDashboards.length <= 1
                      ? (t('dashboards.container.playButtonTooltip.needMoreDashboards') || 'En az 2 dashboard seçmelisiniz')
                      : (t('dashboards.container.playButtonTooltip.needRotationInterval') || 'Rotasyon süresi belirtilmelidir')
                  "
                  location="top"
                >
                  <template #activator="{ props: tooltipProps }">
                    <v-btn
                      v-bind="tooltipProps"
                      :color="isPlaying ? 'error' : 'success'"
                      variant="flat"
                      :prepend-icon="isPlaying ? 'mdi-pause' : 'mdi-play'"
                      :disabled="loadedDashboards.length <= 1 || !refreshIntervalMs"
                      @click.stop="togglePlayPause"
                    >
                      {{ isPlaying ? (t('dashboards.container.pause') || 'Duraklat') : (t('dashboards.container.play') || 'Başlat') }}
                    </v-btn>
                  </template>
                </v-tooltip>
                <v-btn
                  v-else
                  :color="isPlaying ? 'error' : 'success'"
                  variant="flat"
                  :prepend-icon="isPlaying ? 'mdi-pause' : 'mdi-play'"
                  :disabled="loadedDashboards.length <= 1 || !refreshIntervalMs"
                  @click.stop="togglePlayPause"
                >
                  {{ isPlaying ? (t('dashboards.container.pause') || 'Duraklat') : (t('dashboards.container.play') || 'Başlat') }}
                </v-btn>
                <v-btn
                  variant="outlined"
                  prepend-icon="mdi-skip-previous"
                  :disabled="loadedDashboards.length <= 1"
                  @click="currentDashboardIndex = (currentDashboardIndex - 1 + loadedDashboards.length) % loadedDashboards.length"
                >
                  {{ t('dashboards.container.previous') || 'Önceki' }}
                </v-btn>
                <v-btn
                  variant="outlined"
                  prepend-icon="mdi-skip-next"
                  :disabled="loadedDashboards.length <= 1"
                  @click="currentDashboardIndex = (currentDashboardIndex + 1) % loadedDashboards.length"
                >
                  {{ t('dashboards.container.next') || 'Sonraki' }}
                </v-btn>
              </div>
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn
            variant="flat"
            color="primary"
            @click="showSettingsModal = false"
          >
            {{ t('dashboards.container.close') || 'Kapat' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
