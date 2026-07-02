<script setup lang="ts">
import { computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import AcSecEventsExplorer from '@/components/apps/siem-center/AcSecEventsExplorer.vue';
import { useSiemCenterBreadcrumbs } from '@/composables/useSiemCenterBreadcrumbs';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePagePermissions } from '@/composables/usePagePermissions';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const { canView } = usePagePermissions();

const { breadcrumbs } = useSiemCenterBreadcrumbs({
  subPage: true,
  tail: computed(() => ({
    text: t('siemCenter.events.pageTitle'),
    disabled: true,
  })),
});

onMounted(async () => {
  const { useSideMenuStore } = await import('@/stores/apps/sideMenu');
  const menuStore = useSideMenuStore();
  try {
    await menuStore.loadMenuItems(false);
  } catch {
    // fallback: middleware already checked route access
  }
  if (!canView.value) {
    void navigateTo('/unauthorized');
  }
});
</script>

<template>
  <div class="siem-events-page">
    <BaseBreadcrumb
      :title="t('siemCenter.events.pageTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <div class="siem-hero mb-4 pa-4 pa-md-5 rounded-lg">
      <div class="d-flex flex-wrap align-start justify-space-between gap-3">
        <div>
          <h1 class="text-h5 font-weight-bold mb-2">
            {{ t('siemCenter.events.pageTitle') }}
          </h1>
          <p class="text-body-2 text-medium-emphasis mb-0">
            {{ t('siemCenter.events.pageSubtitle') }}
          </p>
        </div>
        <v-btn
          variant="outlined"
          color="primary"
          prepend-icon="mdi-book-open-page-variant"
          to="/apps/siem-center/reference"
        >
          {{ t('siemCenter.reference.openGuide') }}
        </v-btn>
      </div>
    </div>

    <v-card variant="outlined" class="rounded-lg pa-4 pa-md-5">
      <AcSecEventsExplorer />
    </v-card>
  </div>
</template>
