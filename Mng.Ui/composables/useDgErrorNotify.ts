import { useAppI18n } from '@/composables/useAppI18n';
import { useAppToast } from '@/composables/useAppToast';
import { useDgErrorTranslation } from '@/composables/useDgErrorTranslation';
import {
  dgErrorCode,
  dgExtractValidationErrors,
  dgIsDuplicateError,
  dgIsValidationError,
  type DgValidationError,
} from '@/utils/dgErrorUtils';

export interface DgErrorNotifyOptions {
  /** Toast title; defaults to generic error title i18n key. */
  title?: string;
  /** Fallback message when code/message cannot be resolved. */
  fallbackKey?: string;
  /** Maps DG field names to display labels for toast + inline errors. */
  fieldLabelResolver?: (field: string) => string;
  /** Show global toast (default: true). */
  toast?: boolean;
  /** Override toast severity; auto-detected when omitted. */
  severity?: 'error' | 'warning' | 'info';
}

export interface DgErrorNotifyResult {
  message: string;
  fieldErrors: Record<string, string[]>;
}

function resolveToastSeverity(error: unknown): 'error' | 'warning' {
  const code = dgErrorCode(error);
  if (
    code === 'FORBIDDEN'
    || code === 'UNAUTHORIZED'
    || code === 'INTERNAL_ERROR'
    || code === 'DATABASE_ERROR'
  ) {
    return 'error';
  }
  if (dgIsValidationError(error) || dgIsDuplicateError(error)) {
    return 'warning';
  }
  return 'error';
}

function buildFieldErrors(
  error: unknown,
  translateFieldError: (item: DgValidationError, fieldLabel?: string) => string,
  fieldLabelResolver?: (field: string) => string,
): Record<string, string[]> {
  const map: Record<string, string[]> = {};
  for (const item of dgExtractValidationErrors(error)) {
    const field = item.field || '_form';
    const label = item.field ? fieldLabelResolver?.(item.field) : undefined;
    const message = translateFieldError(item, label);
    if (!map[field]) map[field] = [];
    map[field].push(message);
  }
  return map;
}

export function useDgErrorNotify() {
  const { t } = useAppI18n();
  const { push } = useAppToast();
  const { translateDgError, translateFieldError, translateValidationErrors } = useDgErrorTranslation();

  function notifyDgError(error: unknown, options: DgErrorNotifyOptions = {}): DgErrorNotifyResult {
    const fieldLabelResolver = options.fieldLabelResolver;
    const validationLines = translateValidationErrors(error, fieldLabelResolver);
    const fieldErrors = buildFieldErrors(error, translateFieldError, fieldLabelResolver);

    const message = validationLines.length > 0
      ? validationLines.join('\n')
      : translateDgError(error, options.fallbackKey);

    if (options.toast !== false) {
      push({
        title: options.title ?? t('errors.dg.toastTitle'),
        message,
        severity: options.severity ?? resolveToastSeverity(error),
      });
    }

    return { message, fieldErrors };
  }

  return {
    notifyDgError,
  };
}
