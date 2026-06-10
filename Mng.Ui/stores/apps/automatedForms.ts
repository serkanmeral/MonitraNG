import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';

/**
 * Automated Form Interface
 */
export interface AutomatedForm {
  __dataId?: string;
  dataId?: string;
  formName: string;
  formCode: string;
  description?: string;
  datasetName: string;
  sideMenuConfig?: {
    enabled: boolean;
    menuItemId?: string;
    routeType: 'path' | 'form';
    routePath?: string;
  };
  listConfig?: {
    columns?: Array<{
      fieldName: string;
      visible: boolean;
      order: number;
      sortable: boolean;
      filterable: boolean;
      width?: number;
      displayField?: string; // For object/relation fields: which field to display in list
      arrayDisplayStyle?: 'chip' | 'badge' | 'pill' | 'outlined' | 'text-separator' | 'comma-separated' | 'list' | 'tag';
      arraySeparator?: string;
      format?: {
        type?: 'none' | 'regex' | 'number' | 'date' | 'currency' | 'text-transform';
        pattern?: string;
        replacement?: string;
        decimalPlaces?: number;
        thousandSeparator?: boolean;
        currencySymbol?: string;
        dateFormat?: string;
        showTime?: boolean;
        timeFormat?: 'HH:mm' | 'HH:mm:ss';
        textTransform?: 'uppercase' | 'lowercase' | 'capitalize';
      };
    }>;
    defaultSortBy?: string;
    defaultSortOrder?: 'asc' | 'desc';
    enableSearch?: boolean;
  };
  formConfig?: {
    visibleFields: string[];
    readonlyFields: string[];
    /** Salt okunur yalnizca duzenleme modunda (or. birincil anahtar kod) */
    readonlyOnEditFields?: string[];
    fieldOrder: string[];
    fieldLabels?: { [fieldName: string]: string };
    relationFieldConfig?: {
      [fieldName: string]: {
        idField: string; // Which field to use as value (default: '__dataId')
        displayField: string; // Which field to display in dropdown (required)
      };
    };
    fieldLayout?: {
      [fieldName: string]: {
        columnSpan?: number; // 1-12 (default: 6 for normal fields, 12 for object fields)
        group?: string; // Field group name (for grouping fields)
        textWidget?: 'text' | 'textarea' | 'richtext';
        choiceWidget?: 'select' | 'autocomplete';
      };
    };
    groupOrder?: string[]; // Display order of groups. Groups not listed appear after.
  };
  isActive: boolean;
  createInfo?: {
    createdAt: string | Date;
    userInfo: {
      uid: string;
      userName: string;
      domain: string;
    };
  };
  lastUpdateInfo?: {
    updatedAt: string | Date;
    userInfo: {
      uid: string;
      userName: string;
      domain: string;
    };
  } | null;
  historyCount?: number;
}

/**
 * Create Automated Form DTO
 */
export interface CreateAutomatedFormDto {
  formName: string;
  formCode: string;
  description?: string;
  datasetName: string;
  sideMenuConfig?: {
    enabled: boolean;
    menuItemId?: string;
    routeType: 'path' | 'form';
    routePath?: string;
  };
  listConfig?: {
    columns?: Array<{
      fieldName: string;
      visible: boolean;
      order: number;
      sortable: boolean;
      filterable: boolean;
      width?: number;
      displayField?: string; // For object/relation fields: which field to display in list
      arrayDisplayStyle?: 'chip' | 'badge' | 'pill' | 'outlined' | 'text-separator' | 'comma-separated' | 'list' | 'tag';
      arraySeparator?: string;
      format?: {
        type?: 'none' | 'regex' | 'number' | 'date' | 'currency' | 'text-transform';
        pattern?: string;
        replacement?: string;
        decimalPlaces?: number;
        thousandSeparator?: boolean;
        currencySymbol?: string;
        dateFormat?: string;
        showTime?: boolean;
        timeFormat?: 'HH:mm' | 'HH:mm:ss';
        textTransform?: 'uppercase' | 'lowercase' | 'capitalize';
      };
    }>;
    defaultSortBy?: string;
    defaultSortOrder?: 'asc' | 'desc';
    enableSearch?: boolean;
  };
  formConfig?: {
    visibleFields: string[];
    readonlyFields: string[];
    /** Salt okunur yalnizca duzenleme modunda (or. birincil anahtar kod) */
    readonlyOnEditFields?: string[];
    fieldOrder: string[];
    fieldLabels?: { [fieldName: string]: string };
    relationFieldConfig?: {
      [fieldName: string]: {
        idField: string; // Which field to use as value (default: '__dataId')
        displayField: string; // Which field to display in dropdown (required)
      };
    };
    fieldLayout?: {
      [fieldName: string]: {
        columnSpan?: number; // 1-12 (default: 6 for normal fields, 12 for object fields)
        group?: string; // Field group name (for grouping fields)
        textWidget?: 'text' | 'textarea' | 'richtext';
        choiceWidget?: 'select' | 'autocomplete';
      };
    };
    groupOrder?: string[];
  };
  isActive: boolean;
}

