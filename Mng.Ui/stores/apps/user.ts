import { defineStore } from 'pinia';
import { fetchFromMngKeeper } from '@/services/apiService';

export interface User {
  id: string;
  userId?: string; // API response'da userId olabilir
  domainId: string;
  keycloakUserId?: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  groups: string[];
  roles?: string[];
  createdAt: string | Date;
  lastLoginAt?: string | Date | null;
  createdBy?: string;
  updatedAt?: string | Date | null;
  updatedBy?: string | null;
}

interface UserState {
  users: User[];
  currentUser: User | null;
  loading: boolean;
  error: string | null;
  totalCount: number;
}

export const useUserStore = defineStore('user', {
  state: (): UserState => ({
    users: [],
    currentUser: null,
    loading: false,
    error: null,
    totalCount: 0,
  }),

  getters: {
    activeUsers: (state): User[] => {
      return state.users.filter(user => user.isActive);
    },
    inactiveUsers: (state): User[] => {
      return state.users.filter(user => !user.isActive);
    },
    getUserById: (state) => {
      return (id: string) => state.users.find(user => user.id === id || user.userId === id);
    },
  },

  actions: {
    async fetchUsers(params?: { 
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
        if (params?.search) queryParams.append('searchTerm', params.search); // API'de searchTerm kullanılıyor
        if (params?.isActive !== undefined) queryParams.append('isActive', params.isActive.toString());
        
        const url = `/user${queryParams.toString() ? '?' + queryParams.toString() : ''}`;
        const response = await fetchFromMngKeeper(url, 'GET');
        
        console.log('API Response:', response); // Debug için
        
        // API response yapısı: GetUsersResponse { IsSuccess, Users, TotalCount, ErrorMessage }
        if (response.IsSuccess === false) {
          throw new Error(response.ErrorMessage || 'Kullanıcılar yüklenirken bir hata oluştu');
        }
        
        // Response.Users (büyük U) kontrolü
        if (response.Users && Array.isArray(response.Users)) {
          this.users = response.Users.map((user: any) => ({
            id: user.UserId || user.userId || '',
            userId: user.UserId || user.userId,
            domainId: user.domainId || '',
            keycloakUserId: user.keycloakUserId,
            username: user.Username || user.username || '',
            email: user.Email || user.email || '',
            firstName: user.FirstName || user.firstName || '',
            lastName: user.LastName || user.lastName || '',
            isActive: user.IsActive !== undefined ? user.IsActive : (user.isActive !== undefined ? user.isActive : true),
            groups: user.Groups || user.groups || [],
            roles: user.Roles || user.roles || [],
            createdAt: user.CreatedAt || user.createdAt || new Date(),
            lastLoginAt: user.LastLoginAt || user.lastLoginAt || null,
            createdBy: user.CreatedBy || user.createdBy,
            updatedAt: user.UpdatedAt || user.updatedAt || null,
            updatedBy: user.UpdatedBy || user.updatedBy || null,
          }));
          this.totalCount = response.TotalCount || response.totalCount || response.Users.length;
        } else if (response.users && Array.isArray(response.users)) {
          // Küçük harf ile dönerse (fallback)
          this.users = response.users.map((user: any) => ({
            id: user.userId || user.id || '',
            userId: user.userId || user.id,
            domainId: user.domainId || '',
            keycloakUserId: user.keycloakUserId,
            username: user.username || '',
            email: user.email || '',
            firstName: user.firstName || '',
            lastName: user.lastName || '',
            isActive: user.isActive !== undefined ? user.isActive : true,
            groups: user.groups || [],
            roles: user.roles || [],
            createdAt: user.createdAt || new Date(),
            lastLoginAt: user.lastLoginAt || null,
            createdBy: user.createdBy,
            updatedAt: user.updatedAt || null,
            updatedBy: user.updatedBy || null,
          }));
          this.totalCount = response.totalCount || response.users.length;
        } else if (Array.isArray(response)) {
          // Direkt array dönerse
          this.users = response.map((user: any) => ({
            id: user.UserId || user.userId || user.id || '',
            userId: user.UserId || user.userId || user.id,
            domainId: user.domainId || '',
            keycloakUserId: user.keycloakUserId,
            username: user.Username || user.username || '',
            email: user.Email || user.email || '',
            firstName: user.FirstName || user.firstName || '',
            lastName: user.LastName || user.lastName || '',
            isActive: user.IsActive !== undefined ? user.IsActive : (user.isActive !== undefined ? user.isActive : true),
            groups: user.Groups || user.groups || [],
            roles: user.Roles || user.roles || [],
            createdAt: user.CreatedAt || user.createdAt || new Date(),
            lastLoginAt: user.LastLoginAt || user.lastLoginAt || null,
            createdBy: user.CreatedBy || user.createdBy,
            updatedAt: user.UpdatedAt || user.updatedAt || null,
            updatedBy: user.UpdatedBy || user.updatedBy || null,
          }));
          this.totalCount = response.length;
        } else {
          console.warn('Unexpected API response format:', response);
          this.users = [];
          this.totalCount = 0;
        }
      } catch (error: any) {
        this.error = error.message || 'Kullanıcılar yüklenirken bir hata oluştu';
        console.error('Error fetching users:', error);
        this.users = [];
        this.totalCount = 0;
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async fetchUserById(userId: string) {
      this.loading = true;
      this.error = null;
      
      try {
        const response = await fetchFromMngKeeper(`/user/${userId}`, 'GET');
        
        console.log('GetUser API Response:', response); // Debug için
        
        // API response yapısı: GetUserResponse { IsSuccess, User, ErrorMessage }
        if (response.IsSuccess === false) {
          throw new Error(response.ErrorMessage || 'Kullanıcı bulunamadı');
        }
        
        // Response.User (büyük U) kontrolü
        const user = response.User || response.user;
        if (user) {
          this.currentUser = {
            id: user.UserId || user.userId || user.id || '',
            userId: user.UserId || user.userId || user.id,
            domainId: user.domainId || '',
            keycloakUserId: user.keycloakUserId,
            username: user.Username || user.username || '',
            email: user.Email || user.email || '',
            firstName: user.FirstName || user.firstName || '',
            lastName: user.LastName || user.lastName || '',
            isActive: user.IsActive !== undefined ? user.IsActive : (user.isActive !== undefined ? user.isActive : true),
            groups: user.Groups || user.groups || [],
            roles: user.Roles || user.roles || [],
            createdAt: user.CreatedAt || user.createdAt || new Date(),
            lastLoginAt: user.LastLoginAt || user.lastLoginAt || null,
            createdBy: user.CreatedBy || user.createdBy,
            updatedAt: user.UpdatedAt || user.updatedAt || null,
            updatedBy: user.UpdatedBy || user.updatedBy || null,
          };
          return this.currentUser;
        }
        
        throw new Error('Kullanıcı bulunamadı');
      } catch (error: any) {
        this.error = error.message || 'Kullanıcı yüklenirken bir hata oluştu';
        console.error('Error fetching user:', error);
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async createUser(userData: {
      username: string;
      email: string;
      password: string;
      firstName: string;
      lastName: string;
      groups?: string[];
      roles?: string[];
    }) {
      this.loading = true;
      this.error = null;
      
      try {
        const response = await fetchFromMngKeeper('/user', 'POST', userData);
        
        if (response.userId) {
          // Yeni kullanıcıyı listeye ekle
          const newUser: User = {
            id: response.userId,
            userId: response.userId,
            domainId: response.domainId || '',
            username: userData.username,
            email: userData.email,
            firstName: userData.firstName,
            lastName: userData.lastName,
            isActive: true,
            groups: userData.groups || [],
            roles: userData.roles || [],
            createdAt: new Date(),
          };
          
          this.users.push(newUser);
          return newUser;
        }
        
        throw new Error('Kullanıcı oluşturulamadı');
      } catch (error: any) {
        this.error = error.message || 'Kullanıcı oluşturulurken bir hata oluştu';
        console.error('Error creating user:', error);
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async updateUser(userId: string, userData: {
      email?: string;
      firstName?: string;
      lastName?: string;
      isActive?: boolean;
      groups?: string[];
      roles?: string[];
    }) {
      this.loading = true;
      this.error = null;
      
      try {
        const response = await fetchFromMngKeeper(`/user/${userId}`, 'PUT', userData);
        
        // Store'daki kullanıcıyı güncelle
        const index = this.users.findIndex(u => u.id === userId || u.userId === userId);
        if (index !== -1) {
          this.users[index] = {
            ...this.users[index],
            ...userData,
            updatedAt: new Date(),
          };
        }
        
        if (this.currentUser && (this.currentUser.id === userId || this.currentUser.userId === userId)) {
          this.currentUser = {
            ...this.currentUser,
            ...userData,
            updatedAt: new Date(),
          };
        }
        
        return response;
      } catch (error: any) {
        this.error = error.message || 'Kullanıcı güncellenirken bir hata oluştu';
        console.error('Error updating user:', error);
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async deleteUser(userId: string) {
      this.loading = true;
      this.error = null;
      
      try {
        await fetchFromMngKeeper(`/user/${userId}`, 'DELETE');
        
        // Store'dan kaldır
        this.users = this.users.filter(u => u.id !== userId && u.userId !== userId);
        
        if (this.currentUser && (this.currentUser.id === userId || this.currentUser.userId === userId)) {
          this.currentUser = null;
        }
      } catch (error: any) {
        this.error = error.message || 'Kullanıcı silinirken bir hata oluştu';
        console.error('Error deleting user:', error);
        throw error;
      } finally {
        this.loading = false;
      }
    },

    clearError() {
      this.error = null;
    },
  },
});

