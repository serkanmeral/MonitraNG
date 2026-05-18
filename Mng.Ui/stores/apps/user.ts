import { defineStore } from 'pinia';
import { fetchFromMngKeeper } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';

function getAccessToken(): string | null {
  const authStore = useAuthStore();
  return authStore.accessToken;
}

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
  currentUser: User | null; // Logged in user
  viewingUser: User | null; // Currently viewed/edited user
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
    currentUser: null, // Logged in user
    viewingUser: null, // Currently viewed/edited user
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
    /** Liste + son `fetchUserById` / `currentUser` (sohbet için JWT sub ile eşleşen satır). */
    getUserById: (state) => {
      return (id: string) => {
        const t = (id || '').trim();
        if (!t) return undefined;
        const tl = t.toLowerCase();
        const fromList = state.users.find(
          (user) =>
            (user.id && user.id.toLowerCase() === tl) ||
            (user.userId && user.userId.toLowerCase() === tl) ||
            (user.keycloakUserId != null && user.keycloakUserId.toLowerCase() === tl)
        );
        if (fromList) return fromList;
        const v = state.viewingUser;
        if (
          v &&
          ((v.id && v.id.toLowerCase() === tl) ||
            (v.userId && v.userId.toLowerCase() === tl) ||
            (v.keycloakUserId != null && v.keycloakUserId.toLowerCase() === tl))
        )
          return v;
        const c = state.currentUser;
        if (
          c &&
          ((c.id && c.id.toLowerCase() === tl) ||
            (c.userId && c.userId.toLowerCase() === tl) ||
            (c.keycloakUserId != null && c.keycloakUserId.toLowerCase() === tl))
        )
          return c;
        return undefined;
      };
    },
  },

  actions: {
    /**
     * `fetchUserById` ile gelen profili `users` içine yazar; böylece `getUserById` / sohbet `displayNameForStoredPersonId` çözümü çalışır.
     * (Önceden yalnızca `viewingUser` set ediliyordu, liste güncellenmiyordu.)
     */
    mergeResolvedUserProfile(mapped: User) {
      const keys = new Set<string>();
      for (const x of [mapped.id, mapped.userId, mapped.keycloakUserId]) {
        const t = String(x ?? '').trim().toLowerCase();
        if (t) keys.add(t);
      }
      if (!keys.size) return;
      const idx = this.users.findIndex((u) => {
        const ids = [u.id, u.userId, u.keycloakUserId]
          .filter(Boolean)
          .map((v) => String(v).toLowerCase());
        return ids.some((id) => keys.has(id));
      });
      if (idx >= 0) {
        this.users[idx] = { ...this.users[idx], ...mapped };
      } else {
        this.users.push(mapped);
      }
    },

    async fetchUsers(params?: { 
      page?: number; 
      pageSize?: number; 
      search?: string;
      isActive?: boolean;
      sortBy?: string;
      sortOrder?: 'asc' | 'desc';
    }) {
      this.loading = true;
      this.error = null;
      
      try {
        const queryParams = new URLSearchParams();
        if (params?.page) queryParams.append('page', params.page.toString());
        if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString());
        if (params?.search) queryParams.append('searchTerm', params.search); // API'de searchTerm kullanılıyor
        if (params?.isActive !== undefined) queryParams.append('isActive', params.isActive.toString());
        if (params?.sortBy) queryParams.append('sortBy', params.sortBy);
        if (params?.sortOrder) queryParams.append('sortOrder', params.sortOrder);
        
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
          
          // Create a map of existing users by ID to preserve nullable fields if API doesn't return them
          const existingUsersMap = new Map<string, any>();
          this.users.forEach(u => {
            if (u.id) existingUsersMap.set(u.id, u);
            if (u.userId && u.userId !== u.id) existingUsersMap.set(u.userId, u);
          });
          
          this.users = usersArray.map((user: any) => {
            // Try id first, then userId, then UserId (capital)
            const primaryId = user.id || user.Id || user.userId || user.UserId || '';
            const userIdForLookup = user.userId || user.UserId || user.id || user.Id || primaryId;
            
            // Find existing user in store to preserve nullable fields if API doesn't return them
            const existingUser = existingUsersMap.get(primaryId) || existingUsersMap.get(userIdForLookup);
            
            // Helper to preserve existing value if API returns null/undefined
            const preserveNullableField = (apiValue: any, existingValue: any) => {
              // If API explicitly provided a non-null value, use it
              if (apiValue !== undefined && apiValue !== null) {
                return apiValue;
              }
              // If API returned null/undefined, preserve existing value if it exists
              if (existingValue !== undefined && existingValue !== null) {
                return existingValue;
              }
              // Otherwise use API value (null or undefined)
              return apiValue ?? null;
            };
            
            const titleFromApi = user.title !== undefined ? user.title : user.Title;
            const departmentFromApi = user.department !== undefined ? user.department : user.Department;
            const phoneNumberFromApi = user.phoneNumber !== undefined ? user.phoneNumber : user.PhoneNumber;
            const photoUrlFromApi = user.photoUrl !== undefined ? user.photoUrl : user.PhotoUrl;
            
            const mapped = {
              id: primaryId,
              userId: user.userId || user.UserId || user.id || user.Id || primaryId,
              domainId: user.domainId || user.DomainId || '',
              keycloakUserId: user.keycloakUserId || user.KeycloakUserId,
              username: user.username || user.Username || '',
              email: user.email || user.Email || '',
              firstName: user.firstName || user.FirstName || '',
              lastName: user.lastName || user.LastName || '',
              // Preserve existing values if API returns null/undefined for nullable fields
              title: preserveNullableField(titleFromApi, existingUser?.title),
              department: preserveNullableField(departmentFromApi, existingUser?.department),
              phoneNumber: preserveNullableField(phoneNumberFromApi, existingUser?.phoneNumber),
              photoUrl: preserveNullableField(photoUrlFromApi, existingUser?.photoUrl),
              gender: user.gender !== undefined ? user.gender : (user.Gender !== undefined ? user.Gender : Gender.NotSpecified),
              isActive: user.isActive !== undefined ? user.isActive : (user.IsActive !== undefined ? user.IsActive : true),
              groups: user.groups || user.Groups || existingUser?.groups || [],
              roles: user.roles || user.Roles || [],
              createdAt: user.createdAt || user.CreatedAt || new Date(),
              lastLoginAt: user.lastLoginAt || user.LastLoginAt || null,
              createdBy: user.createdBy || user.CreatedBy,
              updatedAt: user.updatedAt || user.UpdatedAt || null,
              updatedBy: user.updatedBy || user.UpdatedBy || null,
            };
            return mapped;
          });
          
          this.totalCount = totalCountValue;
          this.page = pageValue;
          this.pageSize = pageSizeValue;
          this.totalPages = totalPagesValue;
        } else if (Array.isArray(response)) {
          // Direkt array dönerse
          // Try id first, then Id (capital), then userId, then UserId
          this.users = response.map((user: any) => {
            const primaryId = user.id || user.Id || user.userId || user.UserId || '';
            return {
              id: primaryId,
              userId: user.userId || user.UserId || user.id || user.Id || primaryId,
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
          });
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
        // First, try to find user in the current list (if already loaded)
        const uid = String(userId ?? '').trim();
        const ul = uid.toLowerCase();
        const existingUser = this.users.find(
          (u) =>
            (u.id && u.id.toLowerCase() === ul) ||
            (u.userId && u.userId.toLowerCase() === ul) ||
            (u.keycloakUserId != null && u.keycloakUserId.toLowerCase() === ul)
        );
        
        if (existingUser) {
          const userCopy = { ...existingUser };
          this.$patch({
            viewingUser: userCopy,
            loading: false,
          });
          return this.viewingUser;
        }
        
        // If not found in list, fetch from API
        const response = await fetchFromMngKeeper(`/user/${userId}`, 'GET');
        
        // API response yapısı: GetUserResponse { IsSuccess, User, ErrorMessage }
        if (response.IsSuccess === false) {
          throw new Error(response.ErrorMessage || 'Kullanıcı bulunamadı');
        }
        
        // Response.User (büyük U) kontrolü
        const user = response.User || response.user;
        
        if (user) {
          // Try id first, then Id (capital), then userId, then UserId
          const primaryId = user.id || user.Id || user.userId || user.UserId || '';
          const mappedUser = {
            id: primaryId,
            userId: user.userId || user.UserId || user.id || user.Id || primaryId,
            domainId: user.domainId || user.DomainId || '',
            keycloakUserId: user.keycloakUserId || user.KeycloakUserId,
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
          this.mergeResolvedUserProfile(mappedUser as User);
          this.$patch({
            viewingUser: mappedUser,
            loading: false,
          });
          return this.viewingUser;
        }
        throw new Error('Kullanıcı bulunamadı');
      } catch (error: any) {
        this.error = error.message || 'Kullanıcı yüklenirken bir hata oluştu';
        console.error('[fetchUserById] Error fetching user:', error);
        console.error('[fetchUserById] Error details:', {
          message: error.message,
          stack: error.stack,
          userId: userId
        });
        throw error;
      } finally {
        this.loading = false;
      }
    },

    async createUser(userData: {
      username: string;
      email: string;
      password?: string; // Optional - user can set password via reset password
      firstName: string;
      lastName: string;
      title?: string | null;
      department?: string | null;
      phoneNumber?: string | null;
      isActive?: boolean;
      groups?: string[];
      roles?: string[];
    }) {
      this.loading = true;
      this.error = null;
      
      try {
        // Prepare request data - only include fields that have values
        const requestData: any = {
          username: userData.username,
          email: userData.email,
          firstName: userData.firstName,
          lastName: userData.lastName,
          isActive: userData.isActive !== undefined ? userData.isActive : true,
        };
        
        // Only include password if provided (optional - user can set via reset password)
        if (userData.password) {
          requestData.password = userData.password;
        }
        
        // Only include nullable fields if they have values
        if (userData.title) {
          requestData.title = userData.title;
        }
        if (userData.department) {
          requestData.department = userData.department;
        }
        if (userData.phoneNumber) {
          requestData.phoneNumber = userData.phoneNumber;
        }
        if (userData.groups && userData.groups.length > 0) {
          requestData.groupIds = userData.groups;
        }
        if (userData.roles && userData.roles.length > 0) {
          requestData.roleIds = userData.roles;
        }
        
        const response = await fetchFromMngKeeper('/user', 'POST', requestData);
        
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
            title: userData.title || null,
            department: userData.department || null,
            phoneNumber: userData.phoneNumber || null,
            isActive: userData.isActive !== undefined ? userData.isActive : true,
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
        // Get the user being updated (viewingUser or find from list)
        // Use viewingUser first (if we're editing a user), then try to find from list
        const userBeingUpdated = this.viewingUser || this.users.find(u => u.id === userId || u.userId === userId);
        
        if (!userBeingUpdated) {
          throw new Error('Güncellenecek kullanıcı bulunamadı');
        }
        
        
        // Convert gender string to integer (backend expects 0, 1, 2)
        const genderValue = userData.gender || userBeingUpdated.gender || Gender.NotSpecified;
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
        // Use provided username/email or fallback to userBeingUpdated
        // IMPORTANT: Use userData.username if provided, otherwise use userBeingUpdated.username
        // This ensures we're updating the correct user, not the logged-in user
        const username = userData.username || userBeingUpdated.username || '';
        const email = userData.email || userBeingUpdated.email || '';
        
        if (!username) {
          throw new Error('Kullanıcı adı bulunamadı');
        }
        if (!email) {
          throw new Error('Email bulunamadı');
        }
        
        // Backend UpdateUserCommand requires Username and Email
        // Also, backend expects GroupIds, not groups
        // Gender must be integer (0, 1, 2)
        // For nullable fields: only include if userData has the field (not undefined)
        // If userData doesn't have the field, don't include it in request - backend will preserve existing value
        const requestData: any = {
          username: username,
          email: email,
          firstName: userData.firstName || userBeingUpdated.firstName || '',
          lastName: userData.lastName || userBeingUpdated.lastName || '',
          gender: genderInt, // Integer value (0, 1, 2)
          isActive: userData.isActive !== undefined ? userData.isActive : (userBeingUpdated.isActive ?? true),
        };
        
        // Only include nullable fields if explicitly provided in userData
        // If not provided, backend will preserve existing values
        if (userData.title !== undefined) {
          requestData.title = userData.title;
        }
        if (userData.department !== undefined) {
          requestData.department = userData.department;
        }
        if (userData.phoneNumber !== undefined) {
          requestData.phoneNumber = userData.phoneNumber;
        }
        if (userData.photoUrl !== undefined) {
          requestData.photoUrl = userData.photoUrl;
        }
        
        // Track whether groups were explicitly provided in the request
        // This helps us preserve existing groups if backend doesn't return them
        const groupsWereProvided = userData.groups !== undefined;
        
        // Only include groupIds if groups are explicitly provided in userData
        // If groups are not provided, don't send groupIds (backend will keep existing groups)
        if (groupsWereProvided) {
          requestData.groupIds = userData.groups;
        }
        // If userData.groups is undefined, don't include groupIds in request
        // This allows backend to preserve existing groups when only other fields are updated
        
        // Remove undefined values
        // Nullable fields are only added if userData has them, so we don't need special handling
        Object.keys(requestData).forEach(key => {
          if (requestData[key] === undefined) {
            delete requestData[key];
          }
        });
        
        const response = await fetchFromMngKeeper(`/user/${userId}`, 'PUT', requestData);
        
        // Response'dan güncellenmiş kullanıcı bilgilerini al
        // Response yapısı: UpdateUserResponse { UserId, Username, Email, FirstName, LastName, ... }
        const updatedUserData = response.User || response.user || response;
        
        // Helper function to preserve existing value if response is null/undefined
        // If response has a value (even null), use it only if it's not null
        // If response is undefined or null, preserve existing value
        const preserveIfNull = (responseValue: any, existingValue: any) => {
          if (responseValue !== undefined) {
            // Response'da değer var, null ise ve mevcut değer varsa koru
            if (responseValue === null && existingValue !== null && existingValue !== undefined) {
              return existingValue;
            }
            return responseValue;
          }
          // Response'da değer yok, mevcut değeri koru
          return existingValue;
        };
        
        // Store'daki kullanıcıyı güncelle - response'dan gelen değerleri kullan
        // Use nullish coalescing (??) to preserve existing values if response doesn't include the field
        const index = this.users.findIndex(u => u.id === userId || u.userId === userId);
        if (index !== -1) {
          const existingUser = this.users[index];
          
          // For groups: if groups were not provided in request, preserve existing groups
          // even if response doesn't include them or returns empty array
          let groupsValue: string[];
          if (groupsWereProvided) {
            // Groups were changed, use response value or fallback to existing
            groupsValue = updatedUserData.GroupIds ?? updatedUserData.groupIds ?? updatedUserData.Groups ?? updatedUserData.groups ?? existingUser.groups;
          } else {
            // Groups were not changed, always preserve existing groups
            groupsValue = existingUser.groups;
          }
          
          const titleFromResponse = updatedUserData.Title !== undefined ? updatedUserData.Title : updatedUserData.title;
          const departmentFromResponse = updatedUserData.Department !== undefined ? updatedUserData.Department : updatedUserData.department;
          const phoneNumberFromResponse = updatedUserData.PhoneNumber !== undefined ? updatedUserData.PhoneNumber : updatedUserData.phoneNumber;
          const photoUrlFromResponse = updatedUserData.PhotoUrl !== undefined ? updatedUserData.PhotoUrl : updatedUserData.photoUrl;
          
          const titleValue = preserveIfNull(titleFromResponse, existingUser.title);
          const departmentValue = preserveIfNull(departmentFromResponse, existingUser.department);
          const phoneNumberValue = preserveIfNull(phoneNumberFromResponse, existingUser.phoneNumber);
          const photoUrlValue = preserveIfNull(photoUrlFromResponse, existingUser.photoUrl);
          
          this.users[index] = {
            ...existingUser,
            id: updatedUserData.UserId ?? updatedUserData.userId ?? updatedUserData.id ?? existingUser.id,
            userId: updatedUserData.UserId ?? updatedUserData.userId ?? updatedUserData.id ?? existingUser.userId,
            username: updatedUserData.Username ?? updatedUserData.username ?? existingUser.username,
            email: updatedUserData.Email ?? updatedUserData.email ?? existingUser.email,
            firstName: updatedUserData.FirstName ?? updatedUserData.firstName ?? existingUser.firstName,
            lastName: updatedUserData.LastName ?? updatedUserData.lastName ?? existingUser.lastName,
            // For nullable fields, use !== undefined check to allow null values to be set
            title: titleValue,
            department: departmentValue,
            phoneNumber: phoneNumberValue,
            photoUrl: photoUrlValue,
            gender: updatedUserData.Gender !== undefined ? updatedUserData.Gender : (updatedUserData.gender !== undefined ? updatedUserData.gender : existingUser.gender),
            isActive: updatedUserData.IsActive !== undefined ? updatedUserData.IsActive : (updatedUserData.isActive !== undefined ? updatedUserData.isActive : existingUser.isActive),
            groups: groupsValue,
            updatedAt: updatedUserData.UpdatedAt ?? updatedUserData.updatedAt ?? new Date(),
          };
        }
        
        // Helper function to convert gender from integer to Gender enum
        const convertGender = (genderInt: number | undefined): Gender | 'NotSpecified' | 'Male' | 'Female' => {
          if (genderInt === undefined) return Gender.NotSpecified;
          if (typeof genderInt === 'number') {
            switch (genderInt) {
              case 1:
                return Gender.Male;
              case 2:
                return Gender.Female;
              case 0:
              default:
                return Gender.NotSpecified;
            }
          }
          return genderInt as Gender | 'NotSpecified' | 'Male' | 'Female';
        };
        
        // Update viewingUser (the user being edited)
        if (this.viewingUser && (this.viewingUser.id === userId || this.viewingUser.userId === userId)) {
          const genderValue = convertGender(updatedUserData.Gender !== undefined ? updatedUserData.Gender : updatedUserData.gender);
          const existingViewingUser = this.viewingUser;
          
          // For groups: if groups were not provided in request, preserve existing groups
          let viewingUserGroups: string[];
          if (groupsWereProvided) {
            viewingUserGroups = updatedUserData.GroupIds ?? updatedUserData.groupIds ?? updatedUserData.Groups ?? updatedUserData.groups ?? existingViewingUser.groups;
          } else {
            viewingUserGroups = existingViewingUser.groups;
          }
          
          // Apply same preserveIfNull logic for viewingUser
          const viewingUserTitleFromResponse = updatedUserData.Title !== undefined ? updatedUserData.Title : updatedUserData.title;
          const viewingUserDepartmentFromResponse = updatedUserData.Department !== undefined ? updatedUserData.Department : updatedUserData.department;
          const viewingUserPhoneNumberFromResponse = updatedUserData.PhoneNumber !== undefined ? updatedUserData.PhoneNumber : updatedUserData.phoneNumber;
          const viewingUserPhotoUrlFromResponse = updatedUserData.PhotoUrl !== undefined ? updatedUserData.PhotoUrl : updatedUserData.photoUrl;
          
          this.viewingUser = {
            ...existingViewingUser,
            id: updatedUserData.UserId ?? updatedUserData.userId ?? updatedUserData.id ?? existingViewingUser.id,
            userId: updatedUserData.UserId ?? updatedUserData.userId ?? updatedUserData.id ?? existingViewingUser.userId,
            username: updatedUserData.Username ?? updatedUserData.username ?? existingViewingUser.username,
            email: updatedUserData.Email ?? updatedUserData.email ?? existingViewingUser.email,
            firstName: updatedUserData.FirstName ?? updatedUserData.firstName ?? existingViewingUser.firstName,
            lastName: updatedUserData.LastName ?? updatedUserData.lastName ?? existingViewingUser.lastName,
            // Use preserveIfNull to preserve existing values if response is null
            title: preserveIfNull(viewingUserTitleFromResponse, existingViewingUser.title),
            department: preserveIfNull(viewingUserDepartmentFromResponse, existingViewingUser.department),
            phoneNumber: preserveIfNull(viewingUserPhoneNumberFromResponse, existingViewingUser.phoneNumber),
            photoUrl: preserveIfNull(viewingUserPhotoUrlFromResponse, existingViewingUser.photoUrl),
            gender: genderValue,
            isActive: updatedUserData.IsActive !== undefined ? updatedUserData.IsActive : (updatedUserData.isActive !== undefined ? updatedUserData.isActive : existingViewingUser.isActive),
            groups: viewingUserGroups,
            updatedAt: updatedUserData.UpdatedAt ?? updatedUserData.updatedAt ?? new Date(),
          };
        }
        
        // Update currentUser only if the logged-in user is updating themselves
        if (this.currentUser && (this.currentUser.id === userId || this.currentUser.userId === userId)) {
          const genderValue = convertGender(updatedUserData.Gender !== undefined ? updatedUserData.Gender : updatedUserData.gender);
          const existingCurrentUser = this.currentUser;
          
          // For groups: if groups were not provided in request, preserve existing groups
          let currentUserGroups: string[];
          if (groupsWereProvided) {
            currentUserGroups = updatedUserData.GroupIds ?? updatedUserData.groupIds ?? updatedUserData.Groups ?? updatedUserData.groups ?? existingCurrentUser.groups;
          } else {
            currentUserGroups = existingCurrentUser.groups;
          }
          
          // Apply same preserveIfNull logic for currentUser
          const currentUserTitleFromResponse = updatedUserData.Title !== undefined ? updatedUserData.Title : updatedUserData.title;
          const currentUserDepartmentFromResponse = updatedUserData.Department !== undefined ? updatedUserData.Department : updatedUserData.department;
          const currentUserPhoneNumberFromResponse = updatedUserData.PhoneNumber !== undefined ? updatedUserData.PhoneNumber : updatedUserData.phoneNumber;
          const currentUserPhotoUrlFromResponse = updatedUserData.PhotoUrl !== undefined ? updatedUserData.PhotoUrl : updatedUserData.photoUrl;
          
          this.currentUser = {
            ...existingCurrentUser,
            id: updatedUserData.UserId ?? updatedUserData.userId ?? updatedUserData.id ?? existingCurrentUser.id,
            userId: updatedUserData.UserId ?? updatedUserData.userId ?? updatedUserData.id ?? existingCurrentUser.userId,
            username: updatedUserData.Username ?? updatedUserData.username ?? existingCurrentUser.username,
            email: updatedUserData.Email ?? updatedUserData.email ?? existingCurrentUser.email,
            firstName: updatedUserData.FirstName ?? updatedUserData.firstName ?? existingCurrentUser.firstName,
            lastName: updatedUserData.LastName ?? updatedUserData.lastName ?? existingCurrentUser.lastName,
            // Use preserveIfNull to preserve existing values if response is null
            title: preserveIfNull(currentUserTitleFromResponse, existingCurrentUser.title),
            department: preserveIfNull(currentUserDepartmentFromResponse, existingCurrentUser.department),
            phoneNumber: preserveIfNull(currentUserPhoneNumberFromResponse, existingCurrentUser.phoneNumber),
            photoUrl: preserveIfNull(currentUserPhotoUrlFromResponse, existingCurrentUser.photoUrl),
            gender: genderValue,
            isActive: updatedUserData.IsActive !== undefined ? updatedUserData.IsActive : (updatedUserData.isActive !== undefined ? updatedUserData.isActive : existingCurrentUser.isActive),
            groups: currentUserGroups,
            updatedAt: updatedUserData.UpdatedAt ?? updatedUserData.updatedAt ?? new Date(),
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

    /**
     * Request password reset for a user (sends email with reset link)
     */
    async requestPasswordReset(userId: string): Promise<{ isSuccess: boolean; message?: string; error?: string }> {
      this.loading = true;
      this.error = null;
      
      try {
        const response = await fetchFromMngKeeper(`/user/${userId}/request-password-reset`, 'POST');
        
        if (response.isSuccess) {
          return { isSuccess: true, message: response.message || 'Password reset email sent successfully.' };
        } else {
          const errorMsg = response.errorDescription || response.error || 'Failed to send password reset email.';
          this.error = errorMsg;
          return { isSuccess: false, error: errorMsg };
        }
      } catch (error: any) {
        const errorMsg = error.message || 'Failed to send password reset email.';
        this.error = errorMsg;
        console.error('Error requesting password reset:', error);
        return { isSuccess: false, error: errorMsg };
      } finally {
        this.loading = false;
      }
    },

    /**
     * Reset password using reset token
     * Note: This endpoint is public and does not require authentication token
     */
    async resetPassword(token: string, newPassword: string): Promise<{ isSuccess: boolean; message?: string; error?: string }> {
      this.loading = true;
      this.error = null;
      
      try {
        // This is a public endpoint, so we call it directly without token
        const response = await $fetch('/api/keeper/auth/reset-password', {
          method: 'POST',
          body: {
            token,
            newPassword,
          },
        });
        
        if (response.message) {
          return { isSuccess: true, message: response.message };
        } else {
          const errorMsg = response.errorDescription || response.error || 'Failed to reset password.';
          this.error = errorMsg;
          return { isSuccess: false, error: errorMsg };
        }
      } catch (error: any) {
        // Handle H3 errors (from server API route)
        let errorMsg = 'Failed to reset password.';
        
        if (error.data) {
          const errorData = error.data;
          if (typeof errorData === 'object') {
            errorMsg = errorData.errorDescription || errorData.error || errorData.message || errorMsg;
          } else if (typeof errorData === 'string') {
            errorMsg = errorData;
          }
        } else if (error.statusMessage) {
          errorMsg = error.statusMessage;
        } else if (error.message) {
          errorMsg = error.message;
        }
        
        this.error = errorMsg;
        console.error('Error resetting password:', error);
        return { isSuccess: false, error: errorMsg };
      } finally {
        this.loading = false;
      }
    },

    /**
     * Export users to CSV, XLSX or JSON format (backend endpoint)
     */
    async exportUsers(format: 'csv' | 'xlsx' | 'json', params?: { 
      search?: string;
      isActive?: boolean;
      sortBy?: string;
      sortOrder?: 'asc' | 'desc';
    }) {
      this.loading = true;
      this.error = null;
      
      try {
        // Build query string for export endpoint
        const queryParams = new URLSearchParams();
        queryParams.append('format', format);
        
        if (params?.search) {
          queryParams.append('searchTerm', params.search);
        }
        
        if (params?.isActive !== undefined) {
          queryParams.append('isActive', params.isActive.toString());
        }
        
        if (params?.sortBy) {
          queryParams.append('sortBy', params.sortBy);
        }
        
        if (params?.sortOrder) {
          queryParams.append('sortOrder', params.sortOrder);
        }
        
        // Call backend export endpoint with blob response
        const url = `/api/keeper/user/export?${queryParams.toString()}`;
        const token = getAccessToken();
        const headers: HeadersInit = {
          'Content-Type': 'application/json',
        };
        if (token) {
          headers['Authorization'] = `Bearer ${token}`;
        }
        
        const response = await fetch(url, {
          method: 'GET',
          headers,
        });
        
        if (!response.ok) {
          const errorText = await response.text();
          throw new Error(errorText || 'Export işlemi başarısız oldu');
        }
        
        // Create a download link from blob response
        const blob = await response.blob();
        const downloadUrl = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = downloadUrl;
        
        // Generate filename based on format and current date
        const timestamp = new Date().toISOString().split('T')[0];
        const extension = format === 'csv' ? 'csv' : format === 'json' ? 'json' : 'xlsx';
        link.download = `kullanicilar_${timestamp}.${extension}`;
        
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(downloadUrl);
      } catch (error: any) {
        const errorMsg = error.message || 'Export işlemi başarısız oldu';
        this.error = errorMsg;
        console.error('Error exporting users:', error);
        throw error;
      } finally {
        this.loading = false;
      }
    },
  },
});

