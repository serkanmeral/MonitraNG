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

// Helper: cookie (kalıcı oturum) veya Pinia (login sonrası, cookie henüz yazılmadan)
export function getAccessToken(): string | null {
  const tokenCookie = useCookie("access_token");
  if (tokenCookie.value) {
    return tokenCookie.value;
  }
  if (import.meta.client) {
    try {
      const authStore = useAuthStore();
      if (authStore.accessToken) {
        return authStore.accessToken;
      }
    } catch {
      // store henüz hazır değil
    }
  }
  return null;
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
        // FormData için Nuxt server route'u handle ediyor
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

      // Token'ı cookie'den al (production'da Nginx proxy Authorization'ı client'tan iletir; bu yüzden mutlaka gönderilmeli)
      const token = getAccessToken();
      if (!token) {
        throw new Error("Access token bulunamadı. Lütfen tekrar giriş yapın.");
      }

      // URL formatı: '/api/v1/data/@side_menu?skip=0&limit=1000'
      // Query string'i ayrı çıkar
      const [pathPart, queryPart] = url.split('?');
      const cleanPath = pathPart.startsWith('/') ? pathPart : `/${pathPart}`;
      
      // Server route: '/api/data/[...path]'
      // Path'ten '/api/v1/data/' kısmını çıkar: '/api/v1/data/@side_menu' -> 'v1/data/@side_menu'
      // Veya '/api/v1/data/@side_menu' -> 'v1/data/@side_menu'
      let serverPath = cleanPath;
      if (serverPath.startsWith('/api/v1/data/')) {
        // '/api/v1/data/@side_menu' -> 'v1/data/@side_menu'
        serverPath = serverPath.replace(/^\/api\/v1\/data\//, 'v1/data/');
      } else if (serverPath.startsWith('/api/v1/')) {
        // '/api/v1/...' -> 'v1/...'
        serverPath = serverPath.replace(/^\/api\/v1\//, 'v1/');
      } else if (serverPath.startsWith('/api/')) {
        // '/api/...' -> '...'
        serverPath = serverPath.replace(/^\/api\//, '');
      } else if (serverPath.startsWith('/')) {
        // '/...' -> '...'
        serverPath = serverPath.substring(1);
      }
      
      // Query string'i tekrar ekle
      const fullUrl = queryPart 
        ? `/api/data/${serverPath}?${queryPart}`
        : `/api/data/${serverPath}`;
      
      let response: any;
      let totalCount: number | null = null;
      
      try {
        // Use $fetch.raw to access response headers
        // Authorization header zorunlu: production'da Nginx -> DataGateway proxy'si $http_authorization ile iletir
        const rawResponse = await $fetch.raw(fullUrl, {
          method,
          ...(body && { body }),
          headers: {
            Authorization: `Bearer ${token}`,
            ...headers,
          },
        });
        
        response = rawResponse._data;

        // BFF pagination wrapper: { items, totalCount }
        if (
          response &&
          typeof response === 'object' &&
          !Array.isArray(response) &&
          Array.isArray((response as { items?: unknown }).items)
        ) {
          const wrapped = response as { items: unknown[]; totalCount?: number };
          const items = wrapped.items;
          if (typeof wrapped.totalCount === 'number') {
            (items as { _totalCount?: number })._totalCount = wrapped.totalCount;
          }
          resolve(items);
          return;
        }
        
        // Extract X-Total-Count from response headers
        const totalCountHeader = rawResponse.headers.get('x-total-count');
        if (totalCountHeader) {
          totalCount = parseInt(totalCountHeader, 10);
        }
        
        // If totalCount is available, add it to response
        if (totalCount !== null && Array.isArray(response)) {
          // Add totalCount as a property to response (for backward compatibility)
          // Frontend can access it via response._totalCount
          (response as any)._totalCount = totalCount;
        }
        
        resolve(response);
      } catch (fetchError: any) {
        // 401 Unauthorized hatası - token expire olmuş olabilir
        if (fetchError.statusCode === 401 || fetchError.status === 401) {
          try {
            // Token'ı refresh etmeyi dene
            const refreshed = await authStore.refreshAccessToken();
            
            if (refreshed) {
              // Token refresh başarılı, isteği tekrar dene (yeni token ile)
              const newToken = getAccessToken();
              if (newToken) {
                try {
                  const retryResponse = await $fetch(fullUrl, {
                    method,
                    ...(body && { body }),
                    headers: {
                      Authorization: `Bearer ${newToken}`,
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
        // Preserve error structure for proper parsing in components
        if (fetchError.data) {
          const errorData = fetchError.data;
          
          // If errorData is an object with nested error structure, preserve it
          if (typeof errorData === 'object' && errorData.error && typeof errorData.error === 'object') {
            // Error structure like { success: false, error: { code, message, details } }
            // Create a custom error that preserves the structure
            const customError: any = new Error(errorData.error.message || errorData.error.code || 'İstek başarısız');
            customError.data = errorData; // Preserve full error structure: { success: false, error: {...} }
            customError.statusCode = fetchError.statusCode || fetchError.status;
            customError.statusMessage = fetchError.statusMessage || errorData.error.message;
            throw customError;
          } else if (typeof errorData === 'object') {
            // Simple error structure or other formats
            // Try to extract message, but preserve full structure
            let message = 'İstek başarısız';
            if (errorData.errorDescription) {
              message = errorData.errorDescription;
            } else if (typeof errorData.error === 'string') {
              message = errorData.error;
            } else if (errorData.message) {
              message = errorData.message;
            }
            
            const customError: any = new Error(message);
            customError.data = errorData; // Preserve error structure
            customError.statusCode = fetchError.statusCode || fetchError.status;
            customError.statusMessage = fetchError.statusMessage || message;
            throw customError;
          } else if (typeof errorData === 'string') {
            throw new Error(errorData);
          }
        }
        
        // Handle status messages - preserve error structure
        if (fetchError.statusMessage) {
          const customError: any = new Error(fetchError.statusMessage);
          customError.statusCode = fetchError.statusCode || fetchError.status;
          customError.statusMessage = fetchError.statusMessage;
          customError.data = fetchError.data; // Preserve data if exists
          throw customError;
        }
        
        // Handle regular errors - preserve structure
        const customError: any = fetchError instanceof Error ? fetchError : new Error(fetchError.message || 'Unknown error');
        if (fetchError.data) {
          customError.data = fetchError.data;
        }
        if (fetchError.statusCode || fetchError.status) {
          customError.statusCode = fetchError.statusCode || fetchError.status;
        }
        if (fetchError.statusMessage) {
          customError.statusMessage = fetchError.statusMessage;
        }
        throw customError;
      }
    } catch (error: any) {
      reject(error);
    }
  });
}

/** DataGateway proxy URL'ine çevirir. /api/v1/... -> /api/data/v1/... (img/link ile kullanım için) */
export function getDataGatewayProxyUrl(url: string): string {
  const [pathPart, queryPart] = url.split('?');
  const cleanPath = pathPart.startsWith('/') ? pathPart : `/${pathPart}`;
  let serverPath = cleanPath;
  if (serverPath.startsWith('/api/v1/data/')) {
    serverPath = serverPath.replace(/^\/api\/v1\/data\//, 'v1/data/');
  } else if (serverPath.startsWith('/api/v1/')) {
    serverPath = serverPath.replace(/^\/api\/v1\//, 'v1/');
  } else if (serverPath.startsWith('/api/')) {
    serverPath = serverPath.replace(/^\/api\//, '');
  } else if (serverPath.startsWith('/')) {
    serverPath = serverPath.substring(1);
  }
  return queryPart ? `/api/data/${serverPath}?${queryPart}` : `/api/data/${serverPath}`;
}

/** img src / window.open gibi header gönderilemeyen yerler için: URL'e access_token ekler. Nginx bu query'yi Authorization header'a taşır. */
export function getDataGatewayProxyUrlWithAuth(url: string): string {
  const base = getDataGatewayProxyUrl(url);
  const token = getAccessToken();
  if (!token) return base;
  const sep = base.includes('?') ? '&' : '?';
  return `${base}${sep}access_token=${encodeURIComponent(token)}`;
}

/** DataGateway üzerinden dosyayı blob olarak indirir (önizleme/indirme). fetchFromDataGateway ile aynı URL + Authorization mekanizması. */
export async function fetchBlobFromDataGateway(url: string): Promise<Blob> {
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // devam et, sunucu 401 dönebilir
  }
  const token = getAccessToken();
  if (!token) {
    throw new Error("Access token bulunamadı. Lütfen tekrar giriş yapın.");
  }
  // fetchFromDataGateway ile birebir aynı URL üretimi
  const [pathPart, queryPart] = url.split('?');
  const cleanPath = pathPart.startsWith('/') ? pathPart : `/${pathPart}`;
  let serverPath = cleanPath;
  if (serverPath.startsWith('/api/v1/data/')) {
    serverPath = serverPath.replace(/^\/api\/v1\/data\//, 'v1/data/');
  } else if (serverPath.startsWith('/api/v1/')) {
    serverPath = serverPath.replace(/^\/api\/v1\//, 'v1/');
  } else if (serverPath.startsWith('/api/')) {
    serverPath = serverPath.replace(/^\/api\//, '');
  } else if (serverPath.startsWith('/')) {
    serverPath = serverPath.substring(1);
  }
  const fullUrl = queryPart ? `/api/data/${serverPath}?${queryPart}` : `/api/data/${serverPath}`;

  // Tarayıcıda native fetch kullan; Authorization header'ı fetchFromDataGateway ile aynı şekilde gönder
  const res = await fetch(fullUrl, {
    method: 'GET',
    headers: { Authorization: `Bearer ${token}` },
    credentials: 'same-origin',
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => res.statusText);
    const err: any = new Error(msg || `Request failed: ${res.status}`);
    err.statusCode = res.status;
    err.status = res.status;
    throw err;
  }
  return await res.blob();
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

      // URL formatı: '/api/v1/llm/translate' veya 'v1/llm/translate'
      // Query string'i ayrı çıkar
      const [pathPart, queryPart] = url.split('?');
      const cleanPath = pathPart.startsWith('/') ? pathPart : `/${pathPart}`;
      
      // Server route: '/api/llm/[...path]'
      // Server route receives path and forwards to MngLLM as: llmUrl + '/api/' + path
      // Example: path='v1/llm/translate' -> MngLLM: 'https://localhost:5030/api/v1/llm/translate' ✓
      // So we need to extract 'v1/llm/translate' from '/api/v1/llm/translate'
      let serverPath = cleanPath;
      if (serverPath.startsWith('/api/v1/llm/')) {
        // '/api/v1/llm/translate' -> 'v1/llm/translate'
        // Server route '/api/llm/[...path]' receives 'v1/llm/translate'
        // Server route forwards to MngLLM as: llmUrl + '/api/' + path = 'https://localhost:5030/api/v1/llm/translate' ✓
        serverPath = serverPath.replace('/api/v1/llm/', 'v1/llm/');
      } else if (serverPath.startsWith('/api/v1/')) {
        // '/api/v1/translate' -> 'v1/translate'
        serverPath = serverPath.replace('/api/v1/', 'v1/');
      } else if (serverPath.startsWith('/api/')) {
        // '/api/...' -> '...'
        serverPath = serverPath.replace('/api/', '');
      }
      
      // Remove leading slash if exists (server route expects path without leading slash)
      if (serverPath.startsWith('/')) {
        serverPath = serverPath.slice(1);
      }
      
      // Query string'i tekrar ekle
      // Server route: '/api/llm/[...path]' - path will be 'v1/llm/translate'
      // Full URL to server route: '/api/llm/v1/llm/translate'
      // Server route extracts 'v1/llm/translate' and forwards to MngLLM as '/api/v1/llm/translate'
      const fullUrl = queryPart 
        ? `/api/llm/${serverPath}?${queryPart}`
        : `/api/llm/${serverPath}`;
      
      // Production'da istek nginx'e gider; token header'da gönderilmeli (nginx proxy_set_header Authorization ile iletir)
      const token = getAccessToken();
      const authHeaders = token ? { Authorization: `Bearer ${token}` } : {};
      
      try {
        const response = await $fetch(fullUrl, {
          method,
          ...(body && { body }),
          headers: {
            ...authHeaders,
            ...headers,
          },
        });
        
        resolve(response);
      } catch (fetchError: any) {
        // 401 Unauthorized hatası - token expire olmuş olabilir
        if (fetchError.statusCode === 401 || fetchError.status === 401) {
          try {
            const refreshed = await authStore.refreshAccessToken();
            
            if (refreshed) {
              const retryToken = getAccessToken();
              const retryAuthHeaders = retryToken ? { Authorization: `Bearer ${retryToken}` } : {};
              try {
                const retryResponse = await $fetch(fullUrl, {
                  method,
                  ...(body && { body }),
                  headers: {
                    ...retryAuthHeaders,
                    ...headers,
                  },
                });
                resolve(retryResponse);
                return;
              } catch (retryError: any) {
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

// MngOperations API (gateway: /operations/api/v1)
export function fetchFromOperations(
  url: string,
  method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE" = "GET",
  body?: unknown,
  headers: Record<string, string> = {}
): Promise<unknown> {
  return new Promise(async (resolve, reject) => {
    try {
      const authStore = useAuthStore();
      try {
        await authStore.ensureValidToken();
      } catch {
        // Sunucu 401 dönebilir
      }

      const token = getAccessToken();
      if (!token) {
        throw new Error("Access token bulunamadı. Lütfen tekrar giriş yapın.");
      }

      const [pathPart, queryPart] = url.split("?");
      const cleanPath = pathPart.startsWith("/") ? pathPart : `/${pathPart}`;

      let serverPath = cleanPath;
      if (serverPath.startsWith("/api/v1/")) {
        serverPath = serverPath.replace(/^\/api\/v1\//, "v1/");
      } else if (serverPath.startsWith("/api/")) {
        serverPath = serverPath.replace(/^\/api\//, "");
      } else if (serverPath.startsWith("/")) {
        serverPath = serverPath.substring(1);
      }

      const fullUrl = queryPart ? `/api/operations/${serverPath}?${queryPart}` : `/api/operations/${serverPath}`;

      // GEÇİCİ (perf/oc-optimization): tarayıcıda localStorage.OC_PERF='1' iken çağrı süresi.
      const __ocPerf =
        typeof window !== "undefined" && window.localStorage?.getItem("OC_PERF") === "1";
      const __ocStart = __ocPerf ? performance.now() : 0;
      const __ocLog = () => {
        if (__ocPerf)
          // eslint-disable-next-line no-console
          console.info(`[OC_PERF] ${method} ${serverPath} ${(performance.now() - __ocStart).toFixed(0)}ms`);
      };

      if (method === "DELETE") {
        try {
          const rawResponse = await $fetch.raw(fullUrl, {
            method,
            headers: { Authorization: `Bearer ${token}`, ...headers },
            ...(body && { body }),
          });
          __ocLog();
          if (rawResponse.status === 204) {
            resolve({ success: true, statusCode: 204 });
            return;
          }
          resolve(rawResponse._data);
          return;
        } catch (fetchError: any) {
          if (fetchError.statusCode === 204 || fetchError.response?.status === 204) {
            resolve({ success: true, statusCode: 204 });
            return;
          }
          throw fetchError;
        }
      }

      const response = await $fetch(fullUrl, {
        method,
        headers: { Authorization: `Bearer ${token}`, ...headers },
        ...(body && { body }),
      });
      __ocLog();
      resolve(response);
    } catch (error: any) {
      if (error.statusCode === 401 || error.status === 401) {
        const authStore = useAuthStore();
        try {
          const refreshed = await authStore.refreshAccessToken();
          if (refreshed) {
            const token = getAccessToken();
            if (token) {
              const [pathPart, queryPart] = url.split("?");
              const cleanPath = pathPart.startsWith("/") ? pathPart : `/${pathPart}`;
              let serverPath = cleanPath;
              if (serverPath.startsWith("/api/v1/")) {
                serverPath = serverPath.replace(/^\/api\/v1\//, "v1/");
              } else if (serverPath.startsWith("/api/")) {
                serverPath = serverPath.replace(/^\/api\//, "");
              } else if (serverPath.startsWith("/")) {
                serverPath = serverPath.substring(1);
              }
              const retryFullUrl = queryPart
                ? `/api/operations/${serverPath}?${queryPart}`
                : `/api/operations/${serverPath}`;
              try {
                const retryResponse = await $fetch(retryFullUrl, {
                  method,
                  headers: { Authorization: `Bearer ${token}`, ...headers },
                  ...(body && { body }),
                });
                resolve(retryResponse);
                return;
              } catch (retryError: any) {
                error = retryError;
              }
            }
          }
        } catch {
          await authStore.logout();
          if (process.client) {
            navigateTo("/auth/login");
          }
          reject(new Error("Oturum süresi dolmuş. Lütfen tekrar giriş yapın."));
          return;
        }
      }

      let errorMessage = "İstek başarısız";
      if (error.data) {
        const errorData = error.data;
        if (typeof errorData === "object" && errorData.error && typeof errorData.error === "object") {
          errorMessage = errorData.error.message || errorData.error.code || errorMessage;
        } else if (typeof errorData === "object") {
          errorMessage = errorData.errorDescription || errorData.error || errorData.message || errorMessage;
        } else if (typeof errorData === "string") {
          errorMessage = errorData;
        }
      } else if (error.message) {
        errorMessage = error.message;
      } else if (error.statusMessage) {
        errorMessage = error.statusMessage;
      }
      // Hata gövdesini (code/messageTr/details) ve HTTP durumunu koru; çağıranlar guard (409 vb.) ayırt edebilsin.
      const customError: any = new Error(errorMessage);
      if (error.data !== undefined) customError.data = error.data;
      const sc = error.statusCode ?? error.status ?? error.response?.status;
      if (sc !== undefined) {
        customError.statusCode = sc;
        customError.status = sc;
      }
      if (error.statusMessage) customError.statusMessage = error.statusMessage;
      reject(customError);
    }
  });
}

// MngDocument API (gateway: /documents/api/v1)
export function fetchFromDocuments(
  url: string,
  method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE" = "GET",
  body?: unknown,
  headers: Record<string, string> = {}
): Promise<unknown> {
  return new Promise(async (resolve, reject) => {
    try {
      const authStore = useAuthStore();
      try {
        await authStore.ensureValidToken();
      } catch {
        // Sunucu 401 dönebilir
      }

      const token = getAccessToken();
      if (!token) {
        throw new Error("Access token bulunamadı. Lütfen tekrar giriş yapın.");
      }

      const [pathPart, queryPart] = url.split("?");
      const cleanPath = pathPart.startsWith("/") ? pathPart : `/${pathPart}`;

      let serverPath = cleanPath;
      if (serverPath.startsWith("/api/v1/")) {
        serverPath = serverPath.replace(/^\/api\/v1\//, "v1/");
      } else if (serverPath.startsWith("/api/")) {
        serverPath = serverPath.replace(/^\/api\//, "");
      } else if (serverPath.startsWith("/")) {
        serverPath = serverPath.substring(1);
      }

      const fullUrl = queryPart ? `/api/documents/${serverPath}?${queryPart}` : `/api/documents/${serverPath}`;

      if (method === "DELETE") {
        try {
          const rawResponse = await $fetch.raw(fullUrl, {
            method,
            headers: { Authorization: `Bearer ${token}`, ...headers },
            ...(body && { body }),
          });
          if (rawResponse.status === 204) {
            resolve({ success: true, statusCode: 204 });
            return;
          }
          resolve(rawResponse._data);
          return;
        } catch (fetchError: any) {
          if (fetchError.statusCode === 204 || fetchError.response?.status === 204) {
            resolve({ success: true, statusCode: 204 });
            return;
          }
          throw fetchError;
        }
      }

      const response = await $fetch(fullUrl, {
        method,
        headers: { Authorization: `Bearer ${token}`, ...headers },
        ...(body && { body }),
      });
      resolve(response);
    } catch (error: any) {
      if (error.statusCode === 401 || error.status === 401) {
        const authStore = useAuthStore();
        try {
          const refreshed = await authStore.refreshAccessToken();
          if (refreshed) {
            const token = getAccessToken();
            if (token) {
              const [pathPart, queryPart] = url.split("?");
              const cleanPath = pathPart.startsWith("/") ? pathPart : `/${pathPart}`;
              let serverPath = cleanPath;
              if (serverPath.startsWith("/api/v1/")) {
                serverPath = serverPath.replace(/^\/api\/v1\//, "v1/");
              } else if (serverPath.startsWith("/api/")) {
                serverPath = serverPath.replace(/^\/api\//, "");
              } else if (serverPath.startsWith("/")) {
                serverPath = serverPath.substring(1);
              }
              const retryFullUrl = queryPart
                ? `/api/documents/${serverPath}?${queryPart}`
                : `/api/documents/${serverPath}`;
              try {
                const retryResponse = await $fetch(retryFullUrl, {
                  method,
                  headers: { Authorization: `Bearer ${token}`, ...headers },
                  ...(body && { body }),
                });
                resolve(retryResponse);
                return;
              } catch (retryError: any) {
                error = retryError;
              }
            }
          }
        } catch {
          await authStore.logout();
          if (process.client) {
            navigateTo("/auth/login");
          }
          reject(new Error("Oturum süresi dolmuş. Lütfen tekrar giriş yapın."));
          return;
        }
      }

      let errorMessage = "İstek başarısız";
      if (error.data) {
        const errorData = error.data;
        if (typeof errorData === "object" && errorData.error && typeof errorData.error === "object") {
          errorMessage = errorData.error.message || errorData.error.code || errorMessage;
        } else if (typeof errorData === "object") {
          errorMessage = errorData.errorDescription || errorData.error || errorData.message || errorMessage;
        } else if (typeof errorData === "string") {
          errorMessage = errorData;
        }
      } else if (error.message) {
        errorMessage = error.message;
      } else if (error.statusMessage) {
        errorMessage = error.statusMessage;
      }
      // Hata gövdesini (code/message/details) ve HTTP durumunu koru; çağıranlar guard (409 vb.) ayırt edebilsin.
      const customError: any = new Error(errorMessage);
      if (error.data !== undefined) customError.data = error.data;
      const sc = error.statusCode ?? error.status ?? error.response?.status;
      if (sc !== undefined) {
        customError.statusCode = sc;
        customError.status = sc;
      }
      if (error.statusMessage) customError.statusMessage = error.statusMessage;
      reject(customError);
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
