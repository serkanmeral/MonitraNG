import { useAppI18n } from '@/composables/useAppI18n';
import { useAppToast } from '@/composables/useAppToast';
import {
  type DgErrorNotifyOptions,
  type DgErrorNotifyResult,
  useDgErrorNotify,
} from '@/composables/useDgErrorNotify';
import { diExtractMessage } from '@/services/documentIntelligenceService';
import { ocExtractOperationsMessage } from '@/services/operationCoreService';
import {
  dgExtractMessage,
  dgExtractValidationErrors,
  dgIsDuplicateError,
  dgIsValidationError,
  dgResolveErrorPayload,
  dgValidationErrorMap,
} from '@/utils/dgErrorUtils';

export type ApiErrorNotifyOptions = DgErrorNotifyOptions;

export type ApiErrorNotifyResult = DgErrorNotifyResult;

function asErrorDataRecord(error: unknown): Record<string, unknown> | null {
  if (!error || typeof error !== 'object') return null;
  const root = error as Record<string, unknown>;
  const data = root.data;
  if (data && typeof data === 'object') return data as Record<string, unknown>;
  return root;
}

/** MngOperations: { code, message, messageTr? } — DG'nin { error: { ... } } sarmalayıcısı yok. */
function isLikelyOperationsError(error: unknown): boolean {
  const data = asErrorDataRecord(error);
  if (!data) return false;
  const nested = data.data && typeof data.data === 'object' ? (data.data as Record<string, unknown>) : null;
  const candidates = [data, nested].filter(Boolean) as Record<string, unknown>[];
  for (const d of candidates) {
    if (d.error && typeof d.error === 'object') continue;
    const code = d.code;
    if (typeof code !== 'string' || !code.trim()) continue;
    if (typeof d.messageTr === 'string' && d.messageTr.trim()) return true;
    if (typeof d.message === 'string' && d.message.trim()) return true;
  }
  return false;
}

function isLikelyDocumentError(error: unknown): boolean {
  if (isLikelyOperationsError(error)) return false;
  if (!(error instanceof Error)) return false;
  const data = (error as { data?: unknown }).data;
  if (!data || typeof data !== 'object') return false;
  const d = data as Record<string, unknown>;
  return typeof d.messageTr === 'string'
    || typeof d.code === 'string' && !d.error && typeof d.message === 'string';
}

/** Unified message extraction: DG → MngOperations → MngDocument → generic. */
export function extractApiErrorMessage(error: unknown, fallback: string): string {
  if (dgResolveErrorPayload(error) || dgExtractValidationErrors(error).length > 0) {
    return dgExtractMessage(error, fallback);
  }

  if (isLikelyOperationsError(error)) {
    return ocExtractOperationsMessage(error, fallback);
  }

  if (isLikelyDocumentError(error)) {
    return diExtractMessage(error, fallback);
  }

  const moMessage = ocExtractOperationsMessage(error, fallback);
  if (moMessage && moMessage !== fallback) return moMessage;

  return diExtractMessage(error, fallback);
}

function resolveApiSeverity(error: unknown): 'error' | 'warning' {
  if (dgIsValidationError(error) || dgIsDuplicateError(error)) return 'warning';
  return 'error';
}

export function useApiErrorNotify() {
  const { t } = useAppI18n();
  const { push } = useAppToast();
  const { notifyDgError } = useDgErrorNotify();

  function notifyApiError(error: unknown, options: ApiErrorNotifyOptions = {}): ApiErrorNotifyResult {
    if (dgResolveErrorPayload(error) || dgExtractValidationErrors(error).length > 0) {
      return notifyDgError(error, options);
    }

    const fallback = t(options.fallbackKey ?? 'errors.dg.generic');
    const message = extractApiErrorMessage(error, fallback);

    if (options.toast !== false) {
      push({
        title: options.title ?? t('errors.dg.toastTitle'),
        message,
        severity: options.severity ?? resolveApiSeverity(error),
      });
    }

    return {
      message,
      fieldErrors: dgValidationErrorMap(error),
    };
  }

  return {
    notifyApiError,
    notifyDgError,
    extractApiErrorMessage,
  };
}

/** List/panel sayfaları — yalnızca toast. */
export function usePanelErrorNotify(defaultFallbackKey = 'errors.dg.generic') {
  const { notifyApiError } = useApiErrorNotify();
  const { t } = useAppI18n();

  return (error: unknown, fallbackKey?: string, title?: string) =>
    notifyApiError(error, {
      fallbackKey: fallbackKey ?? defaultFallbackKey,
      title: title ?? t('errors.dg.toastTitle'),
    }).message;
}
