import { defineStore } from "pinia";
import { loginToMngKeeper, refreshMngKeeperToken, revokeMngKeeperToken, type TokenResponse } from "@/services/apiService";
import { decodeJwt } from "jose";
import { isTokenExpired } from "@/utils/tokenUtils";
import type { Domain } from "@/composables/useDomain";
import { useDomain } from "@/composables/useDomain";

export interface UserInfo {
  sub: string;
  username: string;
  email?: string;
  given_name?: string; // First name from Keycloak
  family_name?: string; // Last name from Keycloak
  name?: string; // Full name
  preferred_username?: string;
  user_groups?: string[];
  isAdmin?: boolean;
  is_manager?: boolean; // Manager privilege (snake_case for consistency with is_admin)
  domain_id?: string;
  domain_name?: string;
  /** Keeper @users Mongo id (MngKeeper JWT claim `mng_person_id`). */
  mng_person_id?: string;
  [key: string]: any;
}

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  userInfo: UserInfo | null;
  domainInfo: Domain | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  isRefreshing: boolean;
  refreshPromise: Promise<boolean> | null;
}

export const useAuthStore = defineStore("auth", {
  state: (): AuthState => ({
    accessToken: null,
    refreshToken: null,
    userInfo: null,
    domainInfo: null,
    isAuthenticated: false,
    isLoading: false,
    isRefreshing: false,
    refreshPromise: null,
  }),

  getters: {
    isAdmin: (state): boolean => {
      return state.userInfo?.isAdmin === true || false;
    },
    isManager: (state): boolean => {
      // Admin users automatically have Manager privileges
      return state.userInfo?.isAdmin === true || state.userInfo?.is_manager === true || false;
    },
    userGroups: (state): string[] => {
      // Try multiple possible field names for user groups
      const groups = state.userInfo?.user_groups 
        || state.userInfo?.userGroups 
        || state.userInfo?.groups 
        || state.userInfo?.roles
        || [];
      
      // Ensure it's an array
      if (Array.isArray(groups)) {
        return groups;
      }
      
      // If it's a string, split by comma or return empty array
      if (typeof groups === 'string') {
        const splitGroups = groups.split(',').map(g => g.trim()).filter(g => g.length > 0);
        return splitGroups;
      }
      
      return [];
    },
    domainName: (state): string | undefined => {
      return state.userInfo?.domain_name;
    },
  },

  actions: {
    async login(username: string, password: string, domain?: string) {
      this.isLoading = true;
      try {
        const response: TokenResponse = await loginToMngKeeper(username, password, domain);
        
        // Store tokens
        this.accessToken = response.accessToken;
        this.refreshToken = response.refreshToken;
        
        // Store tokens in cookies
        const accessTokenCookie = useCookie("access_token", {
          maxAge: response.expiresIn || 300, // Default 5 minutes
          secure: process.env.NODE_ENV === 'production',
          sameSite: "strict",
        });
        const refreshTokenCookie = useCookie("refresh_token", {
          maxAge: response.refreshExpiresIn || 1800, // Default 30 minutes
          secure: process.env.NODE_ENV === 'production',
          sameSite: "strict",
        });
        
        accessTokenCookie.value = response.accessToken;
        refreshTokenCookie.value = response.refreshToken;
        
        // Decode and store user info
        try {
          const decoded = decodeJwt(response.accessToken) as any;
          
          // Normalize field names: is_admin -> isAdmin
          const normalizedUserInfo: UserInfo = {
            ...decoded,
            isAdmin: decoded.isAdmin === true || decoded.is_admin === true || false,
            is_manager: decoded.is_manager === true || decoded.isManager === true || false,
          };
          
          this.userInfo = normalizedUserInfo;
          this.isAuthenticated = true;
          
          // Store user info in localStorage
          localStorage.setItem("userInfo", JSON.stringify(normalizedUserInfo));
          
          // Load domain information after successful login
          try {
            await this.loadDomainInfo();
          } catch (domainError) {
            // Domain bilgisi yüklenemezse hata verme, sadece logla
            console.warn('Domain bilgisi yüklenemedi:', domainError);
          }
          
          // Load user preferences after successful login
          if (process.client) {
            try {
              const { useUserPreferencesStore } = await import('@/stores/apps/userPreferences');
              const preferencesStore = useUserPreferencesStore();
              const userId = normalizedUserInfo.sub || normalizedUserInfo.username;
              if (userId) {
                const prefs = await preferencesStore.loadPreferences(userId);
                if (prefs) {
                  preferencesStore.applyPreferences(prefs);
                }
              }
            } catch (prefError) {
              // Preferences yüklenemezse hata verme, sadece logla
              // Dataset henüz oluşturulmamış olabilir
              console.warn('Kullanıcı tercihleri yüklenemedi:', prefError);
            }
          }
        } catch (error) {
          throw new Error("Token decode hatası");
        }
        
        return { success: true };
      } catch (error) {
        this.isAuthenticated = false;
        this.accessToken = null;
        this.refreshToken = null;
        this.userInfo = null;
        
        if (error instanceof Error) {
          throw error;
        }
        throw new Error("Giriş başarısız");
      } finally {
        this.isLoading = false;
      }
    },

    async refreshAccessToken(): Promise<boolean> {
      // If refresh is already in progress, return the existing promise
      if (this.isRefreshing && this.refreshPromise) {
        return await this.refreshPromise;
      }
      
      // Create new refresh promise
      this.isRefreshing = true;
      const refreshPromise = this._performRefresh();
      this.refreshPromise = refreshPromise;
      
      try {
        const result = await refreshPromise;
        return result;
      } finally {
        // Clear flags after promise completes (success or failure)
        this.isRefreshing = false;
        if (this.refreshPromise === refreshPromise) {
          this.refreshPromise = null;
        }
      }
    },

    /**
     * Internal method to perform the actual token refresh
     */
    async _performRefresh(): Promise<boolean> {
      try {
      // Refresh token'ı cookie'den al (state'deki güncel olmayabilir)
      const refreshTokenCookie = useCookie("refresh_token");
      const refreshTokenValue = refreshTokenCookie.value || this.refreshToken;
      
      if (!refreshTokenValue || !this.domainName) {
        throw new Error("Refresh token veya domain bilgisi bulunamadı");
      }

        // Client-side expire kontrolü yapmayalım - backend refresh token'ın geçerliliğini kontrol edecek
        // Eğer refresh token gerçekten expire olmuşsa, backend 401/403 dönecek ve biz de ona göre handle edeceğiz
        // Bu yaklaşım daha güvenilir çünkü:
        // 1. Refresh token'ın exp claim'i olmayabilir (backend farklı format kullanıyor olabilir)
        // 2. Backend'in expire kontrolü daha güvenilir
        // 3. Clock skew sorunları olabilir (client ve server zamanı farklı olabilir)

        const response: TokenResponse = await refreshMngKeeperToken(
          refreshTokenValue,
          this.domainName
        );
        
        this.accessToken = response.accessToken;
        this.refreshToken = response.refreshToken;
        
        // Update cookies
        const accessTokenCookie = useCookie("access_token", {
          maxAge: response.expiresIn || 300,
          secure: process.env.NODE_ENV === 'production',
          sameSite: "strict",
        });
        const newRefreshTokenCookie = useCookie("refresh_token", {
          maxAge: response.refreshExpiresIn || 1800,
          secure: process.env.NODE_ENV === 'production',
          sameSite: "strict",
        });
        
        accessTokenCookie.value = response.accessToken;
        newRefreshTokenCookie.value = response.refreshToken;
        
        // Decode and update user info
        try {
          const decoded = decodeJwt(response.accessToken) as any;
          
          // Normalize field names: is_admin -> isAdmin
          // Also preserve domain information from previous userInfo if new token doesn't have it
          const previousUserInfo = this.userInfo;
          const hasDomainInNewToken = !!(decoded.domain_name || decoded.domainName);
          
          const normalizedUserInfo: UserInfo = {
            ...decoded,
            isAdmin: decoded.isAdmin === true || decoded.is_admin === true || false,
            is_manager: decoded.is_manager === true || decoded.isManager === true || false,
            // Preserve domain information if not in new token
            domain_name: decoded.domain_name || decoded.domainName || previousUserInfo?.domain_name,
            domain_id: decoded.domain_id || decoded.domainId || previousUserInfo?.domain_id,
          };
          
          this.userInfo = normalizedUserInfo;
          this.isAuthenticated = true;
          
          // Update localStorage
          if (process.client) {
            localStorage.setItem("userInfo", JSON.stringify(normalizedUserInfo));
          }
        } catch (error) {
          // Decode error - token might be invalid, but don't throw here
        }
        
        return true;
      } catch (error: any) {
        const errorMessage = error.message || error.toString();
        const statusCode = error.statusCode || error.status || error.response?.status;
        
        // If API returns 401, it likely means refresh token is expired or invalid
        // Enhance the error message to indicate refresh token expiration
        if (statusCode === 401 || statusCode === 403) {
          throw new Error("Refresh token süresi dolmuş veya geçersiz");
        }
        
        // Don't logout here, let the caller decide (might want to retry or handle differently)
        // Just throw the error so the caller knows refresh failed
        throw error;
      }
    },

    /**
     * Access token'ı kontrol eder ve gerekirse refresh eder
     * @returns true if token is valid or was successfully refreshed
     */
    async ensureValidToken(): Promise<boolean> {
      // If already refreshing, wait for the existing refresh to complete
      if (this.isRefreshing && this.refreshPromise) {
        try {
          return await this.refreshPromise;
        } catch (error) {
          // Refresh failed, try to continue with current token
          // Check if we still have a token
          const accessTokenCookie = useCookie("access_token");
          return !!accessTokenCookie.value;
        }
      }
      
      // First, try to get token from cookie (more reliable)
      const accessTokenCookie = useCookie("access_token");
      let token = accessTokenCookie.value || this.accessToken;
      
      // Token yoksa false döndür
      if (!token) {
        return false;
      }

      // Update state if cookie has different token
      if (accessTokenCookie.value && accessTokenCookie.value !== this.accessToken) {
        this.accessToken = accessTokenCookie.value;
        token = accessTokenCookie.value;
      }

      // Token expire olmuş mu kontrol et (60 saniye buffer ile)
      const isExpired = isTokenExpired(token, 60);
      
      if (isExpired) {
        // Önce refresh token'ın expire olup olmadığını kontrol et
        // Refresh token expire olmuşsa refresh denememeliyiz
        const refreshTokenCookie = useCookie("refresh_token");
        const refreshTokenValue = refreshTokenCookie.value || this.refreshToken;
        
        if (!refreshTokenValue) {
          return false;
        }
        
        // Refresh token'ın kendisi expire olmuş mu kontrol et (0 buffer - tam expire olup olmadığını kontrol et)
        // Önce token'ı decode edip detaylı kontrol yapalım
        let isRefreshTokenExpired = false;
        let expirationTime: Date | null = null;
        let hasExpClaim = false;
        
        try {
          const decoded = decodeJwt(refreshTokenValue) as any;
          
          if (!decoded.exp) {
            // Exp claim yoksa - bu backend'den gelen token'ın exp claim'i olmayabilir
            // Bu durumda token'ı expire saymak yerine, backend'e refresh request gönderip kontrol edelim
            hasExpClaim = false;
            // Exp claim yoksa, backend refresh request'ini yapacak ve geçerliliğini kontrol edecek
            // Burada expire saymayalım
            isRefreshTokenExpired = false;
          } else {
            hasExpClaim = true;
            expirationTime = new Date(decoded.exp * 1000);
            const currentTime = Math.floor(Date.now() / 1000);
            const tokenExpTime = decoded.exp;
            
            // Buffer olmadan kontrol (0 buffer)
            isRefreshTokenExpired = currentTime >= tokenExpTime;
          }
        } catch (decodeError: any) {
          // Decode hatası varsa - token geçersiz format olabilir
          // Bu durumda refresh denemesine izin verelim, backend geçerliliğini kontrol edecek
          hasExpClaim = false;
          // Decode hatası olsa bile, refresh denemesine izin ver
          // Backend refresh request'ini reddederse, o zaman logout yapılacak
          isRefreshTokenExpired = false;
        }
        
        if (isRefreshTokenExpired) {
          // Refresh token expire olmuş (exp claim var ve gerçekten expire olmuş)
          // Ama logout yapmayalım - API service yapacak
          // Sadece false döndür, böylece request devam eder ve server 401 döner
          // API service 401 aldığında refresh deneyecek ve başarısız olunca logout yapacak
          return false;
        }
        
        // Refresh token geçerli, refresh deneyebiliriz
        try {
          // Refresh token ile yenile
          const refreshed = await this.refreshAccessToken();
          return refreshed;
        } catch (error: any) {
          // Don't logout here - let the API service handle logout when it receives 401
          // This allows the request to proceed and let the server return 401 if needed
          // The API service will then attempt refresh again, and if that fails, it will logout
          // Return false to indicate current token is invalid
          return false;
        }
      }

      return true;
    },

    async logout() {
      // Revoke refresh token if available
      if (this.refreshToken && this.domainName) {
        try {
          await revokeMngKeeperToken(this.refreshToken, this.domainName);
        } catch (error) {
          // Token revoke failed - continue with logout
        }
      }
      
      // Clear state
      this.accessToken = null;
      this.refreshToken = null;
      this.userInfo = null;
      this.domainInfo = null;
      this.isAuthenticated = false;
      this.isRefreshing = false;
      this.refreshPromise = null;
      
      // Clear cookies
      const accessTokenCookie = useCookie("access_token");
      const refreshTokenCookie = useCookie("refresh_token");
      accessTokenCookie.value = null;
      refreshTokenCookie.value = null;
      
      // Clear localStorage
      if (process.client) {
        localStorage.removeItem("userInfo");
        
        // Clear user preferences
        try {
          const { useUserPreferencesStore } = await import('@/stores/apps/userPreferences');
          const preferencesStore = useUserPreferencesStore();
          preferencesStore.clearPreferences();
        } catch (error) {
          // Preferences store might not be loaded yet
        }
        
        try {
          const { useUserNotesStore } = await import('@/stores/apps/userNotes');
          const notesStore = useUserNotesStore();
          notesStore.clearNotes();
        } catch (error) {
          // Notes store might not be loaded yet
        }
        
        // Clear user store currentUser
        try {
          const { useUserStore } = await import('@/stores/apps/user');
          const userStore = useUserStore();
          userStore.currentUser = null;
        } catch (error) {
          // User store might not be loaded yet
        }
      }
    },

    async initializeAuth() {
      // Check if tokens exist in cookies
      const accessTokenCookie = useCookie("access_token");
      const refreshTokenCookie = useCookie("refresh_token");
      
      if (accessTokenCookie.value && refreshTokenCookie.value) {
        this.accessToken = accessTokenCookie.value;
        this.refreshToken = refreshTokenCookie.value;
        
        // Token expire olmuş mu kontrol et
        if (isTokenExpired(accessTokenCookie.value, 60)) {
          // Expire olmuşsa refresh et
          try {
            await this.refreshAccessToken();
            return;
          } catch (error) {
            // Refresh başarısız, logout yap
            await this.logout();
            return;
          }
        }
        
        // Try to decode token
        try {
          const decoded = decodeJwt(accessTokenCookie.value) as any;
          
          // Normalize field names: is_admin -> isAdmin
          const normalizedUserInfo: UserInfo = {
            ...decoded,
            isAdmin: decoded.isAdmin === true || decoded.is_admin === true || false,
            is_manager: decoded.is_manager === true || decoded.isManager === true || false,
          };
          
          this.userInfo = normalizedUserInfo;
          this.isAuthenticated = true;
          
          // Restore from localStorage if available (but prefer decoded token)
          const storedUserInfo = localStorage.getItem("userInfo");
          if (storedUserInfo) {
            try {
              const parsed = JSON.parse(storedUserInfo);
              // Merge stored info with decoded token (decoded token takes precedence)
              this.userInfo = {
                ...parsed,
                ...normalizedUserInfo,
              };
            } catch (error) {
              // Failed to parse stored user info - continue without it
            }
          }
          
          // Load domain information
          try {
            await this.loadDomainInfo();
          } catch (domainError) {
            // Domain bilgisi yüklenemezse hata verme, sadece logla
            console.warn('Domain bilgisi yüklenemedi:', domainError);
          }
          
          // Load user preferences after initialization
          if (process.client) {
            try {
              const { useUserPreferencesStore } = await import('@/stores/apps/userPreferences');
              const preferencesStore = useUserPreferencesStore();
              const userId = normalizedUserInfo.sub || normalizedUserInfo.username;
              if (userId) {
                const prefs = await preferencesStore.loadPreferences(userId);
                if (prefs) {
                  preferencesStore.applyPreferences(prefs);
                }
              }
            } catch (prefError) {
              // Preferences yüklenemezse hata verme, sadece logla
              // Dataset henüz oluşturulmamış olabilir
              console.warn('Kullanıcı tercihleri yüklenemedi:', prefError);
            }
          }
        } catch (error) {
          // JWT decode error - clear everything
          await this.logout();
        }
      }
    },

    /**
     * Load domain information from MngKeeper
     */
    async loadDomainInfo(): Promise<void> {
      if (!this.userInfo) {
        return;
      }

      const domainName = this.userInfo.domain_name;
      const domainId = this.userInfo.domain_id;

      if (!domainName && !domainId) {
        return;
      }

      try {
        // Use composable to get domain info
        const { getCurrentDomain } = useDomain();
        const domain = await getCurrentDomain();
        
        if (domain) {
          this.domainInfo = domain;
        }
      } catch (error) {
        console.error('Error loading domain info:', error);
        // Don't throw - domain info is optional
      }
    },
  },
});

