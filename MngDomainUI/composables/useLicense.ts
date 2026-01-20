export interface LicenseInfo {
  domainName: string
  licenseType: 'Trial' | 'Real' | 0 | 1 // Backend returns 0 (Trial) or 1 (Real)
  isValid: boolean
  isExpired: boolean
  expiresAt: string
  issuedAt: string
  issuedBy: string
  expirationBehavior?: {
    blockTokenGeneration: boolean
    blockCrudOperations: boolean
    blockGetOperations: boolean
    allowReadOnly: boolean
    customMessage?: string
  }
  licenseFeatures?: {
    maxUsers: number
    maxDomains: number
    maxStorageGB: number
    enableAdvancedFeatures: boolean
    supportLevel?: string
    countActiveUsersOnly: boolean
  }
  customerInfo?: {
    customerName: string
    customerId: string
    contactEmail: string
    contactPhone?: string
  }
}

// Helper function to normalize license type
export const getLicenseType = (licenseType: 'Trial' | 'Real' | 0 | 1): 'Trial' | 'Real' => {
  if (licenseType === 'Trial' || licenseType === 0) return 'Trial'
  if (licenseType === 'Real' || licenseType === 1) return 'Real'
  return 'Trial' // Default fallback
}

export interface UserCountInfo {
  domainName: string
  activeUserCount: number
  maxUsers?: number
  canCreateUser: boolean
}

export const useLicense = (accessToken?: string | null) => {
  // Helper to get headers with token if available
  const getHeaders = () => {
    const headers: Record<string, string> = {}
    if (accessToken) {
      headers['Authorization'] = `Bearer ${accessToken}`
    }
    return headers
  }

  const getLicense = async (domainName: string): Promise<LicenseInfo> => {
    return $fetch<LicenseInfo>(`/api/keeper/license/${domainName}`, {
      headers: getHeaders()
    })
  }

  const uploadLicense = async (domainName: string, file: File): Promise<void> => {
    const formData = new FormData()
    formData.append('domainName', domainName)
    formData.append('licenseFile', file)

    return $fetch<void>('/api/keeper/license/upload', {
      method: 'POST',
      body: formData,
      headers: getHeaders()
    })
  }

  const validateLicense = async (domainName: string): Promise<any> => {
    return $fetch<any>('/api/keeper/license/validate', {
      method: 'POST',
      body: { domainName },
      headers: getHeaders()
    })
  }

  const downloadLicense = async (domainName: string, type: 'trial' | 'real' = 'real') => {
    const headers: HeadersInit = {}
    if (accessToken) {
      headers['Authorization'] = `Bearer ${accessToken}`
    }
    
    const response = await fetch(`/api/keeper/license/${domainName}/download?type=${type}`, {
      headers
    })
    const blob = await response.blob()
    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `license-${type}-${domainName}.enc`
    document.body.appendChild(a)
    a.click()
    window.URL.revokeObjectURL(url)
    document.body.removeChild(a)
  }

  const getUserCount = async (domainName: string): Promise<UserCountInfo> => {
    return $fetch<UserCountInfo>(`/api/keeper/license/${domainName}/user-count`, {
      headers: getHeaders()
    })
  }

  const createRealLicense = async (
    domainName: string,
    request: CreateRealLicenseRequest
  ): Promise<void> => {
    return $fetch<void>(`/api/keeper/license/${domainName}/create-real`, {
      method: 'POST',
      body: request,
      headers: getHeaders()
    })
  }

  return {
    getLicense,
    uploadLicense,
    validateLicense,
    downloadLicense,
    getUserCount,
    createRealLicense,
  }
}

export interface CreateRealLicenseRequest {
  expiresAt: string // ISO 8601 date string
  expirationBehavior: {
    blockTokenGeneration: boolean
    blockCrudOperations: boolean
    blockGetOperations: boolean
    allowReadOnly: boolean
    customMessage?: string
  }
  licenseFeatures: {
    maxUsers: number
    maxDomains: number
    maxStorageGB: number
    enableAdvancedFeatures: boolean
    supportLevel?: string
    countActiveUsersOnly: boolean
    activeUserDefinition?: {
      isActive: boolean
      lastLoginDays?: number
    }
  }
  customerInfo?: {
    customerName: string
    customerId: string
    contactEmail: string
    contactPhone?: string
  }
  metadata?: {
    purchaseDate?: string
    invoiceNumber?: string
    salesRep?: string
  }
}
