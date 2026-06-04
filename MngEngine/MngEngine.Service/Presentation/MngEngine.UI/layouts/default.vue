<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900 flex flex-col">
    <header class="bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700 shadow-sm">
      <div class="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between items-center h-14">
          <div class="flex items-center gap-6">
            <NuxtLink to="/" class="flex items-center gap-3 hover:opacity-80 transition-opacity">
              <img src="/favicon.svg" alt="MngEngine" class="w-8 h-8" />
              <span class="text-lg font-semibold text-gray-900 dark:text-white">MngEngine</span>
            </NuxtLink>
            <nav class="flex gap-2">
              <NuxtLink
                to="/"
                class="px-3 py-1.5 rounded-md text-sm font-medium text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700"
                active-class="!text-primary-600 dark:!text-primary-400 !bg-primary-50 dark:!bg-primary-900/20"
              >
                Durum
              </NuxtLink>
              <NuxtLink
                to="/logs"
                class="px-3 py-1.5 rounded-md text-sm font-medium text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700"
                active-class="!text-primary-600 dark:!text-primary-400 !bg-primary-50 dark:!bg-primary-900/20"
              >
                Loglar
              </NuxtLink>
              <NuxtLink
                to="/assets"
                class="px-3 py-1.5 rounded-md text-sm font-medium text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700"
                active-class="!text-primary-600 dark:!text-primary-400 !bg-primary-50 dark:!bg-primary-900/20"
              >
                Asset'ler
              </NuxtLink>
              <NuxtLink
                to="/jobs"
                class="px-3 py-1.5 rounded-md text-sm font-medium text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700"
                active-class="!text-primary-600 dark:!text-primary-400 !bg-primary-50 dark:!bg-primary-900/20"
              >
                Job'lar
              </NuxtLink>
              <NuxtLink
                to="/queue"
                class="px-3 py-1.5 rounded-md text-sm font-medium text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700"
                active-class="!text-primary-600 dark:!text-primary-400 !bg-primary-50 dark:!bg-primary-900/20"
              >
                Queue
              </NuxtLink>
            </nav>
          </div>
          <span class="text-sm text-gray-500 dark:text-gray-400">MonitraNG Edge</span>
        </div>
      </div>
    </header>
    <main class="flex-1 max-w-4xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <slot />
    </main>
    <footer class="bg-white dark:bg-gray-800 border-t border-gray-200 dark:border-gray-700 py-3">
      <div class="max-w-4xl mx-auto px-4 text-center text-sm text-gray-500 dark:text-gray-400">
        &copy; {{ new Date().getFullYear() }} MonitraNG · Engine UI
      </div>
    </footer>
  </div>
</template>
