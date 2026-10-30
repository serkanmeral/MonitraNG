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

export interface DomainPasswordSettings {
  mode?: string;
  personnelDn?: string;
  resetBindUsername?: string;
  resetBindPassword?: string;
  serverCertificateThumbprint?: string;
  resetBindPasswordConfigured?: boolean;
}

export interface DomainMailSettings {
  enabled?: boolean;
  host?: string;
  port?: number;
  username?: string;
  password?: string;
  enableSsl?: boolean;
  secureSocketMode?: string;
  fromEmail?: string;
  fromName?: string | null;
  passwordConfigured?: boolean;
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
    maxFileSizeBytes?: number;
    enableMqtt?: boolean;
    mqttSettings?: any;
    customSettings?: Record<string, any>;
    directoryPrivileges?: UpdateDirectoryPrivilegeSettingsRequest | null;
    directoryLdap?: DirectoryLdapSettings | null;
    password?: DomainPasswordSettings | null;
    mail?: DomainMailSettings | null;
  };
  createdAt?: string;
  expiresAt?: string;
  createdBy?: string;
  updatedAt?: string;
  updatedBy?: string;
  relatedPersonPhone?: string;
  /** Legacy single logo. Treated as the dark-theme logo when logoDark is empty. */
  logo?: string;
  logoDark?: string;
  logoLight?: string;
  logoUrl?: string;
  [key: string]: any;
}

/** Uploaded file (`logo`) wins over `logoUrl` so Domain Yönetimi and the sidebar stay in sync. */
export function resolveDomainLogoSrc(
  domain?: Pick<Domain, 'logo' | 'logoUrl' | 'updatedAt'> | null
): string {
  if (!domain) return '';

  const logo = domain.logo?.trim();
  if (logo) {
    if (
      logo.startsWith('data:') ||
      logo.startsWith('http://') ||
      logo.startsWith('https://') ||
      logo.startsWith('/')
    ) {
      return logo;
    }

    let mimeType = 'image/png';
    if (logo.startsWith('/9j/')) mimeType = 'image/jpeg';
    else if (logo.startsWith('iVBORw0KGgo')) mimeType = 'image/png';
    else if (logo.startsWith('R0lGODlh')) mimeType = 'image/gif';
    else if (logo.startsWith('UklGR')) mimeType = 'image/webp';

    return `data:${mimeType};base64,${logo}`;
  }

  const url = domain.logoUrl?.trim();
  if (!url) return '';
  if (!url.startsWith('http://') && !url.startsWith('https://')) return url;

  const version = domain.updatedAt ? Date.parse(domain.updatedAt) : Date.now();
  const stamp = Number.isFinite(version) ? String(version) : String(Date.now());
  return url.includes('?') ? `${url}&v=${stamp}` : `${url}?v=${stamp}`;
}

export function isDarkAppTheme(themeName?: string | null): boolean {
  return !!themeName?.startsWith('DARK_');
}

/** Dark theme uses logoDark (or the legacy logo). Light theme uses logoLight. */
export function resolveThemedDomainLogoSrc(
  domain?: Pick<Domain, 'logo' | 'logoDark' | 'logoLight' | 'logoUrl' | 'updatedAt'> | null,
  themeName?: string | null
): string {
  const darkSrc = resolveDomainLogoSrc({
    logo: domain?.logoDark || domain?.logo,
    logoUrl: domain?.logoUrl,
    updatedAt: domain?.updatedAt,
  });
  const lightSrc = resolveDomainLogoSrc({
    logo: domain?.logoLight,
    updatedAt: domain?.updatedAt,
  });

  if (isDarkAppTheme(themeName)) return darkSrc || lightSrc;
  return lightSrc || darkSrc;
}

export interface UpdateDomainRequest {
  displayName?: string;
  discoveryRootLabel?: string;
  relatedPersonPhone?: string;
  logo?: string; // Kept equal to logoDark for older readers
  logoDark?: string;
  logoLight?: string;
  logoUrl?: string;
  settings?: {
    maxUsers?: number;
    maxAssets?: number;
    maxFileSizeBytes?: number;
    enableMqtt?: boolean;
    mqttSettings?: any;
    customSettings?: Record<string, any>;
    directoryPrivileges?: DirectoryPrivilegeSettings | null;
    directoryLdap?: DirectoryLdapSettings | null;
    password?: DomainPasswordSettings | null;
    mail?: DomainMailSettings | null;
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
