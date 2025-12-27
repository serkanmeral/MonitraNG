interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "DELETE";
  headers?: Record<string, string>;
  body?: any;
}

// MngKeeper API Types
export interface TokenRequest {
  username: string;
  password: string;
  domain?: string;
}

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresIn: number;
  refreshExpiresIn: number;
}

export interface ErrorResponse {
  error: string;
  errorDescription: string;
}

// MngKeeper Authentication Functions
export async function loginToMngKeeper(
  username: string,
  password: string,
  domain?: string
): Promise<TokenResponse> {
  const requestBody: TokenRequest = {
    username,
    password,
    ...(domain && { domain })
  };

  try {
    // Nginx proxy üzerinden istek yap (SSL sertifika sorunu olmaz)
    const response = await $fetch<TokenResponse>('/api/auth/token', {
      method: 'POST',
      body: requestBody,
    });

    return response;
  } catch (error: any) {
    // Hata mesajını düzgün formatla
    let errorMessage = 'Giriş başarısız';
    
    if (error.data) {
      // Nuxt createError'dan gelen hata
      const errorData = error.data;
      if (typeof errorData === 'object') {
        errorMessage = errorData.errorDescription || errorData.error || errorMessage;
      } else if (typeof errorData === 'string') {
        errorMessage = errorData;
      }
    } else if (error.message) {
      errorMessage = error.message;
    } else if (error.statusMessage) {
      errorMessage = error.statusMessage;
    }
    
    throw new Error(errorMessage);
  }
}

export async function refreshMngKeeperToken(
  refreshToken: string,
  domain: string
): Promise<TokenResponse> {
  try {
    // Nginx proxy üzerinden istek yap
    const response = await $fetch<TokenResponse>('/api/keeper/auth/refresh', {
      method: 'POST',
      body: {
        refreshToken,
        domain
      },
    });

    return response;
  } catch (error: any) {
    // Hata mesajını düzgün formatla
    let errorMessage = 'Token yenileme başarısız';
    
    if (error.data) {
      const errorData = error.data;
      if (typeof errorData === 'object') {
        errorMessage = errorData.errorDescription || errorData.error || errorMessage;
      } else if (typeof errorData === 'string') {
        errorMessage = errorData;
      }
    } else if (error.message) {
      errorMessage = error.message;
    } else if (error.statusMessage) {
      errorMessage = error.statusMessage;
    }
    
    throw new Error(errorMessage);
  }
}

export async function revokeMngKeeperToken(
  refreshToken: string,
  domain: string
): Promise<void> {
  try {
    // Nginx proxy üzerinden istek yap
    await $fetch('/api/keeper/auth/revoke', {
      method: 'POST',
      body: {
        refreshToken,
        domain
      },
    });
  } catch (error: any) {
    // Hata mesajını düzgün formatla
    let errorMessage = 'Token iptal edilemedi';
    
    if (error.data) {
      const errorData = error.data;
      if (typeof errorData === 'string') {
        errorMessage = errorData;
      }
    } else if (error.message) {
      errorMessage = error.message;
    } else if (error.statusMessage) {
      errorMessage = error.statusMessage;
    }
    
    throw new Error(errorMessage);
  }
}

// Helper function to get access token from cookie
export function getAccessToken(): string | null {
  const tokenCookie = useCookie("access_token");
  return tokenCookie.value || null;
}

// Flag to prevent multiple simultaneous refresh attempts
let isRefreshing = false;
let refreshPromise: Promise<boolean> | null = null;

// Helper function to get refresh token from cookie
export function getRefreshToken(): string | null {
  const refreshTokenCookie = useCookie("refresh_token");
  return refreshTokenCookie.value || null;
}

