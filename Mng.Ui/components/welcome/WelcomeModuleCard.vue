<script setup lang="ts">
import { computed } from 'vue';
import type { WelcomeResolvedModule } from '@/composables/useWelcomePage';
import { useAppI18n } from '@/composables/useAppI18n';

const props = defineProps<{
  module: WelcomeResolvedModule;
}>();

const { t } = useAppI18n();

const displayTitle = computed(() => {
  const mod = props.module;
  if (mod.isFallback && mod.fallbackPageCode) {
    const translated = t(mod.fallbackPageCode);
    if (translated && translated !== mod.fallbackPageCode) return translated;
  }
  if (mod.isFallback && mod.fallbackTitle) return mod.fallbackTitle;
  return t(mod.titleKey);
});
</script>

<template>
  <v-card class="module-card h-100 rounded-xl" elevation="2">
    <v-card-text class="pa-6">
      <v-avatar :color="module.color" variant="tonal" size="52" rounded="lg" class="mb-4">
        <v-icon :icon="module.icon" size="28" />
      </v-avatar>
      <div class="text-h6 font-weight-bold mb-2">
        {{ displayTitle }}
      </div>
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ $t(module.descriptionKey) }}
      </p>
    </v-card-text>
    <v-card-actions class="px-6 pb-6 pt-0 flex-wrap gap-2">
      <v-btn
        v-for="(link, idx) in module.links"
        :key="`${module.id}-${idx}`"
        :color="idx === 0 ? module.color : undefined"
        :variant="idx === 0 ? 'flat' : 'tonal'"
        rounded="lg"
        class="text-none"
        :to="link.to"
      >
        {{ $t(link.labelKey) }}
        <v-icon icon="mdi-chevron-right" end />
      </v-btn>
    </v-card-actions>
  </v-card>
</template>

<style scoped>
.module-card {
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.module-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.08) !important;
}
</style>
