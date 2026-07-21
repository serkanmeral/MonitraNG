<script setup lang="ts">
import { RouterLink } from 'vue-router';
</script>

<template>
  <div class="logo">
    <NuxtLink to="/" class="logo-brand logo-brand--light">
      <img src="/images/logos/monitrang-icon.png" alt="" class="logo-brand__icon" width="40" height="40" />
      <span class="logo-brand__text">
        <span class="logo-brand__monitra">Monitra</span><span class="logo-brand__ng">NG</span>
      </span>
    </NuxtLink>
  </div>
</template>

<style scoped>
.logo-brand {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  text-decoration: none;
  line-height: 1;
}

.logo-brand__icon {
  display: block;
  flex-shrink: 0;
}

.logo-brand__text {
  font-family: Inter, 'Segoe UI', Arial, sans-serif;
  font-size: 22px;
  letter-spacing: -0.03em;
  white-space: nowrap;
}

.logo-brand__monitra {
  font-weight: 600;
}

.logo-brand__ng {
  font-weight: 700;
}

.logo-brand--light .logo-brand__monitra {
  color: #ffffff;
}

.logo-brand--light .logo-brand__ng {
  color: #38bdf8;
}
</style>
