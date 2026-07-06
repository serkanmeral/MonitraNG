<script setup lang="ts">
import WelcomeHero from '@/components/welcome/WelcomeHero.vue';
import WelcomeActionStrip from '@/components/welcome/WelcomeActionStrip.vue';
import WelcomeModuleGrid from '@/components/welcome/WelcomeModuleGrid.vue';
import { useWelcomePage } from '@/composables/useWelcomePage';

const { loading, moduleGroups } = useWelcomePage();
</script>

<template>
  <div class="welcome-page">
    <WelcomeHero />
    <WelcomeActionStrip />
    <WelcomeModuleGrid :groups="moduleGroups" :loading="loading" />
  </div>
</template>

<style scoped>
.welcome-page {
  min-height: 100%;
  background: rgb(var(--v-theme-background));
}
</style>
