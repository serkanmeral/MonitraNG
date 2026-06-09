import { toValue, type MaybeRefOrGetter } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocExtractDgErrorMessage, ocReloadWorkspaceMetadataCache } from '@/services/operationCoreService';

/**
 * Alan/form/board kaydı sonrası MO metadata önbelleğini düşürür (fail-soft).
 * Manuel «Runtime önbelleğini yenile» ile aynı uç.
 */
export function useOcWorkspaceMetadataCacheReload(workspaceId: MaybeRefOrGetter<string | undefined>) {
  const { t } = useAppI18n();

  async function reloadAfterMetadataChange(): Promise<number | null> {
    const wsId = toValue(workspaceId)?.trim();
    if (!wsId) return null;
    try {
      const result = await ocReloadWorkspaceMetadataCache(wsId);
      return result.keysRemoved;
    } catch {
      return null;
    }
  }

  /** Kayıt başarı mesajına otomatik cache reload sonucunu ekler. */
  function formatSaveSuccess(baseMessage: string, keysRemoved: number | null): string {
    if (keysRemoved === null) {
      return `${baseMessage} ${t('operationCore.workspaceDefinitions.metadataCacheAutoReloadFailed')}`;
    }
    return `${baseMessage} ${t('operationCore.workspaceDefinitions.metadataCacheAutoReloadOk', {
      count: keysRemoved,
    })}`;
  }

  async function applySaveSuccess(
    setSuccess: (message: string) => void,
    baseMessage: string
  ): Promise<void> {
    const keysRemoved = await reloadAfterMetadataChange();
    setSuccess(formatSaveSuccess(baseMessage, keysRemoved));
  }

  return {
    reloadAfterMetadataChange,
    formatSaveSuccess,
    applySaveSuccess,
  };
}

/** Tek seferlik reload (workspace id string). */
export async function ocTryReloadWorkspaceMetadataCache(workspaceId: string): Promise<number | null> {
  const wsId = workspaceId?.trim();
  if (!wsId) return null;
  try {
    const result = await ocReloadWorkspaceMetadataCache(wsId);
    return result.keysRemoved;
  } catch (e: unknown) {
    void ocExtractDgErrorMessage(e, '');
    return null;
  }
}
