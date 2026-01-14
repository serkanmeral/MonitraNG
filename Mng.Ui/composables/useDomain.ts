import { fetchFromMngKeeper } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';

export interface Domain {
  id: string;
  name: string;
  displayName: string;
  logo?: string;
  logoUrl?: string;
  [key: string]: any;
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

  return {
    getDomainById,
    getDomainByName,
    getCurrentDomain,
  };
};
