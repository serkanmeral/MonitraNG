<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import {
  SettingsIcon,
  BoxIcon,
  LayoutGridIcon,
  ChevronRightIcon,
  ActivityHeartbeatIcon,
  KeyIcon,
} from 'vue-tabler-icons';

const route = useRoute();
const nuxtApp = useNuxtApp();

function t(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const navItems = computed(() => [
  {
    id: 'monitoring',
    label: t('monitoringConfig.nav.monitoring', 'İzleme Ayarları'),
    icon: ActivityHeartbeatIcon,
    to: '/apps/monitoring/config?section=monitoring',
    description: t('monitoringConfig.nav.monitoringDesc', 'Periyotlar, aralıklar, engine ve agent tanımları'),
  },
  {
    id: 'asset-types',
    label: t('monitoringConfig.nav.assetTypes', 'Asset Türleri'),
    icon: BoxIcon,
    to: '/apps/monitoring/config?section=asset-types',
    description: t('monitoringConfig.nav.assetTypesDesc', 'Aileler, tipler ve collectible şablonları'),
  },
  {
    id: 'widgets',
    label: t('monitoringConfig.nav.widgets', 'Widget\'lar'),
    icon: LayoutGridIcon,
    to: '/apps/monitoring/config?section=widgets',
    description: t('monitoringConfig.nav.widgetsDesc', 'Monitoring widget tanımları'),
  },
  {
    id: 'http-auth',
    label: t('monitoringConfig.nav.httpAuth', 'HTTP Auth Tanımları'),
    icon: KeyIcon,
    to: '/apps/monitoring/config?section=http-auth',
    description: t('monitoringConfig.nav.httpAuthDesc', 'Bearer token endpoint tanımları (HTTP Collector)'),
  },
]);

const activeSection = computed(() => {
  const s = route.query.section as string;
  if (s === 'asset-types') return 'asset-types';
  if (s === 'widgets') return 'widgets';
  if (s === 'http-auth') return 'http-auth';
  return 'monitoring';
});
</script>

<template>
  <div class="configuration-shell">
    <aside class="config-sidebar">
      <div class="sidebar-header">
        <div class="sidebar-header-inner">
          <SettingsIcon size="24" class="sidebar-logo-icon" />
          <span class="sidebar-title">{{ t('monitoringConfig.title', 'İzleme Yapılandırması') }}</span>
        </div>
        <p class="sidebar-subtitle">
          {{ t('monitoringConfig.subtitle', 'İzleme, asset ve widget tanımlarını buradan yönetin') }}
        </p>
      </div>
      <nav class="sidebar-nav">
        <NuxtLink
          v-for="item in navItems"
          :key="item.id"
          :to="item.to"
          class="nav-item"
          :class="{ active: activeSection === item.id }"
        >
          <div class="nav-item-icon">
            <component :is="item.icon" size="22" />
          </div>
          <div class="nav-item-content">
            <span class="nav-item-label">{{ item.label }}</span>
            <span class="nav-item-desc">{{ item.description }}</span>
          </div>
          <ChevronRightIcon size="18" class="nav-item-chevron" />
        </NuxtLink>
      </nav>
    </aside>
    <main class="config-main">
      <div class="main-content">
        <slot />
      </div>
    </main>
  </div>
</template>

<style scoped>
.configuration-shell {
  display: flex;
  min-height: 500px;
  background: rgb(var(--v-theme-surface));
  border-radius: 12px;
  overflow: hidden;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.config-sidebar {
  width: 300px;
  min-width: 300px;
  flex-shrink: 0;
  background: rgba(var(--v-theme-surface));
  border-right: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  display: flex;
  flex-direction: column;
}

.sidebar-header {
  padding: 1.5rem 1.25rem;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.sidebar-header-inner {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.sidebar-logo-icon {
  color: rgb(var(--v-theme-primary));
  flex-shrink: 0;
}

.sidebar-title {
  font-size: 1.25rem;
  font-weight: 600;
  letter-spacing: -0.02em;
}

.sidebar-subtitle {
  font-size: 0.8125rem;
  color: rgba(var(--v-theme-on-surface), 0.6);
  margin: 0.5rem 0 0 2.25rem;
  line-height: 1.4;
}

.sidebar-nav {
  padding: 1rem 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 0.875rem;
  padding: 0.875rem 1rem;
  border-radius: 10px;
  text-decoration: none;
  color: rgb(var(--v-theme-on-surface));
  transition: background 0.2s, color 0.2s;
}

.nav-item:hover {
  background: rgba(var(--v-theme-primary), 0.06);
}

.nav-item.active {
  background: rgba(var(--v-theme-primary), 0.12);
  color: rgb(var(--v-theme-primary));
  font-weight: 500;
}

.nav-item-icon {
  flex-shrink: 0;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 8px;
  background: rgba(var(--v-theme-primary), 0.08);
  color: rgb(var(--v-theme-primary));
}

.nav-item.active .nav-item-icon {
  background: rgba(var(--v-theme-primary), 0.2);
}

.nav-item-content {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.nav-item-label {
  font-size: 0.9375rem;
  font-weight: 500;
}

.nav-item-desc {
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-surface), 0.6);
  line-height: 1.3;
}

.nav-item.active .nav-item-desc {
  color: rgba(var(--v-theme-primary), 0.8);
}

.nav-item-chevron {
  flex-shrink: 0;
  opacity: 0.5;
}

.nav-item.active .nav-item-chevron {
  opacity: 1;
}

.config-main {
  flex: 1;
  min-width: 0;
  overflow-x: hidden;
  background: rgb(var(--v-theme-surface));
}

.main-content {
  padding: 1.5rem 2rem 2rem;
  max-width: 1200px;
  margin: 0 auto;
}

</style>

<style>
/* Unscoped: slot content (child pages) */
.configuration-section .section-header {
  margin-bottom: 1.5rem;
}

.configuration-section .section-title {
  font-size: 1.5rem;
  font-weight: 600;
  letter-spacing: -0.02em;
  margin: 0 0 0.5rem 0;
}

.configuration-section .section-desc {
  font-size: 0.9375rem;
  color: rgba(var(--v-theme-on-surface), 0.7);
  margin: 0;
  line-height: 1.5;
}

.configuration-section .config-card {
  cursor: pointer;
  transition: transform 0.2s, box-shadow 0.2s;
}

.configuration-section .config-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
}

.configuration-section .config-card-icon {
  width: 64px;
  height: 64px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 12px;
  background: rgba(var(--v-theme-primary), 0.1);
  flex-shrink: 0;
}

@media (max-width: 960px) {
  .configuration-shell {
    flex-direction: column;
  }

  .config-sidebar {
    width: 100%;
    min-width: 100%;
    flex-direction: row;
    border-right: none;
    border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  }

  .sidebar-nav {
    flex-direction: row;
    overflow-x: auto;
    padding: 0.75rem;
    flex: 1;
  }

  .nav-item-content,
  .nav-item-chevron {
    display: none;
  }

  .sidebar-header {
    flex-shrink: 0;
    border-bottom: none;
  }
}
</style>
