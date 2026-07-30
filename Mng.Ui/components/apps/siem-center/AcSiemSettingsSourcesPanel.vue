<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

const { t } = useAppI18n();

interface SourceRow {
  value: string;
  titleKey: string;
  hintKey: string;
  group: 'security' | 'telemetry';
}

const rows = computed((): SourceRow[] => [
  {
    value: 'ad',
    titleKey: 'siemCenter.events.sourceAd',
    hintKey: 'siemCenter.settings.sources.hintAd',
    group: 'security',
  },
  {
    value: 'firewall',
    titleKey: 'siemCenter.events.sourceFirewall',
    hintKey: 'siemCenter.settings.sources.hintFirewall',
    group: 'security',
  },
  {
    value: 'endpoint',
    titleKey: 'siemCenter.events.sourceEndpoint',
    hintKey: 'siemCenter.settings.sources.hintEndpoint',
    group: 'security',
  },
  {
    value: 'windows-eventlog',
    titleKey: 'siemCenter.events.sourceWindowsEventLog',
    hintKey: 'siemCenter.settings.sources.hintWindowsEventLog',
    group: 'security',
  },
  {
    value: 'bastion',
    titleKey: 'siemCenter.settings.sources.sourceBastion',
    hintKey: 'siemCenter.settings.sources.hintBastion',
    group: 'security',
  },
  {
    value: 'metric',
    titleKey: 'siemCenter.events.sourceMetric',
    hintKey: 'siemCenter.settings.sources.hintMetric',
    group: 'telemetry',
  },
]);

function eventsLink(sourceType: string): string {
  return `/apps/siem-center/events?sourceType=${encodeURIComponent(sourceType)}`;
}
</script>

<template>
  <div class="siem-settings-sources">
    <p class="text-body-2 text-medium-emphasis mb-4">
      {{ t('siemCenter.settings.sources.intro') }}
    </p>

    <h3 class="text-subtitle-1 font-weight-bold mb-2">
      {{ t('siemCenter.settings.sources.securityGroup') }}
    </h3>
    <v-list lines="two" class="mb-6 bg-transparent">
      <v-list-item
        v-for="row in rows.filter((r) => r.group === 'security')"
        :key="row.value"
        :title="t(row.titleKey)"
        :subtitle="t(row.hintKey)"
      >
        <template #append>
          <v-btn
            size="small"
            variant="text"
            color="primary"
            :to="eventsLink(row.value)"
          >
            {{ t('siemCenter.settings.sources.openEvents') }}
          </v-btn>
        </template>
      </v-list-item>
    </v-list>

    <h3 class="text-subtitle-1 font-weight-bold mb-2">
      {{ t('siemCenter.settings.sources.telemetryGroup') }}
    </h3>
    <v-list lines="two" class="bg-transparent">
      <v-list-item
        v-for="row in rows.filter((r) => r.group === 'telemetry')"
        :key="row.value"
        :title="t(row.titleKey)"
        :subtitle="t(row.hintKey)"
      >
        <template #append>
          <v-btn
            size="small"
            variant="text"
            color="primary"
            :to="eventsLink(row.value)"
          >
            {{ t('siemCenter.settings.sources.openEvents') }}
          </v-btn>
        </template>
      </v-list-item>
    </v-list>
  </div>
</template>
