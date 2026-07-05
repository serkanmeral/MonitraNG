<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

const { t } = useAppI18n();
const route = useRoute();

const tabs = computed(() => [
  { to: '/apps/odak-egitim/trainings', label: t('odakEgitim.nav.trainings') },
  { to: '/apps/odak-egitim/person-trainings', label: t('odakEgitim.nav.personTrainings') },
  { to: '/apps/odak-egitim/divisions', label: t('odakEgitim.nav.divisions') },
  { to: '/apps/odak-egitim/stats', label: t('odakEgitim.nav.stats') },
]);

const activeTo = computed(() => {
  const path = route.path.replace(/\/+$/, '');
  if (path.startsWith('/apps/odak-egitim/person-trainings')) return '/apps/odak-egitim/person-trainings';
  if (path.startsWith('/apps/odak-egitim/divisions')) return '/apps/odak-egitim/divisions';
  if (path.startsWith('/apps/odak-egitim/stats')) return '/apps/odak-egitim/stats';
  if (path.startsWith('/apps/odak-egitim/trainings/')) return '/apps/odak-egitim/trainings';
  if (path.startsWith('/apps/odak-egitim/trainings')) return '/apps/odak-egitim/trainings';
  return '/apps/odak-egitim/trainings';
});
</script>

<template>
  <v-tabs :model-value="activeTo" density="comfortable" color="primary" class="mb-4">
    <v-tab v-for="tab in tabs" :key="tab.to" :value="tab.to" :to="tab.to">
      {{ tab.label }}
    </v-tab>
  </v-tabs>
</template>
