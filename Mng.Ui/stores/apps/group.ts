import { defineStore } from 'pinia';
import { fetchFromMngKeeper } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';
import {
  mapGroupProvisioningFromApi,
  type GroupCapabilities,
} from '@/utils/groupFieldPolicy';

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
  includeInApplication?: boolean;
  createdAt: string | Date;
  updatedAt?: string | Date | null;
  createdBy?: string;
  updatedBy?: string | null;
  provisioningSource?: string;
  directorySyncedAt?: string | Date | null;
  capabilities?: GroupCapabilities;
}

function mapApiGroupToGroup(group: Record<string, unknown>): Group {
  const provisioning = mapGroupProvisioningFromApi(group);
  const caps = provisioning.capabilities;
  return {
    id: String(group.groupId || group.GroupId || group.id || ''),
    groupId: String(group.groupId || group.GroupId || group.id || ''),
    name: String(group.name || group.Name || ''),
    description: (group.description ?? group.Description ?? null) as string | null,
    memberCount: Number(group.memberCount ?? group.MemberCount ?? 0),
    isActive:
      group.isActive !== undefined
        ? Boolean(group.isActive)
        : group.IsActive !== undefined
          ? Boolean(group.IsActive)
          : true,
    includeInApplication:
      group.includeInApplication !== undefined
        ? Boolean(group.includeInApplication)
        : group.IncludeInApplication !== undefined
          ? Boolean(group.IncludeInApplication)
          : true,
    createdAt: (group.createdAt || group.CreatedAt || new Date()) as string | Date,
    updatedAt: (group.updatedAt || group.UpdatedAt || null) as string | Date | null,
    createdBy: (group.createdBy || group.CreatedBy) as string | undefined,
    updatedBy: (group.updatedBy || group.UpdatedBy || null) as string | null,
    ...provisioning,
    capabilities: caps,
  };
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
      includeInApplication?: boolean;
      provisioningSource?: number;
      sortBy?: string;
      sortOrder?: 'asc' | 'desc';
    }) {
      this.loading = true;
      this.error = null;

      try {
        const queryParams = new URLSearchParams();
        if (params?.page) queryParams.append('page', params.page.toString());
        if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
        if (params?.search) queryParams.append('searchTerm', params.search);
        if (params?.isActive !== undefined) queryParams.append('isActive', params.isActive.toString());
        if (params?.includeInApplication !== undefined) {
          queryParams.append('includeInApplication', params.includeInApplication.toString());
        }
        if (params?.provisioningSource !== undefined) {
          queryParams.append('provisioningSource', params.provisioningSource.toString());
        }
        if (params?.sortBy) queryParams.append('sortBy', params.sortBy);
        if (params?.sortOrder) queryParams.append('sortOrder', params.sortOrder);
        
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
          this.groups = groupsArray.map((group: Record<string, unknown>) =>
            mapApiGroupToGroup(group)
          );
          
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

    /** Keeper gruplarını sayfalı API üzerinden tamamını yükler (MaxPageSize=100). */
    async fetchAllGroups(params?: { search?: string; isActive?: boolean }) {
      this.loading = true;
      this.error = null;

      try {
        const pageSize = 100;
        let page = 1;
        let totalPages = 1;
        const aggregated: Group[] = [];

        while (page <= totalPages) {
          const queryParams = new URLSearchParams();
          queryParams.append('page', page.toString());
          queryParams.append('pageSize', pageSize.toString());
          if (params?.search) queryParams.append('searchTerm', params.search);
          if (params?.isActive !== undefined) {
            queryParams.append('isActive', params.isActive.toString());
          }

          const url = `/group?${queryParams.toString()}`;
          const response = await fetchFromMngKeeper(url, 'GET');

          if (response.IsSuccess === false) {
            throw new Error(response.ErrorMessage || 'Gruplar yüklenirken bir hata oluştu');
          }

          const groupsArray = response.groups || response.Groups;
          totalPages = response.totalPages ?? response.TotalPages ?? 1;

          if (groupsArray && Array.isArray(groupsArray)) {
            aggregated.push(
              ...groupsArray.map((group: Record<string, unknown>) => mapApiGroupToGroup(group))
            );
          }

          page += 1;
        }

        this.groups = aggregated;
        this.totalCount = aggregated.length;
        this.page = 1;
        this.pageSize = aggregated.length || pageSize;
        this.totalPages = 1;
      } catch (err: any) {
        this.error = err.message || 'Gruplar yüklenirken bir hata oluştu.';
      } finally {
        this.loading = false;
      }
    },

    /** Picker / atama formları — yalnızca aktif ve uygulama kapsamındaki gruplar. */
    async fetchGroupsForSelection(params?: {
      page?: number;
      pageSize?: number;
      search?: string;
    }) {
      return this.fetchGroups({
        ...params,
        isActive: true,
        includeInApplication: true,
      });
    },

    /** Toplu etiket çözümü (modal picker, chip gösterimi). */
    async fetchGroupsByIds(ids: string[]): Promise<Group[]> {
      const unique = [...new Set(ids.map((id) => String(id ?? '').trim()).filter(Boolean))];
      if (!unique.length) return [];

      const response = await fetchFromMngKeeper('/group/by-ids', 'POST', { Ids: unique });
      if (response.IsSuccess === false) {
        throw new Error(response.ErrorMessage || 'Gruplar çözümlenemedi');
      }

      const groupsArray = (response.groups || response.Groups || []) as Record<string, unknown>[];
      const mapped: Group[] = [];
      for (const item of groupsArray) {
        const group = mapApiGroupToGroup({
          groupId: item.groupId ?? item.GroupId,
          id: item.groupId ?? item.GroupId,
          name: item.name ?? item.Name ?? '',
          isActive: item.isActive ?? item.IsActive ?? true,
          memberCount: item.memberCount ?? item.MemberCount ?? 0,
        });
        const idx = this.groups.findIndex(
          (g) => g.id === group.id || g.groupId === group.groupId
        );
        if (idx >= 0) {
          this.groups[idx] = { ...this.groups[idx], ...group };
        } else {
          this.groups.push(group);
        }
        mapped.push(group);
      }
      return mapped;
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
          const group = mapApiGroupToGroup(groupData as Record<string, unknown>);
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

    async updateGroup(groupId: string, groupData: {
      name: string;
      description?: string;
      isActive?: boolean;
      includeInApplication?: boolean;
      permissions?: string[];
    }) {
      this.loading = true;
      this.error = null;
      
      try {
        const requestBody: Record<string, unknown> = {
          Name: groupData.name,
          Description: groupData.description || '',
          IsActive: groupData.isActive !== undefined ? groupData.isActive : true,
          Permissions: groupData.permissions || [],
        };
        if (groupData.includeInApplication !== undefined) {
          requestBody.IncludeInApplication = groupData.includeInApplication;
        }
        
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

    async updateGroupApplicationScope(groupId: string, includeInApplication: boolean) {
      this.loading = true;
      this.error = null;
      try {
        const response = await fetchFromMngKeeper(
          `/group/${groupId}/application-scope`,
          'PATCH',
          { includeInApplication }
        );
        if (response.isSuccess === false || response.IsSuccess === false) {
          throw new Error(response.errorMessage || response.ErrorMessage || 'Grup kapsamı güncellenemedi');
        }
        if (this.currentGroup && (this.currentGroup.id === groupId || this.currentGroup.groupId === groupId)) {
          this.currentGroup.includeInApplication = includeInApplication;
        }
        return response;
      } catch (err: any) {
        this.error = err.message || 'Grup kapsamı güncellenirken bir hata oluştu.';
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

