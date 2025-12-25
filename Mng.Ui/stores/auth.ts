import { defineStore } from "pinia";
import { loginToMngKeeper, refreshMngKeeperToken, revokeMngKeeperToken, type TokenResponse } from "@/services/apiService";
import { decodeJwt } from "jose";
import { isTokenExpired } from "@/utils/tokenUtils";

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
  [key: string]: any;
}

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  userInfo: UserInfo | null;
  isAuthenticated: boolean;
  isLoading: boolean;
}

export const useAuthStore = defineStore("auth", {
  state: (): AuthState => ({
    accessToken: null,
    refreshToken: null,
    userInfo: null,
    isAuthenticated: false,
    isLoading: false,
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
      return state.userInfo?.user_groups || [];
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
          const decoded = decodeJwt(response.accessToken) as UserInfo;
          this.userInfo = decoded;
          this.isAuthenticated = true;
          
          // Store user info in localStorage
          localStorage.setItem("userInfo", JSON.stringify(decoded));
        } catch (error) {
          console.error("JWT decode error:", error);
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
      // Refresh token'ı cookie'den al (state'deki güncel olmayabilir)
      const refreshTokenCookie = useCookie("refresh_token");
      const refreshTokenValue = refreshTokenCookie.value || this.refreshToken;
      
      if (!refreshTokenValue || !this.domainName) {
        throw new Error("Refresh token veya domain bilgisi bulunamadı");
      }

      // Refresh token'ın kendisi expire olmuş mu kontrol et
      if (isTokenExpired(refreshTokenValue, 0)) {
        throw new Error("Refresh token süresi dolmuş");
      }

      try {
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
          const decoded = decodeJwt(response.accessToken) as UserInfo;
          this.userInfo = decoded;
          this.isAuthenticated = true;
          
          // Update localStorage
          localStorage.setItem("userInfo", JSON.stringify(decoded));
        } catch (error) {
          console.error("JWT decode error:", error);
        }
        
        return true;
      } catch (error) {
        // If refresh fails, logout
        await this.logout();
        throw error;
      }
    },

    /**
     * Access token'ı kontrol eder ve gerekirse refresh eder
     * @returns true if token is valid or was successfully refreshed
     */
    async ensureValidToken(): Promise<boolean> {
      // Token yoksa false döndür
      if (!this.accessToken) {
        return false;
      }

      // Token expire olmuş mu kontrol et (60 saniye buffer ile)
      if (isTokenExpired(this.accessToken, 60)) {
        try {
          // Refresh token ile yenile
          await this.refreshAccessToken();
          return true;
        } catch (error) {
          // Refresh başarısız, logout yap
          console.error("Token refresh failed:", error);
          await this.logout();
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
          console.error("Token revoke error:", error);
        }
      }
      
      // Clear state
      this.accessToken = null;
      this.refreshToken = null;
      this.userInfo = null;
      this.isAuthenticated = false;
      
      // Clear cookies
      const accessTokenCookie = useCookie("access_token");
      const refreshTokenCookie = useCookie("refresh_token");
      accessTokenCookie.value = null;
      refreshTokenCookie.value = null;
      
      // Clear localStorage
      localStorage.removeItem("userInfo");
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
            console.error("Token refresh failed during initialization:", error);
            await this.logout();
            return;
          }
        }
        
        // Try to decode token
        try {
          const decoded = decodeJwt(accessTokenCookie.value) as UserInfo;
          this.userInfo = decoded;
          this.isAuthenticated = true;
          
          // Restore from localStorage if available
          const storedUserInfo = localStorage.getItem("userInfo");
          if (storedUserInfo) {
            try {
              this.userInfo = JSON.parse(storedUserInfo);
            } catch (error) {
              console.error("Failed to parse stored user info:", error);
            }
          }
        } catch (error) {
          console.error("JWT decode error:", error);
          // If decode fails, clear everything
          await this.logout();
        }
      }
    },
  },
});

