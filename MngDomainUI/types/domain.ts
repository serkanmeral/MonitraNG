export enum DomainStatus {
  Pending = 'Pending',
  Active = 'Active',
  Suspended = 'Suspended',
  Expired = 'Expired',
  Deleted = 'Deleted',
  Failed = 'Failed'
}

export interface DomainSettings {
  maxUsers?: number
  maxAssets?: number
  enableMqtt?: boolean
  mqttSettings?: {
    brokerHost?: string
    brokerPort?: number
    username?: string
    password?: string
    topicPrefix?: string
  }
  customSettings?: Record<string, any>
}

export interface Domain {
  id: string
  name: string
  displayName: string
  databaseName: string
  realmName: string
  storageBucket: string
  storageQuota: number
  storageUsed: number
  status: DomainStatus
  settings: DomainSettings
  createdAt: string
  expiresAt?: string
  createdBy: string
  updatedAt?: string
  updatedBy?: string
  relatedPersonPhone?: string
  logo?: string
  logoUrl?: string
}

export interface CreateDomainRequest {
  domainName: string
  displayName: string
  adminEmail: string
  adminPassword: string
  settings?: DomainSettings
  relatedPersonPhone?: string
  logo?: string
  logoUrl?: string
}

export interface CreateDomainResponse {
  domainId: string
  domainName: string
  databaseName: string
  adminUsername: string
  adminEmail: string
  createdAt: string
  isSuccess: boolean
  errorMessage?: string
  message?: string
  failedStep?: string
}

