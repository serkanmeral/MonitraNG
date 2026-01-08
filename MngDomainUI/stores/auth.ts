import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

interface User {
  username: string
  email?: string
  roles?: string[]
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(null)
  const refreshToken = ref<string | null>(null)
  const user = ref<User | null>(null)
  const expiresAt = ref<number | null>(null)

  const isAuthenticated = computed(() => !!token.value && !isTokenExpired.value)

  const isTokenExpired = computed(() => {
    if (!expiresAt.value) return false
    return Date.now() >= expiresAt.value * 1000
  })

  // Load from localStorage on initialization
  if (process.client) {
    const storedToken = localStorage.getItem('auth_token')
    const storedRefreshToken = localStorage.getItem('auth_refresh_token')
    const storedUser = localStorage.getItem('auth_user')
    const storedExpiresAt = localStorage.getItem('auth_expires_at')

    if (storedToken) token.value = storedToken
    if (storedRefreshToken) refreshToken.value = storedRefreshToken
    if (storedUser) user.value = JSON.parse(storedUser)
    if (storedExpiresAt) expiresAt.value = parseInt(storedExpiresAt, 10)
  }

  function setAuth(data: {
    token: string
    refreshToken?: string
    user: User
    expiresIn?: number
  }) {
    token.value = data.token
    if (data.refreshToken) refreshToken.value = data.refreshToken
    user.value = data.user

    // Calculate expiration time
    if (data.expiresIn) {
      expiresAt.value = Math.floor(Date.now() / 1000) + data.expiresIn
    }

    // Persist to localStorage
    if (process.client) {
      localStorage.setItem('auth_token', data.token)
      if (data.refreshToken) {
        localStorage.setItem('auth_refresh_token', data.refreshToken)
      }
      localStorage.setItem('auth_user', JSON.stringify(data.user))
      if (expiresAt.value) {
        localStorage.setItem('auth_expires_at', expiresAt.value.toString())
      }
    }
  }

  function clearAuth() {
    token.value = null
    refreshToken.value = null
    user.value = null
    expiresAt.value = null

    // Clear localStorage
    if (process.client) {
      localStorage.removeItem('auth_token')
      localStorage.removeItem('auth_refresh_token')
      localStorage.removeItem('auth_user')
      localStorage.removeItem('auth_expires_at')
    }
  }

  function getAuthHeaders() {
    if (!token.value) return {}
    return {
      Authorization: `Bearer ${token.value}`
    }
  }

  return {
    token,
    refreshToken,
    user,
    expiresAt,
    isAuthenticated,
    isTokenExpired,
    setAuth,
    clearAuth,
    getAuthHeaders
  }
})

