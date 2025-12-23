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
    // Nuxt server-side API route kullan (SSL sertifika sorununu çözer)
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
    // Nuxt server-side API route kullan (SSL sertifika sorununu çözer)
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
    // Nuxt server-side API route kullan (SSL sertifika sorununu çözer)
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
      // Get token from cookie (NOT from localStorage for security)
      const token = getAccessToken();
      
      if (!token) {
        throw new Error("Access token bulunamadı. Lütfen tekrar giriş yapın.");
      }
      
      // Remove leading slash if exists
      const cleanUrl = url.startsWith('/') ? url.slice(1) : url;
      
      // Use Nuxt server-side proxy to avoid SSL issues
      const response = await $fetch(`/api/keeper/${cleanUrl}`, {
        method,
        headers: {
          Authorization: `Bearer ${token}`,
          ...headers,
        },
        ...(body && { body }),
      });

      resolve(response);
    } catch (error: any) {
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
      const gatewayUrl = config.public.reactorUrl || 'https://localhost:5011';
      
      const response = await fetch(`${gatewayUrl}${url}`, {
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
