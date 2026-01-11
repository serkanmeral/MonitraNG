<template>
  <div class="min-h-screen bg-gray-50 flex flex-col">
    <!-- Header -->
    <header class="bg-white border-b border-gray-200 shadow-sm">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between items-center h-16">
          <div class="flex items-center gap-4">
            <NuxtLink to="/domains" class="flex items-center gap-3 hover:opacity-80 transition-opacity">
              <img src="/favicon.svg" alt="MonitraNG" class="w-8 h-8" />
              <h1 class="text-xl font-bold text-gray-900">MonitraNG</h1>
            </NuxtLink>
          </div>
          
          <div class="flex items-center gap-4">
            <div v-if="authStore.user" class="flex items-center gap-2 text-sm text-gray-600">
              <UIcon name="i-heroicons-user-circle" class="w-5 h-5" />
              <span class="font-medium">{{ authStore.user.username }}</span>
            </div>
            <UButton
              color="red"
              variant="outline"
              icon="i-heroicons-arrow-right-on-rectangle"
              @click="handleLogout"
            >
              Logout
            </UButton>
          </div>
        </div>
      </div>
    </header>

    <!-- Main Content -->
    <main class="flex-1 w-full max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <slot />
    </main>

    <!-- Footer with Version -->
    <footer class="bg-white border-t border-gray-200 w-full">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
        <div class="flex justify-between items-center">
          <div class="text-sm text-gray-500">
            &copy; {{ new Date().getFullYear() }} MonitraNG. All rights reserved.
          </div>
          <AppVersion />
        </div>
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { useAuthStore } from '~/stores/auth'
import AppVersion from '~/components/AppVersion.vue'

const authStore = useAuthStore()
const router = useRouter()

const handleLogout = async () => {
  try {
    // Call logout endpoint (optional, for token revocation)
    await $fetch('/api/auth/logout', { method: 'POST' }).catch(() => {
      // Ignore errors if logout endpoint fails
    })
  } catch (error) {
    console.error('Logout error:', error)
  } finally {
    // Clear auth store
    authStore.clearAuth()
    
    // Redirect to login
    await router.push('/login')
  }
}
</script>

