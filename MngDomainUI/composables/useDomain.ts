import type { Domain, CreateDomainRequest } from '~/types/domain'

export const useDomain = () => {
  const getAllDomains = async (status?: string): Promise<Domain[]> => {
    // Use server-side API route to bypass SSL issues
    const query = status ? `?status=${status}` : ''
    return $fetch<Domain[]>(`/api/keeper/domain${query}`)
  }

  const getDomainById = async (id: string): Promise<Domain> => {
    return $fetch<Domain>(`/api/keeper/domain/${id}`)
  }

  const getDomainByName = async (name: string): Promise<Domain> => {
    return $fetch<Domain>(`/api/keeper/domain/name/${name}`)
  }

  const createDomain = async (domainData: CreateDomainRequest) => {
    // Use server-side API route to bypass SSL issues
    return $fetch('/api/keeper/domain', {
      method: 'POST',
      body: domainData,
    })
  }

  const updateDomain = async (id: string, domainData: Partial<Domain>) => {
    return $fetch(`/api/keeper/domain/${id}`, {
      method: 'PUT',
      body: domainData,
    })
  }

  const deleteDomain = async (id: string) => {
    return $fetch(`/api/keeper/domain/${id}`, {
      method: 'DELETE',
    })
  }

  const clearAllDomains = async () => {
    return $fetch('/api/clear-all-domains', {
      method: 'POST',
    })
  }

  return {
    getAllDomains,
    getDomainById,
    getDomainByName,
    createDomain,
    updateDomain,
    deleteDomain,
    clearAllDomains,
  }
}

