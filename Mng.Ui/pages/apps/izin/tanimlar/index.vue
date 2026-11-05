<script setup lang="ts">
import { computed, provide, ref, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AutomatedFormView from '@/pages/apps/automated-forms/view/[formCode].vue';

definePageMeta({ layout: 'default' });

const route = useRoute();
const router = useRouter();

const tabs = [
  { key: 'ayarlar', title: 'Ayarlar', icon: 'mdi-cog-outline', formCode: 'izin-ayarlar-form' },
  { key: 'bant', title: 'Hak bantları', icon: 'mdi-chart-bar', formCode: 'izin-hak-bantlari-form' },
  { key: 'tur', title: 'İzin türleri', icon: 'mdi-format-list-bulleted', formCode: 'izin-turleri-form' },
  { key: 'kalip', title: 'Tatil kalıpları', icon: 'mdi-calendar-sync', formCode: 'izin-tatil-kaliplari-form' },
  { key: 'gun', title: 'Tatil günleri', icon: 'mdi-calendar-remove', formCode: 'izin-tatil-gunleri-form' },
];

const keys = new Set(tabs.map((tab) => tab.key));
const initial = String(route.query.sekme || 'ayarlar');
const tab = ref(keys.has(initial) ? initial : 'ayarlar');

const active = computed(() => tabs.find((item) => item.key === tab.value) ?? tabs[0]);
const embeddedFormCode = computed(() => active.value.formCode);
provide('afEmbeddedFormCode', embeddedFormCode);

watch(tab, (value) => {
  if (route.query.sekme === value) return;
  void router.replace({ query: { ...route.query, sekme: value } });
});

const breadcrumbs = [
  { text: 'AnkaraBT', disabled: false, href: '/dashboards/analytical' },
  { text: 'Personel izin', disabled: true, href: '#' },
  { text: 'Tanımlar', disabled: true, href: '#' },
];
</script>

<template>
  <div>
    <BaseBreadcrumb title="Tanımlar" :breadcrumbs="breadcrumbs" />

    <v-card variant="outlined" class="rounded-lg">
      <div class="d-flex">
        <v-tabs
          v-model="tab"
          direction="vertical"
          color="primary"
          class="izin-def-side-tabs flex-shrink-0 py-2"
        >
          <v-tab
            v-for="item in tabs"
            :key="item.key"
            :value="item.key"
            class="text-none justify-start"
          >
            <v-icon :icon="item.icon" start size="20" />
            {{ item.title }}
          </v-tab>
        </v-tabs>
        <v-divider vertical />
        <v-tabs-window v-model="tab" class="flex-grow-1" style="min-width: 0">
          <v-tabs-window-item
            v-for="item in tabs"
            :key="item.key"
            :value="item.key"
          >
            <AutomatedFormView v-if="tab === item.key" />
          </v-tabs-window-item>
        </v-tabs-window>
      </div>
    </v-card>
  </div>
</template>

<style scoped>
.izin-def-side-tabs {
  min-width: 232px;
  max-width: 260px;
}

.izin-def-side-tabs :deep(.v-tab) {
  justify-content: flex-start;
  text-align: left;
  min-height: 44px;
}
</style>
