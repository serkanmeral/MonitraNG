import { defineStore } from 'pinia';
import { fetchFromMngKeeper } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';

function getAccessToken(): string | null {
  const authStore = useAuthStore();
  return authStore.accessToken;
}

export interface Group {
  id: string;
  groupId: string;
  name: string;
  description?: string;
  memberCount: number;
  isActive: boolean;
  createdAt: string | Date;
  updatedAt?: string | Date | null;
  createdBy?: string;
  updatedBy?: string | null;
}

interface GroupState {
  groups: Group[];
  currentGroup: Group | null;
  loading: boolean;
  error: string | null;
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const useGroupStore = defineStore('group', {
  state: (): GroupState => ({
    groups: [],
    currentGroup: null,
    loading: false,
    error: null,
    totalCount: 0,
    page: 1,
    pageSize: 10,
    totalPages: 1,
  }),

  getters: {
    activeGroups: (state): Group[] => {
      return state.groups.filter(group => group.isActive);
    },
    inactiveGroups: (state): Group[] => {
      return state.groups.filter(group => !group.isActive);
    },
    getGroupById: (state) => {
      return (id: string) => state.groups.find(group => group.id === id || group.groupId === id);
    },
  },

  actions: {
    async fetchGroups(params?: { 
      page?: number; 
      pageSize?: number; 
      search?: string;
      isActive?: boolean;
    }) {
      this.loading = true;
      this.error = null;
      
      try {
        const queryParams = new URLSearchParams();
        if (params?.page) queryParams.append('page', params.page.toString());
        if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
        if (params?.search) queryParams.append('searchTerm', params.search);
        if (params?.isActive !== undefined) queryParams.append('isActive', params.isActive.toString());
        
        const url = `/group${queryParams.toString() ? '?' + queryParams.toString() : ''}`;
        
        const response = await fetchFromMngKeeper(url, 'GET');
        
        // API response yapısı kontrolü: Hem büyük harf (Groups, TotalCount) hem küçük harf (groups, totalCount) destekleniyor
        if (response.IsSuccess === false) {
          throw new Error(response.ErrorMessage || 'Gruplar yüklenirken bir hata oluştu');
        }
        
        // Önce küçük harf kontrolü (API'den gelen format: groups, totalCount, page, pageSize, totalPages)
        const groupsArray = response.groups || response.Groups;
        // totalCount için önce küçük harf, sonra büyük harf kontrolü yapıyoruz
        const totalCountValue = response.totalCount !== undefined ? response.totalCount : (response.TotalCount !== undefined ? response.TotalCount : 0);
        const pageValue = response.page ?? response.Page ?? 1;
        const pageSizeValue = response.pageSize ?? response.PageSize ?? 10;
        const totalPagesValue = response.totalPages ?? response.TotalPages ?? 1;
        
        if (groupsArray && Array.isArray(groupsArray)) {
          this.groups = groupsArray.map((group: any) => ({
            id: group.groupId || group.GroupId || group.id || '',
            groupId: group.groupId || group.GroupId || group.id,
            name: group.name || group.Name || '',
            description: group.description || group.Description || null,
            memberCount: group.memberCount ?? group.MemberCount ?? 0,
            isActive: group.isActive !== undefined ? group.isActive : (group.IsActive !== undefined ? group.IsActive : true),
            createdAt: group.createdAt || group.CreatedAt || new Date(),
            updatedAt: group.updatedAt || group.UpdatedAt || null,
            createdBy: group.createdBy || group.CreatedBy,
            updatedBy: group.updatedBy || group.UpdatedBy || null,
          }));
          
          // totalCount'u her zaman güncelle (API'den gelen en güncel değer)
          this.totalCount = totalCountValue;
          this.page = pageValue;
          this.pageSize = pageSizeValue;
          this.totalPages = totalPagesValue;
        } else {
          this.groups = [];
          this.totalCount = 0;
          this.page = 1;
          this.pageSize = 10;
          this.totalPages = 1;
        }
      } catch (err: any) {
        this.error = err.message || 'Gruplar yüklenirken bir hata oluştu.';
      } finally {
        this.loading = false;
      }
    },

    async fetchGroupById(groupId: string) {
      this.loading = true;
      this.error = null;
      
      try {
        const response = await fetchFromMngKeeper(`/group/${groupId}`, 'GET');
        
        if (response.IsSuccess === false) {
          throw new Error(response.ErrorMessage || 'Grup bulunamadı');
        }
        
        const groupData = response.group || response.Group;
        
        if (groupData) {
          const group: Group = {
            id: groupData.groupId || groupData.GroupId || groupData.id || '',
            groupId: groupData.groupId || groupData.GroupId || groupData.id,
            name: groupData.name || groupData.Name || '',
            description: groupData.description || groupData.Description || null,
            memberCount: groupData.memberCount ?? groupData.MemberCount ?? 0,
            isActive: groupData.isActive !== undefined ? groupData.isActive : (groupData.IsActive !== undefined ? groupData.IsActive : true),
            createdAt: groupData.createdAt || groupData.CreatedAt || new Date(),
            updatedAt: groupData.updatedAt || groupData.UpdatedAt || null,
            createdBy: groupData.createdBy || groupData.CreatedBy,
            updatedBy: groupData.updatedBy || groupData.UpdatedBy || null,
          };
          
          this.currentGroup = group;
          return group;
        } else {
          throw new Error('Grup verisi bulunamadı');
        }
      } catch (err: any) {
        this.error = err.message || 'Grup yüklenirken bir hata oluştu.';
        throw err;
      } finally {
        this.loading = false;
      }
    },

    async createGroup(groupData: { name: string; description?: string; isActive?: boolean; permissions?: string[] }) {
      this.loading = true;
      this.error = null;
      
      try {
        const response = await fetchFromMngKeeper('/group', 'POST', {
          Name: groupData.name,
          Description: groupData.description || '',
          IsActive: groupData.isActive !== undefined ? groupData.isActive : true,
          Permissions: groupData.permissions || [],
        });
        
        if (response.IsSuccess === false) {
          throw new Error(response.ErrorMessage || 'Grup oluşturulamadı');
        }
        
        // Note: List will be refreshed when user returns to list page via onActivated hook
        // No need to fetch here as we don't have pagination context
        
        return response;
      } catch (err: any) {
        this.error = err.message || 'Grup oluşturulurken bir hata oluştu.';
        throw err;
      } finally {
        this.loading = false;
      }
    },

    async updateGroup(groupId: string, groupData: { name: string; description?: string; isActive?: boolean; permissions?: string[] }) {
      this.loading = true;
      this.error = null;
      
      try {
        const requestBody = {
          Name: groupData.name,
          Description: groupData.description || '',
          IsActive: groupData.isActive !== undefined ? groupData.isActive : true,
          Permissions: groupData.permissions || [],
        };
        
        const response = await fetchFromMngKeeper(`/group/${groupId}`, 'PUT', requestBody);
        
        if (response.IsSuccess === false) {
          throw new Error(response.ErrorMessage || 'Grup güncellenemedi');
        }
        
        return response;
      } catch (err: any) {
        this.error = err.message || 'Grup güncellenirken bir hata oluştu.';
        throw err;
      } finally {
        this.loading = false;
      }
    },

    async deleteGroup(groupId: string) {
      this.loading = true;
      this.error = null;
      
      try {
        // DELETE endpoint NoContent (204) döndürür, response body yok
        const response = await fetchFromMngKeeper(`/group/${groupId}`, 'DELETE');
        
        // 204 NoContent response'u için özel kontrol
        // Server-side proxy ve apiService 204 için { success: true, statusCode: 204 } döndürür
        if (!response || (response.statusCode === 204 || response.success === true)) {
          // Store'dan kaldır
          this.groups = this.groups.filter(g => g.id !== groupId && g.groupId !== groupId);
          
          if (this.currentGroup && (this.currentGroup.id === groupId || this.currentGroup.groupId === groupId)) {
            this.currentGroup = null;
          }
          
          // Başarılı - 204 NoContent response
          return { success: true };
        }
        
        // Beklenmeyen response formatı
        throw new Error('Unexpected response format from delete group endpoint');
      } catch (err: any) {
        this.error = err.message || 'Grup silinirken bir hata oluştu.';
        throw err;
      } finally {
        this.loading = false;
      }
    },

    async addUserToGroup(groupId: string, userId: string) {
      this.loading = true;
      this.error = null;
      
      try {
        const response = await fetchFromMngKeeper(`/user/${userId}/groups/${groupId}`, 'POST');
        
        if (response.IsSuccess === false) {
          throw new Error(response.ErrorMessage || 'Kullanıcı gruba eklenemedi');
        }
        
        return response;
      } catch (err: any) {
        this.error = err.message || 'Kullanıcı gruba eklenirken bir hata oluştu.';
        throw err;
      } finally {
        this.loading = false;
      }
    },

    async removeUserFromGroup(groupId: string, userId: string) {
      this.loading = true;
      this.error = null;
      
      try {
        const response = await fetchFromMngKeeper(`/user/${userId}/groups/${groupId}`, 'DELETE');
        
        if (response.IsSuccess === false) {
          throw new Error(response.ErrorMessage || 'Kullanıcı gruptan çıkarılamadı');
        }
        
        return response;
      } catch (err: any) {
        this.error = err.message || 'Kullanıcı gruptan çıkarılırken bir hata oluştu.';
        throw err;
      } finally {
        this.loading = false;
      }
    },

    clearError() {
      this.error = null;
    },

    async exportGroups(format: 'csv' | 'xlsx', params?: { 
      search?: string;
      isActive?: boolean;
    }) {
      this.loading = true;
      this.error = null;
      
      try {
        const queryParams = new URLSearchParams();
        queryParams.append('format', format);
        if (params?.search) queryParams.append('searchTerm', params.search);
        if (params?.isActive !== undefined) queryParams.append('isActive', params.isActive.toString());
        
        const url = `/group/export${queryParams.toString() ? '?' + queryParams.toString() : ''}`;
        
        // For file downloads, we need to use fetch with blob response
        const token = getAccessToken();
        if (!token) {
          throw new Error("Access token bulunamadı. Lütfen tekrar giriş yapın.");
        }
        
        const cleanUrl = url.startsWith('/') ? url.slice(1) : url;
        const fullUrl = `/api/keeper/${cleanUrl}`;
        
        const response = await fetch(fullUrl, {
          method: 'GET',
          headers: {
            'Authorization': `Bearer ${token}`,
          },
        });
        
        if (!response.ok) {
          // Try to parse error as JSON, fallback to text
          let errorMessage = 'Export işlemi başarısız oldu';
          try {
            const errorData = await response.json();
            errorMessage = errorData.errorMessage || errorData.message || errorMessage;
          } catch {
            const errorText = await response.text();
            if (errorText) errorMessage = errorText;
          }
          throw new Error(errorMessage);
        }
        
        // Get filename from Content-Disposition header or use default
        const contentDisposition = response.headers.get('Content-Disposition');
        let filename = `gruplar_${new Date().toISOString().split('T')[0]}.${format}`;
        if (contentDisposition) {
          const filenameMatch = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
          if (filenameMatch && filenameMatch[1]) {
            filename = filenameMatch[1].replace(/['"]/g, '');
            // UTF-8 encoded filename support (RFC 5987)
            if (filename.startsWith("UTF-8''")) {
              filename = decodeURIComponent(filename.substring(7));
            }
          }
        }
        
        // Download file
        const blob = await response.blob();
        const downloadUrl = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = downloadUrl;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(downloadUrl);
        
        return { success: true, filename };
      } catch (err: any) {
        this.error = err.message || 'Export işlemi sırasında bir hata oluştu.';
        console.error('Error exporting groups:', err);
        throw err;
      } finally {
        this.loading = false;
      }
    },
  },
});

