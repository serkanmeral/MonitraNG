<script setup lang="ts">
defineProps<{
  label: string;
  value: string | number;
  hint?: string;
  icon?: string;
  color?: 'primary' | 'success' | 'warning' | 'error' | 'info' | 'secondary';
}>();
</script>

<template>
  <v-card
    class="odak-dash-stat pa-4"
    :class="color ? `odak-dash-stat--${color}` : 'odak-dash-stat--primary'"
    rounded="lg"
    variant="flat"
  >
    <div class="d-flex align-start justify-space-between ga-2">
      <div class="min-w-0">
        <div class="text-overline text-medium-emphasis mb-1">{{ label }}</div>
        <div class="text-h4 font-weight-bold odak-dash-stat__value">{{ value }}</div>
        <div v-if="hint" class="text-caption text-medium-emphasis mt-1">{{ hint }}</div>
      </div>
      <v-avatar v-if="icon" size="44" variant="tonal" :color="color ?? 'primary'" class="flex-shrink-0">
        <v-icon :icon="icon" size="22" />
      </v-avatar>
    </div>
  </v-card>
</template>

<style scoped>
.odak-dash-stat {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgb(var(--v-theme-surface));
  min-height: 108px;
}

.odak-dash-stat--primary {
  border-top: 3px solid rgb(var(--v-theme-primary));
}

.odak-dash-stat--success {
  border-top: 3px solid rgb(var(--v-theme-success));
}

.odak-dash-stat--warning {
  border-top: 3px solid rgb(var(--v-theme-warning));
}

.odak-dash-stat--error {
  border-top: 3px solid rgb(var(--v-theme-error));
}

.odak-dash-stat--info {
  border-top: 3px solid rgb(var(--v-theme-info));
}

.odak-dash-stat--secondary {
  border-top: 3px solid rgba(var(--v-theme-on-surface), 0.35);
}

.odak-dash-stat__value {
  line-height: 1.15;
}
</style>
