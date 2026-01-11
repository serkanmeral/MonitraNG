import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';

export interface DatasetCategory {
  dataId: string;
  categoryName: string;
  categoryDescription?: string;
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

export interface CreateDatasetCategoryDto {
  categoryName: string;
  categoryDescription?: string;
}

export interface UpdateDatasetCategoryDto {
  categoryName?: string;
  categoryDescription?: string;
}

interface DatasetCategoryState {
  categories: DatasetCategory[];
  currentCategory: DatasetCategory | null;
  loading: boolean;
  error: string | null;
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export const useDatasetCategoryStore = defineStore('datasetCategory', {
  state: (): DatasetCategoryState => ({
    categories: [],
    currentCategory: null,
    loading: false,
    error: null,
    totalCount: 0,
    pageNumber: 1,
    pageSize: 20,
    totalPages: 1,
  }),

  getters: {
    /**
     * Category by ID
     */
    getCategoryById: (state) => {
      return (dataId: string) => state.categories.find(cat => cat.dataId === dataId);
    },
  },

  actions: {
    /**
     * Fetch categories with pagination
     */
    async fetchCategories(params?: {
      pageNumber?: number;
      pageSize?: number;
      search?: string; // Search term for category name or description
      includeDeleted?: boolean; // For future use (filtering deleted items)
    }) {
      this.loading = true;
      this.error = null;
      
      try {
        const pageNumber = params?.pageNumber || this.pageNumber;
        const pageSize = params?.pageSize || this.pageSize;
        
        const queryParams = new URLSearchParams();
        queryParams.append('pageNumber', pageNumber.toString());
        queryParams.append('pageSize', pageSize.toString());
        
        if (params?.search && params.search.trim()) {
          queryParams.append('search', params.search.trim());
        }
        
        const url = `/api/v1/dataset-categories?${queryParams.toString()}`;
        const response = await fetchFromDataGateway(url, 'GET');
        
        // API response format: PagedResultDto<DatasetCategoryResponseDto>
        // Backend'den gelen format: { items, totalCount, pageNumber, pageSize, totalPages }
        // Hem büyük harf (Items, TotalCount) hem küçük harf (items, totalCount) destekleniyor
        const itemsArray = response.items || response.Items;
        const totalCountValue = response.totalCount ?? response.TotalCount ?? 0;
        const pageNumberValue = response.pageNumber ?? response.PageNumber ?? pageNumber;
        const pageSizeValue = response.pageSize ?? response.PageSize ?? pageSize;
        const totalPagesValue = response.totalPages ?? response.TotalPages ?? Math.ceil(totalCountValue / pageSizeValue);
        
        if (itemsArray && Array.isArray(itemsArray)) {
          this.categories = itemsArray.map((item: any) => this.mapToCategory(item));
          this.totalCount = totalCountValue;
          this.pageNumber = pageNumberValue;
          this.pageSize = pageSizeValue;
          this.totalPages = totalPagesValue;
        } else if (Array.isArray(response)) {
          // Fallback: Direct array response
          this.categories = response.map((item: any) => this.mapToCategory(item));
          this.totalCount = response.length;
          this.totalPages = Math.ceil(this.totalCount / this.pageSize);
        } else {
          this.categories = [];
          this.totalCount = 0;
          this.totalPages = 0;
        }
      } catch (error: any) {
        this.error = error.message || 'Kategoriler yüklenirken bir hata oluştu';
        this.categories = [];
        this.totalCount = 0;
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Fetch category by ID
     */
    async fetchCategoryById(dataId: string) {
      this.loading = true;
      this.error = null;
      
      try {
        const url = `/api/v1/dataset-categories/${dataId}`;
        const response = await fetchFromDataGateway(url, 'GET');
        
        if (response) {
          this.currentCategory = this.mapToCategory(response);
          
          // Update in list if exists
          const index = this.categories.findIndex(cat => cat.dataId === dataId);
          if (index !== -1) {
            this.categories[index] = this.currentCategory;
          }
          
          return this.currentCategory;
        }
        
        throw new Error('Kategori bulunamadı');
      } catch (error: any) {
        this.error = error.message || 'Kategori yüklenirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Create new category
     */
    async createCategory(categoryData: CreateDatasetCategoryDto) {
      this.loading = true;
      this.error = null;
      
      try {
        const url = `/api/v1/dataset-categories`;
        const response = await fetchFromDataGateway(url, 'POST', categoryData);
        
        if (response) {
          const newCategory = this.mapToCategory(response);
          
          // Add to list
          this.categories.unshift(newCategory);
          this.totalCount++;
          
          this.currentCategory = newCategory;
          return newCategory;
        }
        
        throw new Error('Kategori oluşturulamadı');
      } catch (error: any) {
        this.error = error.message || 'Kategori oluşturulurken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Update category
     */
    async updateCategory(dataId: string, categoryData: UpdateDatasetCategoryDto) {
      this.loading = true;
      this.error = null;
      
      try {
        const url = `/api/v1/dataset-categories/${dataId}`;
        const response = await fetchFromDataGateway(url, 'PUT', categoryData);
        
        if (response) {
          const updatedCategory = this.mapToCategory(response);
          
          // Update in list
          const index = this.categories.findIndex(cat => cat.dataId === dataId);
          if (index !== -1) {
            this.categories[index] = updatedCategory;
          }
          
          if (this.currentCategory && this.currentCategory.dataId === dataId) {
            this.currentCategory = updatedCategory;
          }
          
          return updatedCategory;
        }
        
        throw new Error('Kategori güncellenemedi');
      } catch (error: any) {
        this.error = error.message || 'Kategori güncellenirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Delete category (hard delete)
     */
    async deleteCategory(dataId: string) {
      this.loading = true;
      this.error = null;
      
      try {
        const url = `/api/v1/dataset-categories/${dataId}`;
        await fetchFromDataGateway(url, 'DELETE');
        
        // Remove from list
        this.categories = this.categories.filter(cat => cat.dataId !== dataId);
        this.totalCount--;
        
        if (this.currentCategory && this.currentCategory.dataId === dataId) {
          this.currentCategory = null;
        }
      } catch (error: any) {
        this.error = error.message || 'Kategori silinirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Restore deleted category
     */
    async restoreCategory(dataId: string) {
      this.loading = true;
      this.error = null;
      
      try {
        const url = `/api/v1/dataset-categories/${dataId}/restore`;
        const response = await fetchFromDataGateway(url, 'POST');
        
        if (response) {
          const restoredCategory = this.mapToCategory(response);
          
          // Add to list (if not exists)
          const index = this.categories.findIndex(cat => cat.dataId === dataId);
          if (index === -1) {
            this.categories.unshift(restoredCategory);
            this.totalCount++;
          } else {
            this.categories[index] = restoredCategory;
          }
          
          this.currentCategory = restoredCategory;
          return restoredCategory;
        }
        
        throw new Error('Kategori geri yüklenemedi');
      } catch (error: any) {
        this.error = error.message || 'Kategori geri yüklenirken bir hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Map API response to Category interface
     */
    mapToCategory(item: any): DatasetCategory {
      return {
        dataId: item.DataId || item.dataId || item.__dataId || '',
        categoryName: item.CategoryName || item.categoryName || '',
        categoryDescription: item.CategoryDescription ?? item.categoryDescription ?? undefined,
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
     * Clear error
     */
    clearError() {
      this.error = null;
    },

    /**
     * Reset current category
     */
    resetCurrentCategory() {
      this.currentCategory = null;
    },
  },
});
