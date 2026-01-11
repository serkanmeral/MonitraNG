/**
 * Composable for application version information
 */
export const useVersion = () => {
  const config = useRuntimeConfig()
  
  // Get version from runtime config (from nuxt.config.ts or environment variable)
  const appVersion = config.public.appVersion || '1.0.0'
  
  // Optionally fetch from version.json (for dynamic version updates)
  const versionInfo = ref<{ version: string; buildDate?: string } | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  
  /**
   * Fetch version information from version.json
   * This is optional - version can also come from runtime config
   */
  const fetchVersionInfo = async () => {
    if (versionInfo.value) return versionInfo.value // Already fetched
    
    // Only fetch on client-side
    if (process.server) {
      versionInfo.value = { version: appVersion }
      return versionInfo.value
    }
    
    loading.value = true
    error.value = null
    
    try {
      const response = await $fetch<{ version: string; buildDate?: string }>('/version.json')
      versionInfo.value = response
      return response
    } catch (err: any) {
      error.value = err.message || 'Failed to fetch version info'
      console.warn('Failed to fetch version.json, using config version:', err)
      // Fallback to config version
      versionInfo.value = { version: appVersion }
      return versionInfo.value
    } finally {
      loading.value = false
    }
  }
  
  /**
   * Get current version (from config or fetched info)
   */
  const getVersion = computed(() => {
    return versionInfo.value?.version || appVersion
  })
  
  /**
   * Get build date if available
   */
  const getBuildDate = computed(() => {
    return versionInfo.value?.buildDate || null
  })
  
  /**
   * Format version for display
   */
  const getDisplayVersion = computed(() => {
    const version = getVersion.value
    const buildDate = getBuildDate.value
    
    if (buildDate) {
      const date = new Date(buildDate)
      return `v${version} (${date.toLocaleDateString('tr-TR')})`
    }
    
    return `v${version}`
  })
  
  return {
    version: getVersion,
    buildDate: getBuildDate,
    displayVersion: getDisplayVersion,
    fetchVersionInfo,
    loading,
    error
  }
}
