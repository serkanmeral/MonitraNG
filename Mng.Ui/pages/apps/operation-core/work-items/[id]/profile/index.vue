<script setup lang="ts">
import { computed, onMounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useOperationCoreBreadcrumbs } from '@/composables/useOperationCoreBreadcrumbs';
import { useOperationCoreStore } from '@/stores/apps/operationCore';
import { useAppI18n } from '@/composables/useAppI18n';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const store = useOperationCoreStore();

const workItemId = computed(() => String(route.params.id ?? ''));

const { breadcrumbs } = useOperationCoreBreadcrumbs({
  tail: computed(() => ({
    text: t('operationCore.profile.placeholderTitle'),
    disabled: true,
  })),
});

onMounted(async () => {
  if (!store.workspaces.length) {
    await store.loadWorkspaces();
  }
});
</script>

<template>
  <div class="oc-flow oc-profile-page">
    <BaseBreadcrumb
      :title="t('operationCore.profile.placeholderTitle')"
      :breadcrumbs="breadcrumbs"
    />

    <v-card variant="outlined" class="rounded-lg">
      <v-card-text class="pa-8 text-center">
        <v-icon icon="mdi-card-account-details-outline" size="56" color="primary" class="mb-4 opacity-70" />
        <p class="text-h6 font-weight-medium mb-2">
          {{ t('operationCore.profile.sprint3Hint') }}
        </p>
        <p class="text-body-2 text-medium-emphasis mb-4">
          {{ t('operationCore.profile.workItemId') }}: {{ workItemId }}
        </p>
        <v-btn
          v-if="route.query.boardId"
          variant="tonal"
          color="primary"
          class="text-none"
          :to="`/apps/operation-core/boards/${encodeURIComponent(String(route.query.boardId))}`"
        >
          {{ t('operationCore.board.backToBoard') }}
        </v-btn>
      </v-card-text>
    </v-card>
  </div>
</template>
