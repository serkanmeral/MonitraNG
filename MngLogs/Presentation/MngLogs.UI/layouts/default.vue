<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900 flex flex-col">
    <header class="bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700 shadow-sm">
      <div class="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between items-center h-14">
          <div class="flex items-center gap-6 min-w-0">
            <NuxtLink to="/" class="flex items-center gap-3 hover:opacity-80 transition-opacity shrink-0">
              <img src="/favicon.svg" alt="MngLogs" class="w-8 h-8" />
              <span class="text-lg font-semibold text-gray-900 dark:text-white">MngLogs</span>
              <span
                v-if="agentVersion"
                class="text-xs font-mono text-gray-500 dark:text-gray-400 px-1.5 py-0.5 rounded bg-gray-100 dark:bg-gray-700"
              >v{{ agentVersion }}</span>
              <span
                v-if="platformBadge"
                class="text-[10px] uppercase tracking-wide font-semibold text-primary-700 dark:text-primary-300 px-1.5 py-0.5 rounded bg-primary-50 dark:bg-primary-900/30"
              >{{ platformBadge }}</span>
            </NuxtLink>
            <nav class="flex gap-1 sm:gap-2 overflow-x-auto">
              <NuxtLink
                v-for="link in links"
                :key="link.to"
                :to="link.to"
                class="px-2.5 sm:px-3 py-1.5 rounded-md text-sm font-medium text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 whitespace-nowrap"
                active-class="!text-primary-600 dark:!text-primary-400 !bg-primary-50 dark:!bg-primary-900/20"
              >
                {{ link.label }}
              </NuxtLink>
            </nav>
          </div>
          <span class="text-sm text-gray-500 dark:text-gray-400 hidden sm:inline shrink-0">MonitraNG Saha</span>
        </div>
      </div>
    </header>
    <main class="flex-1 max-w-5xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <slot />
    </main>
    <footer class="bg-white dark:bg-gray-800 border-t border-gray-200 dark:border-gray-700 py-3">
      <div class="max-w-5xl mx-auto px-4 text-center text-sm text-gray-500 dark:text-gray-400">
        &copy; {{ new Date().getFullYear() }} MonitraNG · MngLogs Ajan
        <span v-if="agentVersion" class="font-mono">· v{{ agentVersion }}</span>
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useAgentApi } from '~/composables/useAgentApi'
import { useAgentPlatform } from '~/composables/useAgentPlatform'

const links = [
  { to: '/', label: 'Durum' },
  { to: '/queue', label: 'Kuyruk' },
  { to: '/sources', label: 'Kaynaklar' },
  { to: '/logs', label: 'Loglar' },
  { to: '/policy', label: 'Politika' }
]

const agentVersion = ref<string | null>(null)
const { getStatus } = useAgentApi()
const { applyFromStatus, isLinux } = useAgentPlatform()

const platformBadge = computed(() => {
  if (isLinux.value) return 'Linux'
  return null
})

onMounted(async () => {
  try {
    const s = await getStatus()
    applyFromStatus(s)
    agentVersion.value = s.version || s.hostInventory?.agentVersion || null
  } catch {
    agentVersion.value = null
  }
})
</script>
