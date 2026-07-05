<script setup lang="ts">
import { onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import KeeperListSettingsTab from '@/components/apps/keeper/KeeperListSettingsTab.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useKeeperListSettingsStore } from '@/stores/apps/keeperListSettings';
import { keeperUserListFieldLabel } from '@/utils/keeperListCellService';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const keeperStore = useKeeperListSettingsStore();
const loadError = ref('');

const page = { title: t('users.listSettings.title') };
const breadcrumbs = [
  { text: t('users.breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('users.breadcrumbs.users'), disabled: false, href: '/apps/users' },
  { text: t('users.listSettings.title'), disabled: true, href: '#' },
];

onMounted(() => {
  try {
    keeperStore.ensureReady(true);
  } catch (e: unknown) {
    loadError.value = e instanceof Error ? e.message : String(e);
  }
});

onBeforeRouteLeave((_to, _from, next) => {
  if (!keeperStore.isScopeDirty('users_list')) {
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
        <span class="text-h6">{{ t('users.listSettings.title') }}</span>
        <v-spacer />
        <v-btn variant="text" prepend-icon="mdi-arrow-left" to="/apps/users">
          {{ t('users.listSettings.backToList') }}
        </v-btn>
      </v-card-title>
      <v-divider />
      <v-card-text class="px-5 py-4">
        <v-alert v-if="loadError" type="error" variant="tonal" density="compact" class="mb-4">
          {{ loadError }}
        </v-alert>
        <KeeperListSettingsTab
          scope="users_list"
          hint-key="users.listSettings.hint"
          :field-label="(fieldName) => keeperUserListFieldLabel(fieldName, t)"
        />
      </v-card-text>
    </v-card>
  </div>
</template>
