import { computed } from 'vue';
import { useAuthStore } from '@/stores/auth';

/** Etki alanı kaydı boşsa DataGateway varsayılanı. */
export const DOMAIN_FILE_LIMIT_FALLBACK_BYTES = 5 * 1024 * 1024;

export function domainFileLimitBytes(settings?: { maxFileSizeBytes?: number | null } | null): number {
  const value = Number(settings?.maxFileSizeBytes);
  return Number.isFinite(value) && value > 0 ? value : DOMAIN_FILE_LIMIT_FALLBACK_BYTES;
}

export function useDomainFileLimit() {
  const authStore = useAuthStore();
  const maxBytes = computed(() => domainFileLimitBytes(authStore.domainInfo?.settings));
  const maxMb = computed(() => Math.max(1, Math.floor(maxBytes.value / (1024 * 1024))));
  return { maxBytes, maxMb };
}
