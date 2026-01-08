<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50 px-4">
    <div class="max-w-md w-full space-y-8">
      <div class="text-center">
        <h1 class="text-3xl font-bold text-gray-900">Mng Domain Management</h1>
        <p class="mt-2 text-sm text-gray-600">Please sign in to continue</p>
      </div>

      <UCard>
        <template #header>
          <h2 class="text-xl font-semibold">Sign In</h2>
        </template>

        <UAlert
          v-if="error"
          color="red"
          variant="soft"
          :title="error"
          class="mb-4"
          @close="error = null"
        />

        <form @submit.prevent="handleLogin" class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Username</label>
            <UInput
              v-model="form.username"
              type="text"
              placeholder="admin"
              :disabled="loading"
              required
            />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Password</label>
            <UInput
              v-model="form.password"
              type="password"
              placeholder="Enter your password"
              :disabled="loading"
              required
            />
          </div>

          <UButton
            type="submit"
            color="primary"
            block
            :loading="loading"
            :disabled="!form.username || !form.password"
          >
            Sign In
          </UButton>
        </form>

        <div class="mt-4 pt-4 border-t border-gray-200">
          <p class="text-xs text-gray-500 text-center">
            Use Keycloak admin credentials to sign in
          </p>
        </div>
      </UCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useAuthStore } from '~/stores/auth'

definePageMeta({
  layout: false,
  middleware: 'guest'
})

const authStore = useAuthStore()
const router = useRouter()

// Only pre-fill credentials in development mode
const isDev = process.dev
const form = ref({
  username: isDev ? 'admin' : '',
  password: isDev ? 'admin123' : ''
})

const loading = ref(false)
const error = ref<string | null>(null)

const handleLogin = async () => {
  loading.value = true
  error.value = null

  try {
    const response = await $fetch('/api/auth/login', {
      method: 'POST',
      body: {
        username: form.value.username,
        password: form.value.password
      }
    }) as any

    if (response.success) {
      // Set auth in store
      authStore.setAuth({
        token: response.accessToken,
        refreshToken: response.refreshToken,
        user: response.user || { username: form.value.username },
        expiresIn: response.expiresIn
      })

      // Redirect to domains page
      await router.push('/domains')
    } else {
      error.value = 'Login failed. Please check your credentials.'
    }
  } catch (err: any) {
    error.value = err.data?.message || err.message || 'Login failed. Please check your credentials.'
    console.error('Login error:', err)
  } finally {
    loading.value = false
  }
}
</script>

