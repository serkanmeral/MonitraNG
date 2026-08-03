import { computed, ref } from 'vue'
import type { AgentStatus } from '~/composables/useAgentApi'
import { useAgentApi } from '~/composables/useAgentApi'

const platform = ref<'windows' | 'linux' | 'unknown'>('unknown')
let loaded = false

export function useAgentPlatform() {
  const { getStatus } = useAgentApi()

  const isLinux = computed(() => platform.value === 'linux')
  const isWindows = computed(() => platform.value === 'windows' || platform.value === 'unknown')

  async function ensurePlatform() {
    if (loaded) return platform.value
    try {
      const s = await getStatus()
      applyFromStatus(s)
    } catch {
      platform.value = 'unknown'
    }
    loaded = true
    return platform.value
  }

  function applyFromStatus(s: AgentStatus | null | undefined) {
    const p = (s?.platform || '').toLowerCase()
    if (p === 'linux') platform.value = 'linux'
    else if (p === 'windows') platform.value = 'windows'
    else if (s) platform.value = 'windows' // legacy Windows agent has no platform field
  }

  const logSourceLabel = computed(() => (isLinux.value ? 'Journal' : 'Olay günlüğü'))

  return {
    platform,
    isLinux,
    isWindows,
    logSourceLabel,
    ensurePlatform,
    applyFromStatus
  }
}
