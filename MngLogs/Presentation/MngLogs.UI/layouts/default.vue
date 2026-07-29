<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900 flex flex-col">
    <header class="bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700 shadow-sm">
      <div class="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between items-center h-14">
          <div class="flex items-center gap-6 min-w-0">
            <NuxtLink to="/" class="flex items-center gap-3 hover:opacity-80 transition-opacity shrink-0">
              <img src="/favicon.svg" alt="MngLogs" class="w-8 h-8" />
              <span class="text-lg font-semibold text-gray-900 dark:text-white">MngLogs</span>
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
        &copy; {{ new Date().getFullYear() }} MonitraNG · MngLogs Ajan Arayüzü
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
const links = [
  { to: '/', label: 'Durum' },
  { to: '/queue', label: 'Kuyruk' },
  { to: '/sources', label: 'Kaynaklar' },
  { to: '/logs', label: 'Loglar' },
  { to: '/policy', label: 'Politika' }
]
</script>
