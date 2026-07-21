<script setup lang="ts">
import { RouterLink } from 'vue-router';
</script>

<template>
  <div class="logo">
    <NuxtLink to="/" class="logo-icon-link">
      <img src="/images/logos/monitrang-icon.png" alt="MonitraNG" width="40" height="40" />
    </NuxtLink>
  </div>
</template>

<style scoped>
.logo-icon-link {
  display: inline-flex;
  align-items: center;
  text-decoration: none;
}

.logo-icon-link img {
  display: block;
}
</style>
