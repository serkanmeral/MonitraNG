<script setup lang="ts">
import WelcomeHero from '@/components/welcome/WelcomeHero.vue';
import WelcomePersonnelBoard from '@/components/welcome/WelcomePersonnelBoard.vue';
</script>

<template>
  <div class="welcome-page">
    <WelcomeHero />
    <WelcomePersonnelBoard />
  </div>
</template>

<style scoped>
.welcome-page {
  min-height: 100%;
  background: rgb(var(--v-theme-background));
}
</style>
