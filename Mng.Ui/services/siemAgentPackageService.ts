import { getAccessToken } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';

export interface AgentPackageDto {
  id: string;
  platform: string;
  fileName: string;
  version: string;
  sha256: string;
  sizeBytes: number;
  downloadPath: string;
  downloadUrl: string;
}

export interface AgentPackageCatalog {
  collectorBaseUrl: string;
  packages: AgentPackageDto[];
}

async function authHeaders(): Promise<Record<string, string>> {
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // BFF still needs the session cookie.
  }

  const headers: Record<string, string> = { Accept: 'application/json' };
  if (authStore.domainName) {
    headers['X-Domain-Name'] = authStore.domainName;
  }
  const token = getAccessToken();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

export async function fetchAgentPackages(): Promise<AgentPackageCatalog> {
  const raw = await $fetch<Record<string, unknown>>('/api/logcollector/v1/agent/packages', {
    headers: await authHeaders(),
  });

  const packagesRaw = (raw.packages ?? raw.Packages ?? []) as Record<string, unknown>[];
  const collectorBaseUrl = String(raw.collectorBaseUrl ?? raw.CollectorBaseUrl ?? '').replace(/\/$/, '');

  const packages = Array.isArray(packagesRaw)
    ? packagesRaw.map((p) => {
        const id = String(p.id ?? p.Id ?? '').toLowerCase();
        const downloadPath = String(p.downloadPath ?? p.DownloadPath ?? `/api/v1/agent/packages/${id}`);
        const downloadUrl = String(p.downloadUrl ?? p.DownloadUrl ?? '').trim()
          || (collectorBaseUrl ? `${collectorBaseUrl}${downloadPath}` : downloadPath);
        return {
          id,
          platform: String(p.platform ?? p.Platform ?? id),
          fileName: String(p.fileName ?? p.FileName ?? ''),
          version: String(p.version ?? p.Version ?? ''),
          sha256: String(p.sha256 ?? p.Sha256 ?? ''),
          sizeBytes: Number(p.sizeBytes ?? p.SizeBytes ?? 0),
          downloadPath,
          downloadUrl,
        } satisfies AgentPackageDto;
      }).filter((p) => p.id === 'windows' || p.id === 'linux')
    : [];

  return { collectorBaseUrl, packages };
}
