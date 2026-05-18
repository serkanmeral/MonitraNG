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
      <h2 class="section-title">{{ t('monitoringConfig.sections.monitoring', 'İzleme Ayarları') }}</h2>
      <p class="section-desc">
        {{ t('monitoringConfig.sections.monitoringDesc', 'Toplama periyotları, izleme aralıkları, engine ve agent tanımlarını yönetin.') }}
      </p>
    </div>
    <div class="section-content">
      <v-card variant="tonal" class="config-card" to="/apps/monitoring">
        <v-card-text class="pa-6">
          <div class="d-flex align-center gap-4">
            <div class="config-card-icon">
              <v-icon size="48" color="primary">mdi-cog-play-outline</v-icon>
            </div>
            <div class="flex-grow-1">
              <h3 class="text-h6 mb-2">{{ t('monitoringConfig.openMonitoring', 'İzleme ayarlarını yönet') }}</h3>
              <p class="text-body-2 text-medium-emphasis mb-0">
                {{ t('monitoringConfig.openMonitoringDesc', 'Periyotlar, aralıklar, engine\'ler ve agent\'lar tanımlarını görüntüleyin ve düzenleyin.') }}
              </p>
            </div>
            <v-icon>mdi-chevron-right</v-icon>
          </div>
        </v-card-text>
      </v-card>
    </div>
  </div>
</template>
