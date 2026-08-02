<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { coverageColor } from '@/composables/useSiemDiscoveryMock';
import type { SiemDiscoveryHost } from '@/types/apps/siemDiscovery';
import { resolveOsFamily, type SiemOsFamily } from '@/utils/siemDiscoveryOs';

const props = withDefaults(
  defineProps<{
    host: SiemDiscoveryHost;
    /** tree | compact | wide */
    density?: 'tree' | 'compact' | 'wide';
  }>(),
  { density: 'tree' },
);

const emit = defineEmits<{
  click: [];
}>();

const { t } = useAppI18n();

const family = computed((): SiemOsFamily => resolveOsFamily(props.host));

const osIcon = computed(() => {
  if (family.value === 'windows') return 'mdi-microsoft-windows';
  if (family.value === 'linux') return 'mdi-linux';
  return 'mdi-help-rhombus-outline';
});

const osLabel = computed(() => t(`siemCenter.discovery.osFamily.${family.value}`));

/** Align OS glyph with coverage legend (online / quiet / no agent). */
const osColor = computed(() => coverageColor(props.host.coverage));

const coverageLabel = computed(() => t(`siemCenter.discovery.coverage.${props.host.coverage}`));

const coverageTone = computed(() => osColor.value);

const roleKey = computed(() => props.host.deviceRoleHint || 'unknown');
const roleLabel = computed(() => {
  const key = `siemCenter.discovery.deviceRole.${roleKey.value}`;
  const translated = t(key);
  return translated === key ? roleKey.value : translated;
});

const confidence = computed(() => (props.host.identityConfidence || '').toLowerCase());
const confidenceLabel = computed(() => {
  if (!confidence.value) return '';
  const key = `siemCenter.discovery.identityConfidence.${confidence.value}`;
  const translated = t(key);
  return translated === key ? confidence.value : translated;
});
const confidenceColor = computed(() => {
  if (confidence.value === 'high') return 'success';
  if (confidence.value === 'medium') return 'warning';
  if (confidence.value === 'low') return 'grey';
  return 'grey';
});

const identityLine = computed(() => {
  if (props.host.identitySummary) return props.host.identitySummary;
  if (props.host.deviceRoleHint) return `${osLabel.value} · ${roleLabel.value}`;
  return '';
});

const siteCaption = computed(() => {
  const label = (props.host.siteLabel || '').trim();
  if (!label || label === 'Unscoped' || label === 'No IP') return '';
  return label;
});

const portsLabel = computed(() => {
  const ports = props.host.openPorts ?? [];
  if (!ports.length) return '';
  return ports.slice(0, 5).join(', ');
});

const hasAgent = computed(() => !!props.host.agent || (props.host.sources ?? []).includes('agent'));
const hasScan = computed(() => (props.host.sources ?? []).some((s) => s.toLowerCase() === 'scan'));
const hasAd = computed(() => (props.host.sources ?? []).some((s) => s.toLowerCase() === 'ad'));
</script>

<template>
  <button
    type="button"
    class="node node-host node-host-link host-node"
    :class="[`density-${density}`, `cov-${coverageTone}`]"
    @click="emit('click')"
  >
    <span class="legend-dot" :class="`bg-${osColor}`" />
    <v-icon :icon="osIcon" size="18" :color="osColor" class="host-os-icon" />
    <div class="host-node-body min-w-0">
      <div class="d-flex align-center ga-1 min-w-0">
        <span class="text-body-2 font-weight-medium text-truncate">{{ host.hostname }}</span>
        <v-icon
          v-if="density !== 'compact'"
          icon="mdi-open-in-new"
          size="12"
          class="text-medium-emphasis flex-shrink-0"
        />
      </div>
      <div class="text-caption text-medium-emphasis text-truncate">
        {{ host.ip }}
        <span v-if="density !== 'compact' && siteCaption"> · {{ siteCaption }}</span>
        <span v-if="density !== 'compact' && identityLine"> · {{ identityLine }}</span>
        <span v-else-if="density !== 'compact'"> · {{ osLabel }}</span>
        <span v-if="density === 'wide'"> · {{ coverageLabel }}</span>
      </div>
      <div v-if="density !== 'compact'" class="host-meta d-flex flex-wrap align-center ga-1 mt-1">
        <v-chip
          v-if="host.deviceRoleHint && host.deviceRoleHint !== 'unknown'"
          size="x-small"
          variant="tonal"
          color="info"
          label
        >
          {{ roleLabel }}
        </v-chip>
        <v-chip
          v-if="confidenceLabel"
          size="x-small"
          variant="tonal"
          :color="confidenceColor"
          label
        >
          {{ confidenceLabel }}
        </v-chip>
        <v-chip
          v-if="hasScan"
          size="x-small"
          variant="tonal"
          color="secondary"
          label
        >
          scan
        </v-chip>
        <v-chip
          v-if="hasAd"
          size="x-small"
          variant="tonal"
          color="primary"
          label
        >
          AD
        </v-chip>
        <v-chip
          v-if="hasAgent"
          size="x-small"
          variant="tonal"
          color="success"
          label
        >
          agent
        </v-chip>
        <v-chip
          v-if="portsLabel"
          size="x-small"
          variant="outlined"
          label
          class="ports-chip"
        >
          {{ portsLabel }}
        </v-chip>
      </div>
    </div>
  </button>
</template>

<style scoped>
.host-node {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  text-align: left;
}
.density-tree {
  width: 100%;
}
.density-wide {
  width: auto;
  min-width: 200px;
  max-width: 280px;
}
.density-compact {
  width: auto;
  padding: 6px 8px;
}
.host-os-icon {
  margin-top: 2px;
  flex-shrink: 0;
}
.host-node-body {
  flex: 1;
  min-width: 0;
}
.ports-chip {
  font-variant-numeric: tabular-nums;
}
.host-node.cov-success {
  border-color: rgba(var(--v-theme-success), 0.45);
}
.host-node.cov-warning {
  border-color: rgba(var(--v-theme-warning), 0.5);
}
.host-node.cov-error {
  border-color: rgba(var(--v-theme-error), 0.45);
}
.host-node.cov-grey {
  border-color: rgba(var(--v-theme-on-surface), 0.16);
}
</style>
