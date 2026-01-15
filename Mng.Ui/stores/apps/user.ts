import { defineStore } from 'pinia';
import { fetchFromMngKeeper } from '@/services/apiService';

export enum Gender {
  NotSpecified = 'NotSpecified',
  Male = 'Male',
  Female = 'Female'
}

export enum Gender {
  NotSpecified = 'NotSpecified',
  Male = 'Male',
  Female = 'Female'
}

export interface User {
  id: string;
  userId?: string; // API response'da userId olabilir
  domainId: string;
  keycloakUserId?: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  title?: string | null; // Unvan/İş Unvanı
  department?: string | null; // Departman
  gender?: Gender | 'NotSpecified' | 'Male' | 'Female'; // Cinsiyet
  phoneNumber?: string | null; // Telefon Numarası
  photoUrl?: string | null; // Profil Fotoğrafı URL (MinIO)
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
  page: number;
  pageSize: number;
  totalPages: number;
}

export const useUserStore = defineStore('user', {
  state: (): UserState => ({
    users: [],
    currentUser: null,
    loading: false,
    error: null,
    totalCount: 0,
    page: 1,
    pageSize: 10,
    totalPages: 1,
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
        
        // API response yapısı kontrolü: Hem büyük harf (IsSuccess, Users, TotalCount) hem küçük harf (users, totalCount) destekleniyor
        if (response.IsSuccess === false) {
          throw new Error(response.ErrorMessage || 'Kullanıcılar yüklenirken bir hata oluştu');
        }
        
        // Önce küçük harf kontrolü (API'den gelen format: users, totalCount, page, pageSize, totalPages)
        const usersArray = response.users || response.Users;
        const totalCountValue = response.totalCount ?? response.TotalCount ?? 0;
        const pageValue = response.page ?? response.Page ?? 1;
        const pageSizeValue = response.pageSize ?? response.PageSize ?? 10;
        const totalPagesValue = response.totalPages ?? response.TotalPages ?? 1;
        
        // Response.users (küçük harf) veya Response.Users (büyük harf) kontrolü
        if (usersArray && Array.isArray(usersArray)) {
          this.users = usersArray.map((user: any) => ({
            id: user.userId || user.UserId || user.id || '',
            userId: user.userId || user.UserId || user.id,
            domainId: user.domainId || user.DomainId || '',
            keycloakUserId: user.keycloakUserId || user.KeycloakUserId,
            username: user.username || user.Username || '',
            email: user.email || user.Email || '',
            firstName: user.firstName || user.FirstName || '',
            lastName: user.lastName || user.LastName || '',
            title: user.title || user.Title || null,
            department: user.department || user.Department || null,
            gender: user.gender !== undefined ? user.gender : (user.Gender !== undefined ? user.Gender : Gender.NotSpecified),
            phoneNumber: user.phoneNumber || user.PhoneNumber || null,
            photoUrl: user.photoUrl || user.PhotoUrl || null,
            isActive: user.isActive !== undefined ? user.isActive : (user.IsActive !== undefined ? user.IsActive : true),
            groups: user.groups || user.Groups || [],
            roles: user.roles || user.Roles || [],
            createdAt: user.createdAt || user.CreatedAt || new Date(),
            lastLoginAt: user.lastLoginAt || user.LastLoginAt || null,
            createdBy: user.createdBy || user.CreatedBy,
            updatedAt: user.updatedAt || user.UpdatedAt || null,
            updatedBy: user.updatedBy || user.UpdatedBy || null,
          }));
          this.totalCount = totalCountValue;
          this.page = pageValue;
          this.pageSize = pageSizeValue;
          this.totalPages = totalPagesValue;
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
            title: user.title || user.Title || null,
            department: user.department || user.Department || null,
            gender: user.gender !== undefined ? user.gender : (user.Gender !== undefined ? user.Gender : Gender.NotSpecified),
            phoneNumber: user.phoneNumber || user.PhoneNumber || null,
            photoUrl: user.photoUrl || user.PhotoUrl || null,
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
      username?: string;
      email?: string;
      firstName?: string;
      lastName?: string;
      title?: string;
      department?: string;
      gender?: Gender | 'NotSpecified' | 'Male' | 'Female';
      phoneNumber?: string;
      photoUrl?: string;
      isActive?: boolean;
      groups?: string[];
      roles?: string[];
    }) {
      this.loading = true;
      this.error = null;
      
      try {
        // Get current user to include required fields (Username, Email)
        const currentUser = this.currentUser || this.users.find(u => u.id === userId || u.userId === userId);
        
        // Convert gender string to integer (backend expects 0, 1, 2)
        const genderValue = userData.gender || currentUser?.gender || Gender.NotSpecified;
        let genderInt: number;
        if (typeof genderValue === 'string') {
          switch (genderValue) {
            case 'Male':
              genderInt = 1;
              break;
            case 'Female':
              genderInt = 2;
              break;
            case 'NotSpecified':
            default:
              genderInt = 0;
              break;
          }
        } else {
          // Already a Gender enum (number)
          genderInt = genderValue as number;
        }
        
        // Backend UpdateUserCommand requires Username and Email
        // Also, backend expects GroupIds, not groups
        // Gender must be integer (0, 1, 2)
        const requestData: any = {
          username: userData.username || currentUser?.username || '',
          email: userData.email || currentUser?.email || '',
          firstName: userData.firstName || currentUser?.firstName || '',
          lastName: userData.lastName || currentUser?.lastName || '',
          title: userData.title,
          department: userData.department,
          gender: genderInt, // Integer value (0, 1, 2)
          phoneNumber: userData.phoneNumber,
          photoUrl: userData.photoUrl,
          isActive: userData.isActive !== undefined ? userData.isActive : (currentUser?.isActive ?? true),
        };
        
        // Only include groupIds if groups are explicitly provided in userData
        // If groups are not provided, don't send groupIds (backend will keep existing groups)
        if (userData.groups !== undefined) {
          requestData.groupIds = userData.groups;
        }
        // If userData.groups is undefined, don't include groupIds in request
        // This allows backend to preserve existing groups when only other fields are updated
        
        // Remove undefined values
        Object.keys(requestData).forEach(key => {
          if (requestData[key] === undefined) {
            delete requestData[key];
          }
        });
        
        const response = await fetchFromMngKeeper(`/user/${userId}`, 'PUT', requestData);
        
        // Response'dan güncellenmiş kullanıcı bilgilerini al
        // Response yapısı: UpdateUserResponse { UserId, Username, Email, FirstName, LastName, ... }
        const updatedUserData = response.User || response.user || response;
        
        // Store'daki kullanıcıyı güncelle - response'dan gelen değerleri kullan
        const index = this.users.findIndex(u => u.id === userId || u.userId === userId);
        if (index !== -1) {
          this.users[index] = {
            ...this.users[index],
            id: updatedUserData.UserId || updatedUserData.userId || updatedUserData.id || this.users[index].id,
            userId: updatedUserData.UserId || updatedUserData.userId || updatedUserData.id || this.users[index].userId,
            username: updatedUserData.Username || updatedUserData.username || this.users[index].username,
            email: updatedUserData.Email || updatedUserData.email || this.users[index].email,
            firstName: updatedUserData.FirstName || updatedUserData.firstName || this.users[index].firstName,
            lastName: updatedUserData.LastName || updatedUserData.lastName || this.users[index].lastName,
            title: updatedUserData.Title || updatedUserData.title !== undefined ? updatedUserData.title : this.users[index].title,
            department: updatedUserData.Department || updatedUserData.department !== undefined ? updatedUserData.department : this.users[index].department,
            gender: updatedUserData.Gender !== undefined ? updatedUserData.Gender : (updatedUserData.gender !== undefined ? updatedUserData.gender : this.users[index].gender),
            phoneNumber: updatedUserData.PhoneNumber || updatedUserData.phoneNumber !== undefined ? updatedUserData.phoneNumber : this.users[index].phoneNumber,
            photoUrl: updatedUserData.PhotoUrl || updatedUserData.photoUrl !== undefined ? updatedUserData.photoUrl : this.users[index].photoUrl,
            isActive: updatedUserData.IsActive !== undefined ? updatedUserData.IsActive : (updatedUserData.isActive !== undefined ? updatedUserData.isActive : this.users[index].isActive),
            groups: updatedUserData.GroupIds || updatedUserData.groupIds || updatedUserData.Groups || updatedUserData.groups || this.users[index].groups,
            updatedAt: updatedUserData.UpdatedAt || updatedUserData.updatedAt || new Date(),
          };
        }
        
        // currentUser'ı güncelle - response'dan gelen değerleri kullan
        if (this.currentUser && (this.currentUser.id === userId || this.currentUser.userId === userId)) {
          // Convert gender from integer to Gender enum/string if needed
          let genderValue: Gender | 'NotSpecified' | 'Male' | 'Female' = this.currentUser.gender || Gender.NotSpecified;
          if (updatedUserData.Gender !== undefined || updatedUserData.gender !== undefined) {
            const genderInt = updatedUserData.Gender !== undefined ? updatedUserData.Gender : updatedUserData.gender;
            if (typeof genderInt === 'number') {
              // Backend returns integer (0, 1, 2), convert to Gender enum
              switch (genderInt) {
                case 1:
                  genderValue = Gender.Male;
                  break;
                case 2:
                  genderValue = Gender.Female;
                  break;
                case 0:
                default:
                  genderValue = Gender.NotSpecified;
                  break;
              }
            } else {
              genderValue = genderInt as Gender | 'NotSpecified' | 'Male' | 'Female';
            }
          }
          
          this.currentUser = {
            ...this.currentUser,
            id: updatedUserData.UserId || updatedUserData.userId || updatedUserData.id || this.currentUser.id,
            userId: updatedUserData.UserId || updatedUserData.userId || updatedUserData.id || this.currentUser.userId,
            username: updatedUserData.Username || updatedUserData.username || this.currentUser.username,
            email: updatedUserData.Email || updatedUserData.email || this.currentUser.email,
            firstName: updatedUserData.FirstName || updatedUserData.firstName || this.currentUser.firstName,
            lastName: updatedUserData.LastName || updatedUserData.lastName || this.currentUser.lastName,
            title: updatedUserData.Title || updatedUserData.title !== undefined ? updatedUserData.title : this.currentUser.title,
            department: updatedUserData.Department || updatedUserData.department !== undefined ? updatedUserData.department : this.currentUser.department,
            gender: genderValue,
            phoneNumber: updatedUserData.PhoneNumber || updatedUserData.phoneNumber !== undefined ? updatedUserData.phoneNumber : this.currentUser.phoneNumber,
            photoUrl: updatedUserData.PhotoUrl || updatedUserData.photoUrl !== undefined ? updatedUserData.photoUrl : this.currentUser.photoUrl,
            isActive: updatedUserData.IsActive !== undefined ? updatedUserData.IsActive : (updatedUserData.isActive !== undefined ? updatedUserData.isActive : this.currentUser.isActive),
            groups: updatedUserData.GroupIds || updatedUserData.groupIds || updatedUserData.Groups || updatedUserData.groups || this.currentUser.groups,
            updatedAt: updatedUserData.UpdatedAt || updatedUserData.updatedAt || new Date(),
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