/**
 * Update Automated Form DTO
 */
export interface UpdateAutomatedFormDto {
  formName?: string;
  formCode?: string;
  description?: string;
  datasetName?: string;
  sideMenuConfig?: {
    enabled: boolean;
    menuItemId?: string;
    routeType: 'path' | 'form';
    routePath?: string;
  };
  listConfig?: {
    columns?: Array<{
      fieldName: string;
      visible: boolean;
      order: number;
      sortable: boolean;
      filterable: boolean;
      width?: number;
      displayField?: string; // For object/relation fields: which field to display in list
      arrayDisplayStyle?: 'chip' | 'badge' | 'pill' | 'outlined' | 'text-separator' | 'comma-separated' | 'list' | 'tag';
      arraySeparator?: string;
      format?: {
        type?: 'none' | 'regex' | 'number' | 'date' | 'currency' | 'text-transform';
        pattern?: string;
        replacement?: string;
        decimalPlaces?: number;
        thousandSeparator?: boolean;
        currencySymbol?: string;
        dateFormat?: string;
        showTime?: boolean;
        timeFormat?: 'HH:mm' | 'HH:mm:ss';
        textTransform?: 'uppercase' | 'lowercase' | 'capitalize';
      };
    }>;
    defaultSortBy?: string;
    defaultSortOrder?: 'asc' | 'desc';
    enableSearch?: boolean;
  };
  formConfig?: {
    visibleFields: string[];
    readonlyFields: string[];
    /** Salt okunur yalnizca duzenleme modunda (or. birincil anahtar kod) */
    readonlyOnEditFields?: string[];
    fieldOrder: string[];
    fieldLabels?: { [fieldName: string]: string };
    relationFieldConfig?: {
      [fieldName: string]: {
        idField: string; // Which field to use as value (default: '__dataId')
        displayField: string; // Which field to display in dropdown (required)
      };
    };
    fieldLayout?: {
      [fieldName: string]: {
        columnSpan?: number; // 1-12 (default: 6 for normal fields, 12 for object fields)
        group?: string; // Field group name (for grouping fields)
        textWidget?: 'text' | 'textarea' | 'richtext';
        choiceWidget?: 'select' | 'autocomplete';
      };
    };
    groupOrder?: string[];
  };
  isActive?: boolean;
}

