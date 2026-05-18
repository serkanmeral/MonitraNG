<script setup lang="ts">
const nuxtApp = useNuxtApp();
function t(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}
</script>

<template>
  <div class="configuration-section">
    <div class="section-header">
      <h2 class="section-title">{{ t('monitoringConfig.sections.widgets', 'Widget\'lar') }}</h2>
      <p class="section-desc">
        {{ t('monitoringConfig.sections.widgetsDesc', 'Monitoring dashboard widget tanımlarını oluşturun ve yönetin.') }}
      </p>
    </div>
    <div class="section-content">
      <v-card variant="tonal" class="config-card" to="/apps/monitoring/widgets">
        <v-card-text class="pa-6">
          <div class="d-flex align-center gap-4">
            <div class="config-card-icon">
              <v-icon size="48" color="primary">mdi-view-dashboard-outline</v-icon>
            </div>
            <div class="flex-grow-1">
              <h3 class="text-h6 mb-2">{{ t('monitoringConfig.openWidgets', 'Widget\'ları yönet') }}</h3>
              <p class="text-body-2 text-medium-emphasis mb-0">
                {{ t('monitoringConfig.openWidgetsDesc', 'Chart ve card widget\'ları oluşturun, düzenleyin ve dashboard\'lara ekleyin.') }}
              </p>
            </div>
            <v-icon>mdi-chevron-right</v-icon>
          </div>
        </v-card-text>
      </v-card>
    </div>
  </div>
</template>
