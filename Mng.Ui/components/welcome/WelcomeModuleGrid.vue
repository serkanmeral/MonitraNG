<script setup lang="ts">
import WelcomeModuleCard from '@/components/welcome/WelcomeModuleCard.vue';
import type { WelcomeModuleGroup } from '@/composables/useWelcomePage';

defineProps<{
  groups: WelcomeModuleGroup[];
  loading?: boolean;
}>();
</script>

<template>
  <v-container fluid class="px-4 px-md-6 pb-8">
    <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-4">
      <h2 class="text-h5 font-weight-bold mb-0">
        {{ $t('welcome.modules.title') }}
      </h2>
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ $t('welcome.modules.hint') }}
      </p>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4 rounded-lg" />

    <template v-else-if="groups.length">
      <section v-for="group in groups" :key="group.groupId" class="mb-6">
        <h3 class="text-subtitle-1 font-weight-bold text-medium-emphasis mb-3">
          {{ $t(group.groupKey) }}
        </h3>
        <v-row>
          <v-col
            v-for="mod in group.modules"
            :key="mod.id"
            cols="12"
            sm="6"
            lg="4"
          >
            <WelcomeModuleCard :module="mod" />
          </v-col>
        </v-row>
      </section>
    </template>

    <v-alert
      v-else
      type="info"
      variant="tonal"
      class="rounded-lg"
      density="comfortable"
    >
      {{ $t('welcome.empty.noModules') }}
    </v-alert>
  </v-container>
</template>
