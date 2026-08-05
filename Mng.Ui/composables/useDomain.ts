import { fetchFromMngKeeper } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';

export interface DirectoryPrivilegeSettings {
  adminGroupNames?: string[];
  managerGroupNames?: string[];
}

export interface UpdateDirectoryPrivilegeSettingsRequest {
  adminGroupNames?: string[];
  managerGroupNames?: string[];
}

export interface DirectoryLdapSettings {
  enabled?: boolean;
  host?: string;
  port?: number;
  useSsl?: boolean;
  baseDn?: string;
  bindUsername?: string;
  bindPassword?: string;
}

export interface Domain {
  id: string;
  name: string;
  displayName: string;
  discoveryRootLabel?: string | null;
  databaseName?: string;
  realmName?: string;
  storageBucket?: string;
  storageQuota?: number;
  storageUsed?: number;
  status?: string;
  settings?: {
    maxUsers?: number;
    maxAssets?: number;
    enableMqtt?: boolean;
    mqttSettings?: any;
    customSettings?: Record<string, any>;
    directoryPrivileges?: UpdateDirectoryPrivilegeSettingsRequest | null;
    directoryLdap?: DirectoryLdapSettings | null;
  };
  createdAt?: string;
  expiresAt?: string;
  createdBy?: string;
  updatedAt?: string;
  updatedBy?: string;
  relatedPersonPhone?: string;
  logo?: string;
  logoUrl?: string;
  [key: string]: any;
}

export interface UpdateDomainRequest {
  displayName?: string;
  discoveryRootLabel?: string;
  relatedPersonPhone?: string;
  logo?: string; // Base64 encoded image
  logoUrl?: string;
  settings?: {
    maxUsers?: number;
    maxAssets?: number;
    enableMqtt?: boolean;
    mqttSettings?: any;
    customSettings?: Record<string, any>;
    directoryPrivileges?: DirectoryPrivilegeSettings | null;
    directoryLdap?: DirectoryLdapSettings | null;
  };
}

export const useDomain = () => {
  const authStore = useAuthStore();
  
  /**
   * Get domain by ID
   */
  const getDomainById = async (id: string): Promise<Domain | null> => {
    try {
      const domain = await fetchFromMngKeeper(`domain/${id}`);
      return domain as Domain;
    } catch (error) {
      console.error('Error fetching domain by ID:', error);
      return null;
    }
  };

  /**
   * Get domain by name
   */
  const getDomainByName = async (name: string): Promise<Domain | null> => {
    try {
      const domain = await fetchFromMngKeeper(`domain/name/${name}`);
      return domain as Domain;
    } catch (error) {
      console.error('Error fetching domain by name:', error);
      return null;
    }
  };

  /**
   * Get current user's domain
   * Uses domain_name or domain_id from token
   */
  const getCurrentDomain = async (): Promise<Domain | null> => {
    if (!authStore.userInfo) {
      return null;
    }

    const domainName = authStore.userInfo.domain_name;
    const domainId = authStore.userInfo.domain_id;

    if (domainName) {
      return await getDomainByName(domainName);
    } else if (domainId) {
      return await getDomainById(domainId);
    }

    return null;
  };

  /**
   * Update domain information
   */
  const updateDomain = async (id: string, domainData: UpdateDomainRequest): Promise<Domain | null> => {
    try {
      const updatedDomain = await fetchFromMngKeeper(`domain/${id}`, 'PUT', domainData);
      return updatedDomain as Domain;
    } catch (error) {
      console.error('Error updating domain:', error);
      throw error;
    }
  };

  return {
    getDomainById,
    getDomainByName,
    getCurrentDomain,
    updateDomain,
  };
};
