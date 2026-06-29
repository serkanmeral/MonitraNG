import { useAppI18n } from '@/composables/useAppI18n';
import { dgErrorCode, dgExtractMessage, dgExtractValidationErrors, type DgValidationError } from '@/utils/dgErrorUtils';

const CODE_TO_I18N_KEY: Record<string, string> = {
  VALIDATION_ERROR: 'errors.dg.validationError',
  DUPLICATE_KEY: 'errors.dg.duplicateKey',
  VALIDATION_UNIQUE_CONSTRAINT: 'errors.dg.uniqueConstraint',
  DATASET_NOT_FOUND: 'errors.dg.datasetNotFound',
  DATA_NOT_FOUND: 'errors.dg.dataNotFound',
  QUERY_NOT_FOUND: 'errors.dg.queryNotFound',
  MISSING_PARAMETER: 'errors.dg.missingParameter',
  INVALID_ARGUMENT: 'errors.dg.invalidArgument',
  INVALID_FORMAT: 'errors.dg.invalidFormat',
  INVALID_OPERATION: 'errors.dg.invalidOperation',
  DATABASE_WRITE_ERROR: 'errors.dg.databaseWriteError',
  FORBIDDEN: 'errors.dg.forbidden',
  UNAUTHORIZED: 'errors.dg.unauthorized',
  INTERNAL_ERROR: 'errors.dg.internalError',
};

export function useDgErrorTranslation() {
  const { t } = useAppI18n();

  const translateDgError = (error: unknown, fallbackKey = 'errors.dg.generic'): string => {
    const code = dgErrorCode(error);
    const i18nKey = code ? CODE_TO_I18N_KEY[code] : undefined;
    if (i18nKey) {
      return t(i18nKey);
    }
    return dgExtractMessage(error, t(fallbackKey));
  };

  const translateFieldError = (item: DgValidationError, fieldLabel?: string): string => {
    const field = fieldLabel || item.field || t('errors.dg.field');
    if (item.code === 'VALIDATION_UNIQUE_CONSTRAINT' || item.code === 'DUPLICATE_KEY') {
      return t('errors.dg.uniqueConstraintField', { field });
    }
    if (item.message) {
      return item.message.replaceAll(`'${item.field}'`, field).replaceAll(item.field || '', field);
    }
    return t('errors.dg.validationError');
  };

  const translateValidationErrors = (error: unknown, fieldLabelResolver?: (field: string) => string): string[] => {
    return dgExtractValidationErrors(error).map((item) =>
      translateFieldError(item, item.field ? fieldLabelResolver?.(item.field) : undefined),
    );
  };

  return {
    translateDgError,
    translateFieldError,
    translateValidationErrors,
  };
}