interface AutomatedFormsState {
  forms: AutomatedForm[];
  currentForm: AutomatedForm | null;
  loading: boolean;
  error: string | null;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export const useAutomatedFormsStore = defineStore('automatedForms', {
  state: (): AutomatedFormsState => ({
    forms: [],
    currentForm: null,
    loading: false,
    error: null,
    totalCount: 0,
    pageNumber: 1,
    pageSize: 20,
    totalPages: 1,
  }),

  getters: {
    /**
     * Form by code
     */
    getFormByCode: (state) => {
      return (formCode: string) => state.forms.find(form => form.formCode === formCode);
    },

    /**
     * Form by dataId
     */
    getFormById: (state) => {
      return (dataId: string) => state.forms.find(form => (form.__dataId || form.dataId) === dataId);
    },

    /**
     * Active forms only
     */
    activeForms: (state) => {
      return state.forms.filter(form => form.isActive);
    },
  },

  actions: {
    /**
     * Fetch forms with pagination
     */
    async fetchForms(params?: {
      pageNumber?: number;
      pageSize?: number;
      formCode?: string;
      datasetName?: string;
      isActive?: boolean;
    }) {
      this.loading = true;
      this.error = null;
      
      try {
        const pageNumber = params?.pageNumber || this.pageNumber;
        const pageSize = params?.pageSize || this.pageSize;
        
        const queryParams = new URLSearchParams();
        queryParams.append('pageNumber', pageNumber.toString());
        queryParams.append('pageSize', pageSize.toString());
        
        // Filter by formCode if provided
        if (params?.formCode) {
          queryParams.append('formCode', params.formCode);
        }
        
        // Filter by datasetName if provided
        if (params?.datasetName) {
          queryParams.append('datasetName', params.datasetName);
        }
        
        // Filter by isActive if provided
        if (params?.isActive !== undefined) {
          queryParams.append('isActive', params.isActive.toString());
        }
        
        const url = `/api/v1/data/@automated_forms?${queryParams.toString()}`;
        const response = await fetchFromDataGateway(url, 'GET');
        
        // API response format: PagedResultDto or array
        const itemsArray = response.items || response.Items || response;
        const totalCountValue = response.totalCount ?? response.TotalCount ?? (Array.isArray(itemsArray) ? itemsArray.length : 0);
        const pageNumberValue = response.pageNumber ?? response.PageNumber ?? pageNumber;
        const pageSizeValue = response.pageSize ?? response.PageSize ?? pageSize;
        const totalPagesValue = response.totalPages ?? response.TotalPages ?? Math.ceil(totalCountValue / pageSizeValue);
        
        if (itemsArray && Array.isArray(itemsArray)) {
          this.forms = itemsArray.map((item: any) => this.mapToForm(item));
          this.totalCount = totalCountValue;
          this.pageNumber = pageNumberValue;
          this.pageSize = pageSizeValue;
          this.totalPages = totalPagesValue;
        } else {
          this.forms = [];
          this.totalCount = 0;
          this.totalPages = 0;
        }
      } catch (error: any) {
        this.error = error.message || 'Formlar yüklenirken bir hata oluştu';
        this.forms = [];
        this.totalCount = 0;
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Fetch form by code
     */
    async fetchFormByCode(formCode: string) {
      this.loading = true;
      this.error = null;
      
      try {
        const url = `/api/v1/data/@automated_forms?formCode=${encodeURIComponent(formCode)}`;
        const response = await fetchFromDataGateway(url, 'GET');
        
        // Response could be array or single object or PagedResultDto
        let formsArray: any[] = [];
        
        if (Array.isArray(response)) {
          formsArray = response;
        } else if (response.items || response.Items) {
          formsArray = response.items || response.Items || [];
        } else if (response) {
          formsArray = [response];
        }
        
        // Filter by formCode to ensure we get the exact match (case-sensitive or case-insensitive)
        // Backend might return multiple results if formCode is not unique, so we need to find exact match
        let formData = formsArray.find((form: any) => {
          const code = form.formCode || form.FormCode || '';
          const matches = code === formCode || code.toLowerCase() === formCode.toLowerCase();
          return matches;
        });
        
        // If exact match not found, try first item (fallback)
        if (!formData && formsArray.length > 0) {
          formData = formsArray[0];
        }
        
        if (formData) {
          this.currentForm = this.mapToForm(formData);
          
          // Verify formCode matches (extra safety check)
          if (this.currentForm.formCode !== formCode && this.currentForm.formCode.toLowerCase() !== formCode.toLowerCase()) {
            throw new Error(`Form kodu uyuşmazlığı: Beklenen "${formCode}", Bulunan "${this.currentForm.formCode}"`);
          }
          
          // Update in list if exists
          const index = this.forms.findIndex(form => form.formCode === formCode);
          if (index !== -1) {
            this.forms[index] = this.currentForm;
          }
          
          return this.currentForm;
        }
        
        throw new Error(`Form bulunamadı: "${formCode}"`);
      } catch (error: any) {
        this.error = error.message || 'Form yüklenirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Fetch form by dataId
     */
    async fetchFormById(dataId: string) {
      this.loading = true;
      this.error = null;
      
      try {
        const url = `/api/v1/data/@automated_forms/${dataId}`;
        const response = await fetchFromDataGateway(url, 'GET');
        
        if (response) {
          this.currentForm = this.mapToForm(response);
          
          // Update in list if exists
          const index = this.forms.findIndex(form => (form.__dataId || form.dataId) === dataId);
          if (index !== -1) {
            this.forms[index] = this.currentForm;
          }
          
          return this.currentForm;
        }
        
        throw new Error('Form bulunamadı');
      } catch (error: any) {
        this.error = error.message || 'Form yüklenirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Create new form
     */
    async createForm(formData: CreateAutomatedFormDto) {
      this.loading = true;
      this.error = null;
      
      try {
        const url = `/api/v1/data/@automated_forms`;
        const response = await fetchFromDataGateway(url, 'POST', formData);
        
        if (response) {
          const newForm = this.mapToForm(response);
          
          // Add to list
          this.forms.unshift(newForm);
          this.totalCount++;
          
          this.currentForm = newForm;
          return newForm;
        }
        
        throw new Error('Form oluşturulamadı');
      } catch (error: any) {
        this.error = error.message || 'Form oluşturulurken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Update form
     */
    async updateForm(dataId: string, formData: UpdateAutomatedFormDto) {
      this.loading = true;
      this.error = null;
      
      try {
        const url = `/api/v1/data/@automated_forms/${dataId}`;
        const response = await fetchFromDataGateway(url, 'PUT', formData);
        
        if (response) {
          const updatedForm = this.mapToForm(response);
          
          // Update in list
          const index = this.forms.findIndex(form => (form.__dataId || form.dataId) === dataId);
          if (index !== -1) {
            this.forms[index] = updatedForm;
          }
          
          if (this.currentForm && (this.currentForm.__dataId || this.currentForm.dataId) === dataId) {
            this.currentForm = updatedForm;
          }
          
          return updatedForm;
        }
        
        throw new Error('Form güncellenemedi');
      } catch (error: any) {
        this.error = error.message || 'Form güncellenirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Delete form
     */
    async deleteForm(dataId: string) {
      this.loading = true;
      this.error = null;
      
      try {
        const url = `/api/v1/data/@automated_forms/${dataId}`;
        await fetchFromDataGateway(url, 'DELETE');
        
        // Remove from list
        this.forms = this.forms.filter(form => (form.__dataId || form.dataId) !== dataId);
        this.totalCount--;
        
        if (this.currentForm && (this.currentForm.__dataId || this.currentForm.dataId) === dataId) {
          this.currentForm = null;
        }
      } catch (error: any) {
        this.error = error.message || 'Form silinirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Map API response to Form interface
     */
    mapToForm(item: any): AutomatedForm {
      return {
        __dataId: item.__dataId || item.DataId || item.dataId || '',
        dataId: item.__dataId || item.DataId || item.dataId || '',
        formName: item.formName || item.FormName || '',
        formCode: item.formCode || item.FormCode || '',
        description: item.description ?? item.Description ?? undefined,
        datasetName: item.datasetName || item.DatasetName || '',
        sideMenuConfig: item.sideMenuConfig ?? item.SideMenuConfig ?? undefined,
        listConfig: item.listConfig ?? item.ListConfig ?? undefined,
        formConfig: item.formConfig ?? item.FormConfig ?? undefined,
        isActive: item.isActive ?? item.IsActive ?? true,
        createInfo: item.createInfo || item.CreateInfo || item.createInfo || {
          createdAt: item.createdAt || new Date(),
          userInfo: {
            uid: item.createdBy?.uid || item.createdBy?.userId || '',
            userName: item.createdBy?.userName || item.createdBy?.username || '',
            domain: item.createdBy?.domain || item.domain || '',
          },
        },
        lastUpdateInfo: item.lastUpdateInfo ?? item.LastUpdateInfo ?? null,
        historyCount: item.historyCount ?? item.HistoryCount ?? 0,
      };
    },

    /**
     * Clear error
     */
    clearError() {
      this.error = null;
    },

    /**
     * Reset current form
     */
    resetCurrentForm() {
      this.currentForm = null;
    },
  },
});
