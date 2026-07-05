<script setup lang="ts">
import { onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import KeeperListSettingsTab from '@/components/apps/keeper/KeeperListSettingsTab.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useKeeperListSettingsStore } from '@/stores/apps/keeperListSettings';
import { keeperGroupListFieldLabel } from '@/utils/keeperListCellService';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const keeperStore = useKeeperListSettingsStore();
const loadError = ref('');

const page = { title: t('groups.listSettings.title') };
const breadcrumbs = [
  { text: t('groups.breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('groups.breadcrumbs.groups'), disabled: false, href: '/apps/groups' },
  { text: t('groups.listSettings.title'), disabled: true, href: '#' },
];

onMounted(() => {
  try {
    keeperStore.ensureReady(true);
  } catch (e: unknown) {
    loadError.value = e instanceof Error ? e.message : String(e);
  }
});

onBeforeRouteLeave((_to, _from, next) => {
  if (!keeperStore.isScopeDirty('groups_list')) {
    next();
    return;
  }
  next(window.confirm(t('keeper.listSettings.unsavedConfirm')));
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
    <v-card elevation="10">
      <v-card-title class="d-flex flex-wrap align-center ga-3 py-4">
        <span class="text-h6">{{ t('groups.listSettings.title') }}</span>
        <v-spacer />
        <v-btn variant="text" prepend-icon="mdi-arrow-left" to="/apps/groups">
          {{ t('groups.listSettings.backToList') }}
        </v-btn>
      </v-card-title>
      <v-divider />
      <v-card-text class="px-5 py-4">
        <v-alert v-if="loadError" type="error" variant="tonal" density="compact" class="mb-4">
          {{ loadError }}
        </v-alert>
        <KeeperListSettingsTab
          scope="groups_list"
          hint-key="groups.listSettings.hint"
          :field-label="(fieldName) => keeperGroupListFieldLabel(fieldName, t)"
        />
      </v-card-text>
    </v-card>
  </div>
</template>
