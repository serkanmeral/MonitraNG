<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAppI18n } from '@/composables/useAppI18n';
import AcSiemSettingsCatalogPanel from '@/components/apps/siem-center/AcSiemSettingsCatalogPanel.vue';
import AcSiemSettingsSourcesPanel from '@/components/apps/siem-center/AcSiemSettingsSourcesPanel.vue';
import AcSiemSettingsScenariosPanel from '@/components/apps/siem-center/AcSiemSettingsScenariosPanel.vue';
import AcSiemParserReference from '@/components/apps/siem-center/AcSiemParserReference.vue';

const TAB_IDS = ['catalog', 'sources', 'scenarios', 'dictionary'] as const;
type TabId = (typeof TAB_IDS)[number];

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();

function parseTab(raw: unknown): TabId {
  const v = typeof raw === 'string' ? raw : '';
  return (TAB_IDS as readonly string[]).includes(v) ? (v as TabId) : 'catalog';
}

const activeTab = ref<TabId>(parseTab(route.query.tab));

watch(
  () => route.query.tab,
  (tab) => {
    activeTab.value = parseTab(tab);
  },
);

watch(activeTab, (tab) => {
  if (route.query.tab === tab) return;
  void router.replace({ query: { ...route.query, tab } });
});

const tabs = computed(() =>
  TAB_IDS.map((id) => ({
    id,
    title: t(`siemCenter.settings.tabs.${id}`),
  })),
);
</script>

<template>
  <div class="siem-settings-view">
    <v-tabs v-model="activeTab" color="primary" class="mb-4" show-arrows>
      <v-tab v-for="tab in tabs" :key="tab.id" :value="tab.id">
        {{ tab.title }}
      </v-tab>
    </v-tabs>

    <v-tabs-window v-model="activeTab">
      <v-tabs-window-item value="catalog">
        <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
          <AcSiemSettingsCatalogPanel />
        </v-card>
      </v-tabs-window-item>
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
  </div>
</template>
