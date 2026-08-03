<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAppI18n } from '@/composables/useAppI18n';
import AcSiemSettingsCatalogPanel from '@/components/apps/siem-center/AcSiemSettingsCatalogPanel.vue';
import AcSiemSettingsParseRulesPanel from '@/components/apps/siem-center/AcSiemSettingsParseRulesPanel.vue';
import AcSiemSettingsFieldCatalogPanel from '@/components/apps/siem-center/AcSiemSettingsFieldCatalogPanel.vue';
import AcSiemSettingsSourcesPanel from '@/components/apps/siem-center/AcSiemSettingsSourcesPanel.vue';
import AcSiemSettingsScenariosPanel from '@/components/apps/siem-center/AcSiemSettingsScenariosPanel.vue';
import AcSiemDiscoveryPrefixesPanel from '@/components/apps/siem-center/AcSiemDiscoveryPrefixesPanel.vue';
import AcSiemParserReference from '@/components/apps/siem-center/AcSiemParserReference.vue';

/** Top-level SIEM Settings areas. */
const TOP_TABS = ['eventlog', 'discovery', 'reference'] as const;
type TopTab = (typeof TOP_TABS)[number];

const EVENTLOG_SECTIONS = ['catalog', 'parsers', 'fields'] as const;
const DISCOVERY_SECTIONS = ['prefixes'] as const;
const REFERENCE_SECTIONS = ['sources', 'scenarios', 'dictionary'] as const;

type EventlogSection = (typeof EVENTLOG_SECTIONS)[number];
type DiscoverySection = (typeof DISCOVERY_SECTIONS)[number];
type ReferenceSection = (typeof REFERENCE_SECTIONS)[number];

/** Legacy flat tab ids → new tab + optional section. */
const LEGACY_TAB_MAP: Record<string, { tab: TopTab; section?: string }> = {
  catalog: { tab: 'eventlog', section: 'catalog' },
  parsers: { tab: 'eventlog', section: 'parsers' },
  fields: { tab: 'eventlog', section: 'fields' },
  prefixes: { tab: 'discovery', section: 'prefixes' },
  sources: { tab: 'reference', section: 'sources' },
  scenarios: { tab: 'reference', section: 'scenarios' },
  dictionary: { tab: 'reference', section: 'dictionary' },
  eventlog: { tab: 'eventlog' },
  discovery: { tab: 'discovery' },
  reference: { tab: 'reference' },
};

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();

function resolveFromRoute(query: Record<string, unknown>): {
  tab: TopTab;
  eventlogSection: EventlogSection;
  discoverySection: DiscoverySection;
  referenceSection: ReferenceSection;
} {
  const rawTab = typeof query.tab === 'string' ? query.tab : '';
  const rawSection = typeof query.section === 'string' ? query.section : '';

  const mapped = LEGACY_TAB_MAP[rawTab] ?? { tab: 'eventlog' as TopTab };
  const tab = mapped.tab;

  let eventlogSection: EventlogSection = 'catalog';
  let discoverySection: DiscoverySection = 'prefixes';
  let referenceSection: ReferenceSection = 'sources';

  if (tab === 'eventlog') {
    const s = rawSection || mapped.section || 'catalog';
    eventlogSection = (EVENTLOG_SECTIONS as readonly string[]).includes(s)
      ? (s as EventlogSection)
      : 'catalog';
  } else if (tab === 'discovery') {
    const s = rawSection || mapped.section || 'prefixes';
    discoverySection = (DISCOVERY_SECTIONS as readonly string[]).includes(s)
      ? (s as DiscoverySection)
      : 'prefixes';
  } else if (tab === 'reference') {
    const s = rawSection || mapped.section || 'sources';
    referenceSection = (REFERENCE_SECTIONS as readonly string[]).includes(s)
      ? (s as ReferenceSection)
      : 'sources';
  }

  return { tab, eventlogSection, discoverySection, referenceSection };
}

const initial = resolveFromRoute(route.query as Record<string, unknown>);
const activeTab = ref<TopTab>(initial.tab);
const eventlogSection = ref<EventlogSection>(initial.eventlogSection);
const discoverySection = ref<DiscoverySection>(initial.discoverySection);
const referenceSection = ref<ReferenceSection>(initial.referenceSection);

function syncRoute() {
  const next: Record<string, string | string[] | null | undefined> = {
    ...route.query,
    tab: activeTab.value,
  };

  if (activeTab.value === 'eventlog') {
    next.section = eventlogSection.value;
  } else if (activeTab.value === 'discovery') {
    next.section = discoverySection.value;
  } else if (activeTab.value === 'reference') {
    next.section = referenceSection.value;
  } else {
    delete next.section;
  }

  const curTab = typeof route.query.tab === 'string' ? route.query.tab : '';
  const curSection = typeof route.query.section === 'string' ? route.query.section : '';
  if (curTab === next.tab && (curSection || '') === (next.section || '')) return;

  void router.replace({ query: next });
}

