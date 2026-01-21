import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';

// Field Types
export type FieldType = 'text' | 'number' | 'bool' | 'datetime' | 'object' | 'relation' | 'persons' | 'personGroups' | 'incremental';

// Incremental Options
export interface IncrementalOptions {
  format?: string;
  startValue?: number;
  incrementStep?: number;
}

// Field Validation Rules
export interface FieldValidationRules {
  min?: number;              // For number fields: minimum value
  max?: number;              // For number fields: maximum value
  minLength?: number;        // For text fields: minimum length
  maxLength?: number;        // For text fields: maximum length
  pattern?: string;          // For text fields: regex pattern
  minItems?: number;         // For array fields: minimum items
  maxItems?: number;         // For array fields: maximum items
  minDate?: string;          // For datetime fields: minimum date (ISO 8601)
  maxDate?: string;          // For datetime fields: maximum date (ISO 8601)
  message?: string;          // Custom error message (optional)
}

// Object Schema (for object field type)
export interface ObjectSchema {
  [key: string]: string; // fieldName -> fieldType
}

// Field Definition
export interface FieldDefinition {
  fieldType: FieldType;
  name: string;
  title?: string;
  mandatory?: boolean;
  unique?: boolean;
  isArray?: boolean;
  defaultValue?: any;
  relationDataset?: string; // For relation type
  relationField?: string; // For relation type (default: __dataId)
  incrementalOptions?: IncrementalOptions; // For incremental type
  objectSchema?: ObjectSchema; // For object type (not in backend, but used in UI)
  validation?: FieldValidationRules;
}

// Validation Definition
export interface ValidationDefinition {
  name: string;
  description?: string;
  type?: 'http' | 'expression';
  url?: string; // For http type
  method?: string; // For http type
  expression?: string; // For expression type
  fields?: string[]; // For http type
  when?: 'create' | 'update' | 'both';
  order?: number;
}

// Query Parameter Definition
export interface QueryParameterDefinition {
  name: string;
  type?: string;
  required?: boolean;
  defaultValue?: any;
}

// Query Definition DTO (for create/update)
export interface QueryDefinitionDto {
  name: string;
  description?: string;
  pipeline?: any[]; // MongoDB aggregation pipeline (array of objects)
  parameters?: string[] | QueryParameterDefinition[]; // Legacy: string[], New: QueryParameterDefinition[]
}

// Query Definition Response (from API)
export interface QueryDefinitionResponseDto {
  name: string;
  description?: string;
  pipeline?: any[];
  parameters?: string[] | QueryParameterDefinition[];
}

// Index Definition
export interface IndexDefinition {
  name: string;
  fields: { [fieldName: string]: 1 | -1 }; // 1 for ascending, -1 for descending
  unique?: boolean;
}