// MngKeeper API Functions (with token)
export function fetchFromMngKeeper(
  url: string,
  method: "GET" | "POST" | "PUT" | "DELETE" = "GET",
  body?: any,
  headers: Record<string, string> = {}
): Promise<any> {
  return new Promise(async (resolve, reject) => {
    try {
      const authStore = useAuthStore();
      
      // Ensure token is valid (refresh if needed)
      const isValid = await authStore.ensureValidToken();
      if (!isValid) {
        throw new Error("Token geçersiz veya süresi dolmuş. Lütfen tekrar giriş yapın.");
      }
      
      // Get token from cookie (NOT from localStorage for security)
      const token = getAccessToken();
      
      if (!token) {
        throw new Error("Access token bulunamadı. Lütfen tekrar giriş yapın.");
      }
      
      // Remove leading slash if exists
      const cleanUrl = url.startsWith('/') ? url.slice(1) : url;
      const fullUrl = `/api/keeper/${cleanUrl}`;
      
      // LOG: Sadece PUT (güncelleme) request'leri için log
      if (process.env.NODE_ENV === 'development' && method === 'PUT' && cleanUrl.startsWith('group')) {
        console.log('[ApiService] Update Group Request:', {
          url: fullUrl,
          method,
          body
        });
      }
      
      // DELETE işlemleri için 204 NoContent response'u handle et
      let response: any;
      
      if (method === 'DELETE') {
        try {
          // DELETE için $fetch.raw kullanarak status code'u kontrol et
          const rawResponse = await $fetch.raw(fullUrl, {
            method,
            headers: {
              Authorization: `Bearer ${token}`,
              ...headers,
            },
            ...(body && { body }),
          });
          
          // 204 NoContent durumu - başarılı, body yok
          if (rawResponse.status === 204) {
            response = { success: true, statusCode: 204 };
          } else {
            // Diğer başarılı durumlar
            response = rawResponse._data;
          }
        } catch (fetchError: any) {
          // 204 NoContent için $fetch.raw hata vermez, ama kontrol edelim
          if (fetchError.statusCode === 204 || fetchError.response?.status === 204) {
            response = { success: true, statusCode: 204 };
          } else {
            throw fetchError;
          }
        }
      } else {
        // GET, POST, PUT için normal $fetch kullan
        response = await $fetch(fullUrl, {
          method,
          headers: {
            Authorization: `Bearer ${token}`,
            ...headers,
          },
          ...(body && { body }),
        });
      }

      // LOG: Sadece PUT (güncelleme) response'ları için log
      if (process.env.NODE_ENV === 'development' && method === 'PUT' && cleanUrl.startsWith('group')) {
        console.log('[ApiService] Update Group Response:', response);
      }

      resolve(response);
    } catch (error: any) {
      // 401 Unauthorized hatası - token expire olmuş olabilir
      if (error.statusCode === 401 || error.status === 401) {
        const authStore = useAuthStore();
        
        // Token'ı refresh etmeyi dene
        try {
          const refreshed = await authStore.refreshAccessToken();
          
          if (refreshed) {
            // Token refresh başarılı, isteği tekrar dene
            const token = getAccessToken();
            if (token) {
              const cleanUrl = url.startsWith('/') ? url.slice(1) : url;
              const retryFullUrl = `/api/keeper/${cleanUrl}`;
              try {
                const retryResponse = await $fetch(retryFullUrl, {
                  method,
                  headers: {
                    Authorization: `Bearer ${token}`,
                    ...headers,
                  },
                  ...(body && { body }),
                });
                resolve(retryResponse);
                return;
              } catch (retryError: any) {
                // Retry de başarısız, normal hata akışına devam et
                error = retryError;
              }
            }
          }
        } catch (refreshError) {
          // Refresh başarısız, logout yap ve login sayfasına yönlendir
          await authStore.logout();
          if (process.client) {
            navigateTo('/auth/login');
          }
          reject(new Error("Oturum süresi dolmuş. Lütfen tekrar giriş yapın."));
          return;
        }
      }
      
      // Hata mesajını düzgün formatla
      let errorMessage = 'İstek başarısız';
      
      if (error.data) {
        const errorData = error.data;
        if (typeof errorData === 'object') {
          errorMessage = errorData.errorDescription || errorData.error || errorMessage;
        } else if (typeof errorData === 'string') {
          errorMessage = errorData;
        }
      } else if (error.message) {
        errorMessage = error.message;
      } else if (error.statusMessage) {
        errorMessage = error.statusMessage;
      }
      
      reject(new Error(errorMessage));
    }
  });
}

// MngDataGateway API Functions (with token)
export function fetchFromDataGateway(
  url: string,
  method: "GET" | "POST" | "PUT" | "DELETE" = "GET",
  body?: any,
  headers: Record<string, string> = {}
): Promise<any> {
  return new Promise(async (resolve, reject) => {
    try {
      // Get token from cookie (NOT from localStorage for security)
      const token = getAccessToken();
      
      if (!token) {
        throw new Error("Access token bulunamadı. Lütfen tekrar giriş yapın.");
      }
      
      const config = useRuntimeConfig();
      // Use gateway URL if available, otherwise use direct DataGateway URL
      const baseUrl = config.public.gatewayUrl 
        ? `${config.public.gatewayUrl}/data`
        : (config.public.reactorUrl || 'https://localhost:5010');
      
      const response = await fetch(`${baseUrl}${url}`, {
        method,
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
          ...headers,
        },
        ...(body && { body: JSON.stringify(body) }),
      });

      if (!response.ok) {
        let errorMessage = 'İstek başarısız';
        try {
          const errorData = await response.json();
          errorMessage = errorData.errorDescription || errorData.error || errorMessage;
        } catch {
          const errorText = await response.text();
          errorMessage = errorText || errorMessage;
        }
        throw new Error(errorMessage);
      }

      const data = await response.json();
      resolve(data);
    } catch (error) {
      reject(error);
    }
  });
}

// Legacy functions (for backward compatibility)
export function fetchData(url = "", method = "GET", body = "", headers = {}) {
  return fetchFromDataGateway(url, method, body, headers);
}

export function fetchDataWithoutToken<T>(
  url = "",
  method = "GET",
  body = "",
  headers = {}
): Promise<T> {
  return new Promise(async (resolve, reject) => {
    try {
      const options = {
        method,
        body,
        headers: {
          "Content-Type": "application/json",
          ...headers,
        },
      };

      if (body) {
        options.body = body;
      }

      const response = await fetch(url, options);

      if (!response.ok) {
        const resError = await response.text();
        throw new Error(resError);
      }

      const data = await response.json();
      resolve(data);
    } catch (error) {
      reject(error);
    }
  });
}