watch(
  () => [route.query.tab, route.query.section] as const,
  () => {
    const r = resolveFromRoute(route.query as Record<string, unknown>);
    activeTab.value = r.tab;
    eventlogSection.value = r.eventlogSection;
    discoverySection.value = r.discoverySection;
    referenceSection.value = r.referenceSection;
  },
);

watch([activeTab, eventlogSection, discoverySection, referenceSection], () => {
  syncRoute();
});

const topTabs = computed(() =>
  TOP_TABS.map((id) => ({
    id,
    title: t(`siemCenter.settings.tabs.${id}`),
  })),
);

const eventlogSections = computed(() =>
  EVENTLOG_SECTIONS.map((id) => ({
    id,
    title: t(`siemCenter.settings.sections.eventlog.${id}`),
  })),
);

const discoverySections = computed(() =>
  DISCOVERY_SECTIONS.map((id) => ({
    id,
    title: t(`siemCenter.settings.sections.discovery.${id}`),
  })),
);

const referenceSections = computed(() =>
  REFERENCE_SECTIONS.map((id) => ({
    id,
    title: t(`siemCenter.settings.sections.reference.${id}`),
  })),
);
</script>

<template>
  <div class="siem-settings-view">
    <v-tabs v-model="activeTab" color="primary" class="mb-3" show-arrows>
      <v-tab v-for="tab in topTabs" :key="tab.id" :value="tab.id">
        {{ tab.title }}
      </v-tab>
    </v-tabs>

    <v-tabs-window v-model="activeTab">
      <v-tabs-window-item value="eventlog">
        <p class="text-body-2 text-medium-emphasis mb-3">
          {{ t('siemCenter.settings.tabs.eventlogHint') }}
        </p>
        <v-tabs
          v-model="eventlogSection"
          color="primary"
          density="compact"
          class="mb-3"
          show-arrows
        >
          <v-tab
            v-for="sec in eventlogSections"
            :key="sec.id"
            :value="sec.id"
          >
            {{ sec.title }}
          </v-tab>
        </v-tabs>
        <v-tabs-window v-model="eventlogSection">
          <v-tabs-window-item value="catalog">
            <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
              <AcSiemSettingsCatalogPanel />
            </v-card>
          </v-tabs-window-item>
          <v-tabs-window-item value="parsers">
            <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
              <AcSiemSettingsParseRulesPanel />
            </v-card>
          </v-tabs-window-item>
          <v-tabs-window-item value="fields">
            <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
              <AcSiemSettingsFieldCatalogPanel />
            </v-card>
          </v-tabs-window-item>
        </v-tabs-window>
      </v-tabs-window-item>

      <v-tabs-window-item value="discovery">
        <p class="text-body-2 text-medium-emphasis mb-3">
          {{ t('siemCenter.settings.tabs.discoveryHint') }}
        </p>
        <v-tabs
          v-model="discoverySection"
          color="primary"
          density="compact"
          class="mb-3"
        >
          <v-tab
            v-for="sec in discoverySections"
            :key="sec.id"
            :value="sec.id"
          >
            {{ sec.title }}
          </v-tab>
        </v-tabs>
        <v-tabs-window v-model="discoverySection">
          <v-tabs-window-item value="prefixes">
            <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
              <AcSiemDiscoveryPrefixesPanel />
            </v-card>
          </v-tabs-window-item>
        </v-tabs-window>
      </v-tabs-window-item>

      <v-tabs-window-item value="reference">
        <p class="text-body-2 text-medium-emphasis mb-3">
          {{ t('siemCenter.settings.tabs.referenceHint') }}
        </p>
        <v-tabs
          v-model="referenceSection"
          color="primary"
          density="compact"
          class="mb-3"
          show-arrows
        >
          <v-tab
            v-for="sec in referenceSections"
            :key="sec.id"
            :value="sec.id"
          >
            {{ sec.title }}
          </v-tab>
        </v-tabs>
        <v-tabs-window v-model="referenceSection">
          <v-tabs-window-item value="sources">
            <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
              <AcSiemSettingsSourcesPanel />
            </v-card>
          </v-tabs-window-item>
          <v-tabs-window-item value="scenarios">
            <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
              <AcSiemSettingsScenariosPanel />
            </v-card>
          </v-tabs-window-item>
          <v-tabs-window-item value="dictionary">
            <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
              <AcSiemParserReference />
            </v-card>
          </v-tabs-window-item>
        </v-tabs-window>
      </v-tabs-window-item>
    </v-tabs-window>
  </div>
</template>