// Dataset Response DTO
export interface Dataset {
  dataId: string;
  name: string;
  description?: string;
  category?: string;
  forceSchema: boolean;
  logging: string;
  publishMode: string;
  fieldsCount: number;
  fields?: FieldDefinition[];
  validationsCount: number;
  validations?: ValidationDefinition[];
  queriesCount: number;
  queries?: QueryDefinitionResponseDto[];
  indexListCount: number;
  indexList?: IndexDefinition[];
  createInfo: {
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
  historyCount: number;
}

// Create Dataset DTO
export interface CreateDatasetDto {
  name: string;
  description?: string;
  category?: string;
  forceSchema?: boolean;
  logging?: 'none' | 'self' | 'common';
  publishMode?: 'none' | 'basic' | 'full';
  fields?: FieldDefinition[];
  validations?: ValidationDefinition[];
  queries?: QueryDefinitionDto[];
  indexList?: IndexDefinition[];
}

// Update Dataset DTO
export interface UpdateDatasetDto {
  description?: string;
  category?: string;
  forceSchema?: boolean;
  logging?: 'none' | 'self' | 'common';
  publishMode?: 'none' | 'basic' | 'full';
  fields?: FieldDefinition[];
  validations?: ValidationDefinition[];
  queries?: QueryDefinitionDto[];
  indexList?: IndexDefinition[];
}

interface DatasetState {
  datasets: Dataset[];
  currentDataset: Dataset | null;
  loading: boolean;
  error: string | null;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export const useDatasetStore = defineStore('dataset', {
  state: (): DatasetState => ({
    datasets: [],
    currentDataset: null,
    loading: false,
    error: null,
    totalCount: 0,
    pageNumber: 1,
    pageSize: 20,
    totalPages: 1,
  }),

  getters: {
    /**
     * Dataset by name
     */
    getDatasetByName: (state) => {
      return (name: string) => state.datasets.find(ds => ds.name === name);
    },

    /**
     * Dataset by ID
     */
    getDatasetById: (state) => {
      return (dataId: string) => state.datasets.find(ds => ds.dataId === dataId);
    },
  },

  actions: {
    /**
     * Fetch datasets with pagination
     */
    async fetchDatasets(params?: {
      pageNumber?: number;
      pageSize?: number;
      includeDeleted?: boolean;
    }) {
      this.loading = true;
      this.error = null;
      
      try {
        const pageNumber = params?.pageNumber || this.pageNumber;
        const pageSize = params?.pageSize || this.pageSize;
        
        const queryParams = new URLSearchParams();
        queryParams.append('pageNumber', pageNumber.toString());
        queryParams.append('pageSize', pageSize.toString());
        
        // Note: search parameter is not yet implemented in backend
        // if (params?.search && params.search.trim()) {
        //   queryParams.append('search', params.search.trim());
        // }
        
        const url = `/api/v1/datasets?${queryParams.toString()}`;
        const response = await fetchFromDataGateway(url, 'GET');
        
        // API response format: PagedResultDto<DatasetResponseDto>
        const itemsArray = response.items || response.Items;
        const totalCountValue = response.totalCount ?? response.TotalCount ?? 0;
        const pageNumberValue = response.pageNumber ?? response.PageNumber ?? pageNumber;
        const pageSizeValue = response.pageSize ?? response.PageSize ?? pageSize;
        const totalPagesValue = response.totalPages ?? response.TotalPages ?? Math.ceil(totalCountValue / pageSizeValue);
        
        if (itemsArray && Array.isArray(itemsArray)) {
          this.datasets = itemsArray.map((item: any) => this.mapToDataset(item));
          this.totalCount = totalCountValue;
          this.pageNumber = pageNumberValue;
          this.pageSize = pageSizeValue;
          this.totalPages = totalPagesValue;
        } else if (Array.isArray(response)) {
          // Fallback: Direct array response
          this.datasets = response.map((item: any) => this.mapToDataset(item));
          this.totalCount = response.length;
          this.totalPages = Math.ceil(this.totalCount / this.pageSize);
        } else {
          this.datasets = [];
          this.totalCount = 0;
          this.totalPages = 0;
        }
      } catch (error: any) {
        this.error = error.message || 'Datasets yüklenirken bir hata oluştu';
        this.datasets = [];
        this.totalCount = 0;
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Fetch dataset by name
     */
    async fetchDatasetByName(name: string) {
      this.loading = true;
      this.error = null;
      
      try {
        // URL encode the dataset name (it may contain special characters like @)
        const encodedName = encodeURIComponent(name);
        const url = `/api/v1/datasets/${encodedName}`;
        const response = await fetchFromDataGateway(url, 'GET');
        
        if (response) {
          this.currentDataset = this.mapToDataset(response);
          
          // Update in list if exists
          const index = this.datasets.findIndex(ds => ds.name === name);
          if (index !== -1) {
            this.datasets[index] = this.currentDataset;
          }
          
          return this.currentDataset;
        }
        
        throw new Error('Dataset bulunamadı');
      } catch (error: any) {
        this.error = error.message || 'Dataset yüklenirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Create new dataset
     */
    async createDataset(datasetData: CreateDatasetDto) {
      this.loading = true;
      this.error = null;
      
      try {
        const url = `/api/v1/datasets`;
        const response = await fetchFromDataGateway(url, 'POST', datasetData);
        
        if (response) {
          const newDataset = this.mapToDataset(response);
          
          // Add to list
          this.datasets.unshift(newDataset);
          this.totalCount++;
          
          this.currentDataset = newDataset;
          return newDataset;
        }
        
        throw new Error('Dataset oluşturulamadı');
      } catch (error: any) {
        this.error = error.message || 'Dataset oluşturulurken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Update dataset
     */
    async updateDataset(name: string, datasetData: UpdateDatasetDto) {
      this.loading = true;
      this.error = null;
      
      try {
        const encodedName = encodeURIComponent(name);
        const url = `/api/v1/datasets/${encodedName}`;
        const response = await fetchFromDataGateway(url, 'PUT', datasetData);
        
        if (response) {
          const updatedDataset = this.mapToDataset(response);
          
          // Update in list
          const index = this.datasets.findIndex(ds => ds.name === name);
          if (index !== -1) {
            this.datasets[index] = updatedDataset;
          }
          
          if (this.currentDataset && this.currentDataset.name === name) {
            this.currentDataset = updatedDataset;
          }
          
          return updatedDataset;
        }
        
        throw new Error('Dataset güncellenemedi');
      } catch (error: any) {
        this.error = error.message || 'Dataset güncellenirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Delete dataset (hard delete)
     */
    async deleteDataset(name: string) {
      this.loading = true;
      this.error = null;
      
      try {
        const encodedName = encodeURIComponent(name);
        const url = `/api/v1/datasets/${encodedName}`;
        await fetchFromDataGateway(url, 'DELETE');
        
        // Remove from list
        this.datasets = this.datasets.filter(ds => ds.name !== name);
        this.totalCount--;
        
        if (this.currentDataset && this.currentDataset.name === name) {
          this.currentDataset = null;
        }
      } catch (error: any) {
        this.error = error.message || 'Dataset silinirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Restore deleted dataset
     */
    async restoreDataset(name: string) {
      this.loading = true;
      this.error = null;
      
      try {
        const encodedName = encodeURIComponent(name);
        const url = `/api/v1/datasets/${encodedName}/restore`;
        const response = await fetchFromDataGateway(url, 'POST');
        
        if (response) {
          const restoredDataset = this.mapToDataset(response);
          
          // Add to list (if not exists)
          const index = this.datasets.findIndex(ds => ds.name === name);
          if (index === -1) {
            this.datasets.unshift(restoredDataset);
            this.totalCount++;
          } else {
            this.datasets[index] = restoredDataset;
          }
          
          this.currentDataset = restoredDataset;
          return restoredDataset;
        }
        
        throw new Error('Dataset geri yüklenemedi');
      } catch (error: any) {
        this.error = error.message || 'Dataset geri yüklenirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Map API response to Dataset interface
     */
    mapToDataset(item: any): Dataset {
      return {
        dataId: item.DataId || item.dataId || item.__dataId || '',
        name: item.Name || item.name || '',
        description: item.Description ?? item.description ?? undefined,
        category: item.Category ?? item.category ?? undefined,
        forceSchema: item.ForceSchema ?? item.forceSchema ?? true,
        logging: item.Logging || item.logging || 'none',
        publishMode: item.PublishMode || item.publishMode || item.PublishMode || 'none',
        fieldsCount: item.FieldsCount ?? item.fieldsCount ?? (item.Fields?.length ?? item.fields?.length ?? 0),
        fields: item.Fields || item.fields ? this.mapFields(item.Fields || item.fields) : undefined,
        validationsCount: item.ValidationsCount ?? item.validationsCount ?? (item.Validations?.length ?? item.validations?.length ?? 0),
        validations: item.Validations || item.validations ? this.mapValidations(item.Validations || item.validations) : undefined,
        queriesCount: item.QueriesCount ?? item.queriesCount ?? (item.Queries?.length ?? item.queries?.length ?? 0),
        queries: item.Queries || item.queries ? this.mapQueries(item.Queries || item.queries) : undefined,
        indexListCount: item.IndexListCount ?? item.indexListCount ?? (item.IndexList?.length ?? item.indexList?.length ?? 0),
        indexList: item.IndexList || item.indexList ? this.mapIndexList(item.IndexList || item.indexList) : undefined,
        createInfo: item.CreateInfo || item.createInfo || {
          createdAt: item.createdAt || new Date(),
          userInfo: {
            uid: item.createdBy?.uid || item.createdBy?.userId || '',
            userName: item.createdBy?.userName || item.createdBy?.username || '',
            domain: item.createdBy?.domain || item.domain || '',
          },
        },
        lastUpdateInfo: item.LastUpdateInfo ?? item.lastUpdateInfo ?? null,
        historyCount: item.HistoryCount ?? item.historyCount ?? 0,
      };
    },

    /**
     * Map fields array
     */
    mapFields(fields: any[]): FieldDefinition[] {
      if (!Array.isArray(fields)) return [];
      
      return fields.map((field: any) => ({
        fieldType: field.fieldType || field.FieldType || 'text',
        name: field.name || field.Name || '',
        title: field.title ?? field.Title ?? undefined,
        mandatory: field.mandatory ?? field.Mandatory ?? false,
        unique: field.unique ?? field.Unique ?? false,
        isArray: field.isArray ?? field.IsArray ?? false,
        defaultValue: field.defaultValue ?? field.DefaultValue ?? undefined,
        relationDataset: field.relationDataset ?? field.RelationDataset ?? undefined,
        relationField: field.relationField ?? field.RelationField ?? '__dataId',
        incrementalOptions: field.incrementalOptions || field.IncrementalOptions ? {
          format: field.incrementalOptions?.format ?? field.IncrementalOptions?.Format ?? undefined,
          startValue: field.incrementalOptions?.startValue ?? field.IncrementalOptions?.StartValue ?? 1,
          incrementStep: field.incrementalOptions?.incrementStep ?? field.IncrementalOptions?.IncrementStep ?? 1,
        } : undefined,
        objectSchema: field.objectSchema ?? field.ObjectSchema ?? undefined,
        validation: field.validation || field.Validation ? {
          min: field.validation?.min ?? field.Validation?.Min ?? undefined,
          max: field.validation?.max ?? field.Validation?.Max ?? undefined,
          minLength: field.validation?.minLength ?? field.Validation?.MinLength ?? undefined,
          maxLength: field.validation?.maxLength ?? field.Validation?.MaxLength ?? undefined,
          pattern: field.validation?.pattern ?? field.Validation?.Pattern ?? undefined,
          minItems: field.validation?.minItems ?? field.Validation?.MinItems ?? undefined,
          maxItems: field.validation?.maxItems ?? field.Validation?.MaxItems ?? undefined,
          minDate: field.validation?.minDate ?? field.Validation?.MinDate ?? undefined,
          maxDate: field.validation?.maxDate ?? field.Validation?.MaxDate ?? undefined,
          message: field.validation?.message ?? field.Validation?.Message ?? undefined,
        } : undefined,
      }));
    },

    /**
     * Map validations array
     */
    mapValidations(validations: any[]): ValidationDefinition[] {
      if (!Array.isArray(validations)) return [];
      
      return validations.map((val: any) => ({
        name: val.name || val.Name || '',
        description: val.description ?? val.Description ?? undefined,
        type: val.type || val.Type || 'http',
        url: val.url ?? val.Url ?? undefined,
        method: val.method ?? val.Method ?? 'POST',
        expression: val.expression ?? val.Expression ?? undefined,
        fields: val.fields || val.Fields ? (Array.isArray(val.fields || val.Fields) ? (val.fields || val.Fields) : []) : undefined,
        when: val.when || val.When || 'both',
        order: val.order ?? val.Order ?? 0,
      }));
    },

    /**
     * Map queries array
     */
    mapQueries(queries: any[]): QueryDefinitionResponseDto[] {
      if (!Array.isArray(queries)) return [];
      
      return queries.map((query: any) => ({
        name: query.name || query.Name || '',
        description: query.description ?? query.Description ?? undefined,
        pipeline: query.pipeline || query.Pipeline || undefined,
        parameters: query.parameters || query.Parameters || undefined,
      }));
    },

    /**
     * Map index list array
     */
    mapIndexList(indexList: any[]): IndexDefinition[] {
      if (!Array.isArray(indexList)) return [];
      
      return indexList.map((index: any) => ({
        name: index.name || index.Name || '',
        fields: index.fields || index.Fields || {},
        unique: index.unique ?? index.Unique ?? false,
      }));
    },

    /**
     * Clear error
     */
    clearError() {
      this.error = null;
    },

    /**
     * Reset current dataset
     */
    resetCurrentDataset() {
      this.currentDataset = null;
    },
  },
});
