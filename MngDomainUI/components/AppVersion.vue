<template>
  <div class="flex items-center gap-2 text-xs text-gray-500">
    <UIcon name="i-heroicons-information-circle" class="w-4 h-4" />
    <span>{{ displayVersion }}</span>
  </div>
</template>

<script setup lang="ts">
const { displayVersion, fetchVersionInfo } = useVersion()

// Optionally fetch version info on mount
onMounted(async () => {
  await fetchVersionInfo().catch(() => {
    // Ignore errors, use config version as fallback
  })
})
</script>
