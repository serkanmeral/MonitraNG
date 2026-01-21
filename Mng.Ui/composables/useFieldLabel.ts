/**
 * Field label için hibrit fallback mekanizması
 * 
 * Öncelik sırası:
 * 1. Form bazlı label: automated-forms.fields.{formCode}.{fieldName}
 * 2. Dataset bazlı label: dataset.fields.{datasetName}.{fieldName}
 * 3. Field title (fallback)
 * 
 * Note: Legacy mode kullanıldığı için useI18n() yerine useNuxtApp() ile i18n instance'ına erişiyoruz
 */
export const useFieldLabel = () => {
  // Get i18n instance for legacy mode
  const nuxtApp = useNuxtApp();
  const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
  
  const t = (key: string, params?: any) => {
    if (i18n && i18n.t) {
      return i18n.t(key, params);
    }
    if (i18n?.global?.t) {
      return i18n.global.t(key, params);
    }
    return key;
  };

  /**
   * Field label'ı getir (hibrit fallback mekanizması ile)
   * 
   * @param fieldName - Field adı
   * @param field - Field definition (title içerir)
   * @param form - Form object (formCode içerir)
   * @param datasetName - Dataset adı
   * @returns Çevrilmiş label
   */
  const getFieldLabel = (
    fieldName: string,
    field: any,
    form?: any,
    datasetName?: string
  ): string => {
    // 1. Form bazlı label (en yüksek öncelik)
    if (form?.formCode) {
      const formKey = `automated-forms.fields.${form.formCode}.${fieldName}`;
      const formTranslated = t(formKey);
      if (formTranslated !== formKey) {
        return formTranslated;
      }
    }

    // 2. Dataset bazlı label (orta öncelik)
    if (datasetName) {
      const datasetKey = `dataset.fields.${datasetName}.${fieldName}`;
      const datasetTranslated = t(datasetKey);
      if (datasetTranslated !== datasetKey) {
        return datasetTranslated;
      }
    }

    // 3. Son fallback: field.title veya field.name
    return field?.title || fieldName;
  };

  return {
    getFieldLabel,
  };
};
