export interface Template {
  id: string
  name: string
  description?: string
  sourceDomainId: string
  sourceDatabaseName: string
  collections: SelectedCollection[]
  totalDocumentCount: number
  createdAt: string
  createdBy: string
  updatedAt?: string
  updatedBy?: string
}

export interface SelectedCollection {
  collectionName: string
  includeIndexes: boolean
  documentCount: number
}

export interface CreateTemplateRequest {
  name: string
  description?: string
  sourceDomainId: string
  collections: SelectedCollectionDto[]
}

export interface SelectedCollectionDto {
  collectionName: string
  includeIndexes: boolean
}

export interface UpdateTemplateRequest {
  description?: string
  collections: SelectedCollectionDto[]
}

export interface CollectionInfo {
  name: string
  documentCount: number
  hasIndexes: boolean
}

export const useTemplate = () => {
  const getAllTemplates = async (): Promise<Template[]> => {
    return $fetch<Template[]>('/api/keeper/templates')
  }

  const getTemplate = async (name: string): Promise<Template> => {
    return $fetch<Template>(`/api/keeper/templates/${name}`)
  }

  const getTemplatesByDomain = async (domainId: string): Promise<Template[]> => {
    return $fetch<Template[]>(`/api/keeper/templates/domain/${domainId}`)
  }

  const createTemplate = async (templateData: CreateTemplateRequest): Promise<Template> => {
    return $fetch<Template>('/api/keeper/templates', {
      method: 'POST',
      body: templateData,
    })
  }

  const updateTemplate = async (name: string, templateData: UpdateTemplateRequest): Promise<Template> => {
    return $fetch<Template>(`/api/keeper/templates/${name}`, {
      method: 'PUT',
      body: templateData,
    })
  }

  const deleteTemplate = async (name: string): Promise<void> => {
    await $fetch(`/api/keeper/templates/${name}`, {
      method: 'DELETE',
    })
  }

  const getTemplateContent = async (name: string): Promise<any> => {
    return $fetch(`/api/keeper/templates/${name}/content`)
  }

  // Get collections from domain database
  const getDomainCollections = async (domainId: string): Promise<CollectionInfo[]> => {
    // Get collections via Keeper API
    return $fetch<CollectionInfo[]>(`/api/keeper/domain/${domainId}/collections`)
  }

  return {
    getAllTemplates,
    getTemplate,
    getTemplatesByDomain,
    createTemplate,
    updateTemplate,
    deleteTemplate,
    getTemplateContent,
    getDomainCollections,
  }
}
