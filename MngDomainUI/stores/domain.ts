import { defineStore } from 'pinia'
import type { Domain } from '~/types/domain'

export const useDomainStore = defineStore('domain', {
  state: () => ({
    domains: [] as Domain[],
    currentDomain: null as Domain | null,
    loading: false,
    error: null as string | null,
  }),

  getters: {
    activeDomains: (state) => state.domains.filter(d => d.status === 'Active'),
    pendingDomains: (state) => state.domains.filter(d => d.status === 'Pending'),
  },

  actions: {
    async fetchDomains(status?: string) {
      this.loading = true
      this.error = null
      try {
        const { getAllDomains } = useDomain()
        this.domains = await getAllDomains(status)
      } catch (error: any) {
        this.error = error.message || 'Failed to fetch domains'
        throw error
      } finally {
        this.loading = false
      }
    },

    async fetchDomainById(id: string) {
      this.loading = true
      this.error = null
      try {
        const { getDomainById } = useDomain()
        this.currentDomain = await getDomainById(id)
        return this.currentDomain
      } catch (error: any) {
        this.error = error.message || 'Failed to fetch domain'
        throw error
      } finally {
        this.loading = false
      }
    },

    setCurrentDomain(domain: Domain | null) {
      this.currentDomain = domain
    },

    clearError() {
      this.error = null
    },
  },
})

