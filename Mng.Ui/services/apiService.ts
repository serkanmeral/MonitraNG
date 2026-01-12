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
      
      // Try to ensure token is valid (refresh if needed) - BEFORE making the request
      // But don't fail if it returns false, let the server decide (it will return 401 if needed)
      try {
        await authStore.ensureValidToken();
      } catch (tokenError: any) {
        // If ensureValidToken throws an error, it means refresh failed
        // In this case, we should still try the request (server might accept it)
        // Don't throw here, let the request proceed (server will return 401 if needed)
      }
      
      // Get token from cookie (NOT from localStorage for security)
      const token = getAccessToken();
      
      if (!token) {
        throw new Error("Access token bulunamadı. Lütfen tekrar giriş yapın.");
      }
      
      // Remove leading slash if exists
      const cleanUrl = url.startsWith('/') ? url.slice(1) : url;
      const fullUrl = `/api/keeper/${cleanUrl}`;
      
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
// Uses Nuxt server API route to avoid SSL certificate issues in browser
export function fetchFromDataGateway(
  url: string,
  method: "GET" | "POST" | "PUT" | "DELETE" = "GET",
  body?: any,
  headers: Record<string, string> = {}
): Promise<any> {
  return new Promise(async (resolve, reject) => {
    try {
      const authStore = useAuthStore();
      
      // Try to ensure token is valid (refresh if needed) - BEFORE making the request
      // But don't fail if it returns false, let the server decide (it will return 401 if needed)
      try {
        await authStore.ensureValidToken();
      } catch (tokenError: any) {
        // If ensureValidToken throws an error, it means refresh failed
        // In this case, we should still try the request (server might accept it)
        // Don't throw here, let the request proceed (server will return 401 if needed)
      }

      // URL formatı: '/api/v1/data/@side_menu?skip=0&limit=1000'
      // Query string'i ayrı çıkar
      const [pathPart, queryPart] = url.split('?');
      const cleanPath = pathPart.startsWith('/') ? pathPart : `/${pathPart}`;
      
      // Server route: '/api/data/[...path]'
      // Path'ten '/api/' kısmını çıkar: '/api/v1/data/@side_menu' -> 'v1/data/@side_menu'
      let serverPath = cleanPath;
      if (serverPath.startsWith('/api/v1/')) {
        serverPath = serverPath.replace(/^\/api\/v1\//, 'v1/');
      } else if (serverPath.startsWith('/api/')) {
        serverPath = serverPath.replace(/^\/api\//, '');
      } else if (serverPath.startsWith('/')) {
        serverPath = serverPath.substring(1);
      }
      
      // Query string'i tekrar ekle
      const fullUrl = queryPart 
        ? `/api/data/${serverPath}?${queryPart}`
        : `/api/data/${serverPath}`;
      
      let response: any;
      
      try {
        // Use $fetch which automatically handles server-side routing
        response = await $fetch(fullUrl, {
        method,
          ...(body && { body }),
        headers: {
          ...headers,
        },
        });
        
        resolve(response);
      } catch (fetchError: any) {
        // 401 Unauthorized hatası - token expire olmuş olabilir
        if (fetchError.statusCode === 401 || fetchError.status === 401) {
          try {
            // Token'ı refresh etmeyi dene
            const refreshed = await authStore.refreshAccessToken();
            
            if (refreshed) {
              // Token refresh başarılı, isteği tekrar dene
              try {
                const retryResponse = await $fetch(fullUrl, {
                  method,
                  ...(body && { body }),
                  headers: {
                    ...headers,
                  },
                });
                resolve(retryResponse);
                return;
              } catch (retryError: any) {
                // Retry de başarısız, normal hata akışına devam et
                fetchError = retryError;
                // Retry'de de 401 alırsak aşağıdaki hata handling'e devam et
              }
            }
          } catch (refreshError: any) {
            // Refresh başarısız, check if refresh token really expired
            const refreshErrorMessage = refreshError.message || refreshError.toString();
            const refreshErrorStatus = refreshError.statusCode || refreshError.status || refreshError.response?.status;
            
            // Check error message for refresh token expiration indicators
            const errorMessageIndicatesExpiration = 
              refreshErrorMessage.includes('Refresh token süresi dolmuş') || 
              refreshErrorMessage.includes('Refresh token süresi dolmuş veya geçersiz') ||
              refreshErrorMessage.includes('refresh token expired') ||
              refreshErrorMessage.includes('Refresh token veya domain bilgisi bulunamadı');
            
            // Check if API returned 401/403 (token expired/invalid)
            const is401or403 = refreshErrorStatus === 401 || refreshErrorStatus === 403;
            
            // Check the actual refresh token expiration
            // Since ensureValidToken already checked, we trust the error message
            // But also verify by checking if error message clearly indicates expiration
            const isRefreshTokenActuallyExpired = errorMessageIndicatesExpiration || is401or403;
            
            // Only logout if error clearly indicates refresh token expiration
            if (isRefreshTokenActuallyExpired) {
              // Refresh token expired, logout and redirect to login
              await authStore.logout();
              if (process.client) {
                navigateTo('/auth/login');
              }
              reject(new Error("Oturum süresi dolmuş. Lütfen tekrar giriş yapın."));
              return;
            } else {
              // Refresh token still valid but refresh failed (network error, server error, etc.)
              // Don't logout, just reject with the original error
              // Re-throw the original fetch error, not the refresh error
              // The caller can handle it appropriately
              throw fetchError;
            }
          }
        }
        
        // Handle H3 errors (from server API route)
        if (fetchError.data) {
          const errorData = fetchError.data;
          if (typeof errorData === 'object') {
            throw new Error(errorData.errorDescription || errorData.error || errorData.message || 'İstek başarısız');
          } else if (typeof errorData === 'string') {
            throw new Error(errorData);
          }
        }
        
        // Handle status messages
        if (fetchError.statusMessage) {
          throw new Error(fetchError.statusMessage);
        }
        
        // Handle regular errors
        throw fetchError;
      }
    } catch (error: any) {
      reject(error);
    }
  });
}

// Legacy functions (for backward compatibility)
export function fetchData(url = "", method = "GET", body = "", headers = {}) {
  return fetchFromDataGateway(url, method, body, headers);
}

// MngLLM API Functions (with token)
// Uses Nuxt server API route to avoid SSL certificate issues in browser
export function fetchFromMngLLM(
  url: string,
  method: "GET" | "POST" | "PUT" | "DELETE" = "GET",
  body?: any,
  headers: Record<string, string> = {}
): Promise<any> {
  return new Promise(async (resolve, reject) => {
    try {
      const authStore = useAuthStore();
      
      // Try to ensure token is valid (refresh if needed) - BEFORE making the request
      try {
        await authStore.ensureValidToken();
      } catch (tokenError: any) {
        // If ensureValidToken throws an error, it means refresh failed
        // Don't throw here, let the request proceed (server will return 401 if needed)
      }

      // URL formatı: '/api/v1/llm/translate'
      // Query string'i ayrı çıkar
      const [pathPart, queryPart] = url.split('?');
      const cleanPath = pathPart.startsWith('/') ? pathPart : `/${pathPart}`;
      
      // Server route: '/api/llm/[...path]'
      // Path: 'api/v1/llm/translate' olmalı (başındaki '/' olmadan)
      // Ama biz '/api/v1/llm/translate' gönderiyoruz, bu durumda server route'a '/api/llm/api/v1/llm/translate' gider
      // Bunun yerine path'ten '/api/' kısmını çıkarıp direkt 'v1/llm/translate' gönderelim
      let serverPath = cleanPath;
      if (serverPath.startsWith('/api/v1/')) {
        serverPath = serverPath.replace('/api/v1/', 'v1/');
      } else if (serverPath.startsWith('/api/')) {
        serverPath = serverPath.replace('/api/', '');
      }
      
      // Query string'i tekrar ekle
      const fullUrl = queryPart 
        ? `/api/llm/${serverPath}?${queryPart}`
        : `/api/llm/${serverPath}`;
      
      try {
        // Use $fetch which automatically handles server-side routing
        const response = await $fetch(fullUrl, {
          method,
          ...(body && { body }),
          headers: {
            ...headers,
          },
        });
        
        resolve(response);
      } catch (fetchError: any) {
        // 401 Unauthorized hatası - token expire olmuş olabilir
        if (fetchError.statusCode === 401 || fetchError.status === 401) {
          try {
            // Token'ı refresh etmeyi dene
            const refreshed = await authStore.refreshAccessToken();
            
            if (refreshed) {
              // Token refresh başarılı, isteği tekrar dene
              try {
                const retryResponse = await $fetch(fullUrl, {
                  method,
                  ...(body && { body }),
                  headers: {
                    ...headers,
                  },
                });
                resolve(retryResponse);
                return;
              } catch (retryError: any) {
                // Retry de başarısız, normal hata akışına devam et
                fetchError = retryError;
              }
            }
          } catch (refreshError: any) {
            // Refresh başarısız, normal hata akışına devam et
          }
        }
        
        // Error handling
        const errorMessage = fetchError.message || fetchError.statusMessage || 'Unknown error';
        reject(new Error(errorMessage));
      }
    } catch (error: any) {
      const errorMessage = error.message || error || 'Unknown error';
      reject(new Error(errorMessage));
    }
  });
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
