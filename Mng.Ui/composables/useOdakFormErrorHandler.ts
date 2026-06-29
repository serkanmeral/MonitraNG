import { ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAppToast } from '@/composables/useAppToast';
import { useApiErrorNotify } from '@/composables/useApiErrorNotify';

export function useOdakFormErrorHandler(options: {
  saveErrorTitleKey: string;
  loadErrorTitleKey?: string;
  saveFallbackKey: string;
  loadFallbackKey?: string;
  fieldLabelResolver?: (field: string) => string;
}) {
  const { t } = useAppI18n();
  const { push } = useAppToast();
  const { notifyApiError } = useApiErrorNotify();
  const fieldErrors = ref<Record<string, string[]>>({});

  function clearFieldErrors() {
    fieldErrors.value = {};
  }

  function fieldMessages(name: string): string[] | undefined {
    const messages = fieldErrors.value[name];
    return messages?.length ? messages : undefined;
  }

  function notifyClientValidation(errors: Record<string, string>) {
    fieldErrors.value = Object.fromEntries(
      Object.entries(errors).map(([field, message]) => [field, [message]]),
    );
    push({
      title: t('errors.dg.toastTitle'),
      message: Object.values(errors).join('\n'),
      severity: 'warning',
    });
  }

  function handleLoadError(error: unknown) {
    notifyApiError(error, {
      title: t(options.loadErrorTitleKey ?? options.saveErrorTitleKey),
      fallbackKey: options.loadFallbackKey ?? options.saveFallbackKey,
    });
  }

  function handleSaveError(error: unknown) {
    const result = notifyApiError(error, {
      title: t(options.saveErrorTitleKey),
      fallbackKey: options.saveFallbackKey,
      fieldLabelResolver: options.fieldLabelResolver,
    });
    fieldErrors.value = result.fieldErrors;
  }

  return {
    fieldErrors,
    clearFieldErrors,
    fieldMessages,
    notifyClientValidation,
    handleLoadError,
    handleSaveError,
  };
}
