import { defineStore } from "pinia";
import { loginToMngKeeper, refreshMngKeeperToken, revokeMngKeeperToken, type TokenResponse } from "@/services/apiService";
import { decodeJwt } from "jose";

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

    async refreshToken() {
      if (!this.refreshToken || !this.domainName) {
        throw new Error("Refresh token veya domain bilgisi bulunamadı");
      }

      try {
        const response: TokenResponse = await refreshMngKeeperToken(
          this.refreshToken,
          this.domainName
        );
        
        this.accessToken = response.accessToken;
        this.refreshToken = response.refreshToken;
        
        // Update cookies
        const accessTokenCookie = useCookie("access_token", {
          maxAge: response.expiresIn,
          secure: true,
          sameSite: "strict",
        });
        const refreshTokenCookie = useCookie("refresh_token", {
          maxAge: response.refreshExpiresIn,
          secure: true,
          sameSite: "strict",
        });
        
        accessTokenCookie.value = response.accessToken;
        refreshTokenCookie.value = response.refreshToken;
        
        // Decode and update user info
        try {
          const decoded = decodeJwt(response.accessToken) as UserInfo;
          this.userInfo = decoded;
        } catch (error) {
          console.error("JWT decode error:", error);
        }
        
        return { success: true };
      } catch (error) {
        // If refresh fails, logout
        await this.logout();
        throw error;
      }
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

    initializeAuth() {
      // Check if tokens exist in cookies
      const accessTokenCookie = useCookie("access_token");
      const refreshTokenCookie = useCookie("refresh_token");
      
      if (accessTokenCookie.value && refreshTokenCookie.value) {
        this.accessToken = accessTokenCookie.value;
        this.refreshToken = refreshTokenCookie.value;
        
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
          this.logout();
        }
      }
    },
  },
});

